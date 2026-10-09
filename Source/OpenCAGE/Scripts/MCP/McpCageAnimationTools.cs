using CATHODE;
using CATHODE.Enums;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CathodeLib.ObjectExtensions;
using Newtonsoft.Json.Linq;
using OpenCAGE.DockPanels;
using OpenCAGE.Undo;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using LiveLink = OpenCAGE.RuntimeUtilsConnection.LiveLink;
using LiveLinkAnimationDrive = OpenCAGE.RuntimeUtilsConnection.LiveLinkAnimationDrive;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// CAGEAnimation entities: reading their tracks, keys, events and bindings, and editing them the way the
    /// CAGEAnimation editor does - every call one <see cref="CageAnimationEdit"/> on the undo history, so an
    /// open editor window and the inspector follow it exactly as they follow an undo. Also what animates an
    /// entity, a posed preview in the viewport, and the ANIMATED_MODEL (environment animation) resource.
    /// </summary>
    internal static class McpCageAnimationTools
    {
        /// <summary>The sub-properties a transform is animated through, in the editor's order.</summary>
        private static readonly string[] Components = { "x", "y", "z", "Yaw", "Pitch", "Roll" };

        /// <summary>What an animation-entity (T_GUID) event may fire (CAGEAnimationEditor.GuidKeyframeFunctionTypes).</summary>
        private static readonly FunctionType[] EventEntityTypes = { FunctionType.CMD_PlayAnimation, FunctionType.CameraPlayAnimation, FunctionType.PlayEnvironmentAnimation };

        private const float Eps = CageAnimationCurves.TimeEpsilon;
        private static readonly ShortGuid AnimLength = ShortGuidUtils.Generate("anim_length");
        private static readonly ShortGuid PlaySpeed = ShortGuidUtils.Generate("playspeed");
        private static readonly ShortGuid PositionParam = ShortGuidUtils.Generate("position");
        private static readonly ShortGuid FovParam = ShortGuidUtils.Generate("fov");

        /// <summary>What a new CAGEAnimation's anim_length is (its type's default).</summary>
        internal const float DefaultLength = 10f;

        /// <summary>
        /// The float tracks retail CAGEAnimations drive, by target type and parameter, counted over 20 retail levels (9 Oct 2026
        /// scan; a transform counts six tracks; 'instance' is a composite instance). The game is known to play these; a parameter
        /// with no retail track may well be ignored at runtime (BSP_TORRENS LightReference intensity_multiplier did not visibly
        /// change under the game's own playback).
        /// </summary>
        private static readonly Dictionary<string, int> RetailTracks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["ModelReference.position"] = 347,
            ["instance.position"] = 210,
            ["VariableFloat.initial_value"] = 42,
            ["LightReference.position"] = 36,
            ["CameraResource.position"] = 6,
            ["FogSetting.exponential_density"] = 1,
            ["FloatMultiply.LHS"] = 1,
        };

        /// <summary>Parameters retail drives on targets whose type the scan could not read (still evidence the parameter plays).</summary>
        private static readonly Dictionary<string, int> RetailParameterOnly = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["position"] = 186,
            ["radiosity_multiplier"] = 3,
            ["float_input"] = 1,
        };

        #region Schemas
        private static JObject Prop(string type, string description) => new JObject() { ["type"] = type, ["description"] = description };

        //An entity path: an array of ids/names, one string split on '/', or a result's {ids} object (McpScript.PathSteps)
        private static JObject PathProp(string description) => new JObject()
        {
            ["anyOf"] = new JArray(
                new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" } },
                new JObject() { ["type"] = "string" },
                new JObject()
                {
                    ["type"] = "object",
                    ["properties"] = new JObject() { ["ids"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" } }, ["path"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" } } },
                    ["additionalProperties"] = true,
                }),
            ["description"] = description,
        };

        private static JObject Item(string description, JObject properties, params string[] required)
        {
            JObject item = new JObject() { ["type"] = "object", ["description"] = description, ["properties"] = properties };
            if (required.Length != 0) item["required"] = new JArray(required);
            return item;
        }

        private const string TargetText = "Entity path (ids/names) from the animation's composite, through instances, e.g. [\"Door_1\"] or [\"Room\", \"Door\"].";
        private const string FromRootText = "The path starts in the level's root composite instead (for things outside the animation's composite).";

        private static readonly JObject SetSpec = Item("One track (or a whole transform) to write.", new JObject()
        {
            ["target"] = PathProp(TargetText),
            ["from_root"] = Prop("boolean", FromRootText),
            ["track"] = Prop("string", "An existing float track id (from get_cage_animation), instead of target/parameter/component."),
            ["parameter"] = Prop("string", "The parameter to drive: 'position' (a transform) or a FLOAT one, e.g. 'initial_value'."),
            ["component"] = Prop("string", "For a transform: x, y, z, Yaw (rotation y), Pitch (rotation x) or Roll (rotation z). Leave out to key whole transforms."),
            ["keys"] = new JObject()
            {
                ["type"] = "array",
                ["description"] = "[time, value] pairs, or {time, value, tan_in?:[dt,dv], tan_out?:[dt,dv]}. A whole transform takes {time, position?:[x,y,z] m, rotation?:[pitch,yaw,roll] degrees} " +
                    "({x?,y?,z?} keys only the axes given), and instead of rotation: look_at:[x,y,z] (face that point from the key's position), forward:[x,y,z] (face along it), with roll? degrees; " +
                    "pivot:[x,y,z] with a rotation (no position) turns it about that point, e.g. a hinge, from where it rests - the position is worked out, and swings of more than 15 degrees get keys along the arc; " +
                    "or from:'viewport_camera' (where the viewport camera is now). A key at an existing time updates it. Times in seconds.",
            },
            ["space"] = Prop("string", "What the keys are in: 'composite' (default: the space of the composite holding the target - find_entities within=<that composite> reports positions in it), 'world' (converted for you; see 'placement') or 'relative' (offsets added to the target's resting value)."),
            ["placement"] = Prop("integer", "space 'world' (and keys from the viewport camera): which placement of the animation's composite the keys are for (0-based, get_placements order); needed when it is placed more than once."),
            ["unwrap"] = Prop("boolean", "Rotation keys (Yaw, Pitch, Roll) written here move by whole turns to within 180 degrees of the key before them, so nothing spins the long way round (default true)."),
            ["replace"] = Prop("boolean", "The keys replace every key the track has."),
            ["remove_times"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["description"] = "Times (seconds) of keys to delete." },
            ["move"] = new JObject()
            {
                ["type"] = "array",
                ["items"] = Item("Move the key at 'from' to 'to' (seconds), keeping its value and tangents.", new JObject() { ["from"] = Prop("number", "Current time."), ["to"] = Prop("number", "New time.") }, "from", "to"),
                ["description"] = "Keys to move in time.",
            },
            ["tangents"] = Prop("string", "Tangents for keys written here without explicit ones: flat (default; eases in and out), smooth (through the neighbours; flat at the ends), auto (smooth, with one-sided slopes at the ends), cyclic (smooth, wrapping round: the last key leaves as the first arrives, for loops) or straight."),
            ["retangent"] = Prop("boolean", "Apply 'tangents' (default smooth) to every key of the track(s), not only the keys written here."),
        });

        private static readonly JObject RetimeSpec = Item("Change the timing of the whole animation (every float track, and the events unless include_events is false). Done before 'remove' and 'set'.", new JObject()
        {
            ["shift"] = Prop("number", "Move every key and event at or after 'from' by this many seconds (negative pulls them earlier); tangents are kept. Inserts or removes time, e.g. a pause."),
            ["scale"] = Prop("number", "Stretch the keys between 'from' and 'to' by this factor about 'from' (2 = twice as slow), tangents and event durations with them; later keys move along. To change speed without touching keys, set the animation's playspeed with set_parameters."),
            ["reverse"] = Prop("boolean", "Play the keys between 'from' and 'to' backwards (mirrored in time, tangents too)."),
            ["ping_pong"] = Prop("boolean", "Append a mirrored copy after the end, so the animation goes there and back (link animation_finished -> start to repeat it); doubles the length."),
            ["from"] = Prop("number", "Start of the range (s, default 0)."),
            ["to"] = Prop("number", "End of the range (s, default the animation's length)."),
            ["include_events"] = Prop("boolean", "Events move with the keys (default true)."),
        });

        private static readonly JObject RemoveSpec = Item("Tracks to drop.", new JObject()
        {
            ["track"] = Prop("string", "A float track id."),
            ["target"] = PathProp(TargetText),
            ["from_root"] = Prop("boolean", FromRootText),
            ["parameter"] = Prop("string", "Only this parameter's tracks (default: every track on the target)."),
            ["component"] = Prop("string", "Only this transform component's track."),
        });

        private static readonly JObject BindingProps = new JObject()
        {
            ["marker"] = PathProp("Bind a PositionMarker, ModelReference, CMD_PlayAnimation or composite instance (path as for targets; an alias or proxy of one too); [] unbinds."),
            ["character"] = PathProp("Bind a Character, VariableThePlayer, ModelReference or composite instance (an alias or proxy of one too); [] unbinds."),
            ["camera"] = PathProp("Bind a CameraResource, for a CameraPlayAnimation event's baked camera clip; [] unbinds."),
            ["from_root"] = Prop("boolean", "These paths start in the level's root composite."),
        };

        private const string PoseHelp = "Sample the composed pose of animated transforms: {step?: s (default 0.5), target?: path (default every animated transform, at most 10), from_root?, space?: 'composite' (default: the space of the composite holding the target) or 'world', placement?: n (world: which placement of the animation's composite), check_collision?: true (world: say where the path passes through or close to collision, as the level stands in the editor)}.";

        private static readonly JObject PoseSpec = new JObject()
        {
            ["type"] = "object",
            ["properties"] = new JObject()
            {
                ["step"] = Prop("number", "Seconds between samples (default 0.5; at most 400 samples a target)."),
                ["target"] = PathProp(TargetText),
                ["from_root"] = Prop("boolean", FromRootText),
                ["space"] = new JObject() { ["type"] = "string", ["enum"] = new JArray("composite", "world"), ["description"] = "composite (default) or world." },
                ["placement"] = Prop("integer", "space world: which placement of the animation's composite (0-based); needed when it is placed more than once."),
                ["check_collision"] = Prop("boolean", "space world: check the sampled path against the level's collision (camera filter)."),
            },
        };
        #endregion

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "get_cage_animation",
                Title = "Read CAGEAnimation",
                Description = "Read a CAGEAnimation entity: length, playspeed, interpolation, float tracks (which entity, parameter and component each drives, its resting value, keys with tangents, optional samples, and whether retail animations drive that parameter), event tracks (string events and the output pins they make, animation-entity events, marker/character/camera bindings) and loose tracks. " +
                    "pose_samples gives an animated transform's composed pose over time (position, rotation, forward) in its composite's space or the world, and can check the path against collision. Track ids it gives are what animate_parameters and set_animation_events take. " +
                    "Make one with create_entities {function: 'CAGEAnimation'} (create_camera_animation builds a whole camera move); check_cage_animation says whether it will play in game.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the CAGEAnimation is in (path or id).", required: true),
                    McpSchema.String("entity", "The CAGEAnimation (id or name).", required: true),
                    McpSchema.String("filter", "Only float tracks whose target or parameter contains this text."),
                    McpSchema.Integer("max_keys", "Keys listed per float track (default 40; the count is always given)."),
                    McpSchema.Integer("max_tracks", "Float tracks listed (default 150; the total is always given)."),
                    McpSchema.Number("sample_step", "Also sample each listed float track every this many seconds (at most 200 samples a track)."),
                    McpSchema.Nested("pose_samples", PoseHelp, PoseSpec)),
                ReadOnly = true,
                Idempotent = true,
                Run = GetAnimation,
            };

            yield return new McpTool()
            {
                Name = "find_cage_animations",
                Title = "Find CAGEAnimations",
                Description = "List the level's CAGEAnimation entities (all of them, or those in one composite), with their length, track counts and what they drive and bind ('drives': Type.parameter; 'instance' for a composite instance), filtered by name or by what they drive (drives_type: 'CameraResource' finds every animation that moves a camera). " +
                    "Given an entity instead: every CAGEAnimation in the level that drives its parameters, binds it as a marker/character/camera, or fires it as an animation event - an animated position is overridden while the animation plays.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "Only animations in this composite; with 'entity', the composite that entity is in."),
                    McpSchema.String("entity", "Report what animates this entity (id or name) instead of listing."),
                    McpSchema.String("filter", "Listing only: text the animation's name or composite path contains."),
                    McpSchema.String("drives_type", "Listing only: animations driving or binding an entity of this function type ('CameraResource', 'LightReference'...), 'instance' for composite instances, or a composite path's text."),
                    McpSchema.String("drives_parameter", "Listing only: animations driving this parameter (e.g. 'position', 'initial_value')."),
                    McpSchema.Limit(100, "results"),
                    McpSchema.Offset("results")),
                ReadOnly = true,
                Idempotent = true,
                Run = FindAnimations,
            };

            yield return new McpTool()
            {
                Name = "animate_parameters",
                Title = "Animate parameters",
                Description = "Edit a CAGEAnimation's float tracks as its editor does: drive an entity's FLOAT or TRANSFORM parameter (a transform gets all six component tracks; a new track starts from the resting value at 0 unless 'replace'), add, update, replace, move or delete keys and tangents, remove tracks or targets, retime the whole animation (shift, scale, reverse, ping-pong), set length and interpolation. " +
                    "Keys are in the space of the composite holding the target unless 'space' is 'world' or 'relative'; rotations are [pitch, yaw, roll] degrees (Pitch = rotation x, Yaw = y, Roll = z; an entity faces +Z, positive pitch looks down) and rotation keys are unwrapped; look_at/forward keys build the rotation for you. " +
                    "To fly a camera, key a CameraResource's position and link something to its activate_camera (create_camera_animation builds the whole rig, animate_camera_path a smooth path); the 'camera' binding of set_animation_events is for CameraPlayAnimation clips. " +
                    "A new CAGEAnimation (create_entities {function: 'CAGEAnimation'}) has anim_length 10 until its first keys set it; playspeed (set_parameters) scales playback; there is no loop parameter (link animation_finished -> start). " +
                    "Retail animates positions (models, composite instances, lights, cameras) and VariableFloat initial_value: other parameters may not change in game, and each touched track says whether retail drives it. One undo step; saved with save_level. Returns the touched tracks' keys.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the CAGEAnimation is in.", required: true),
                    McpSchema.String("entity", "The CAGEAnimation (id or name).", required: true),
                    McpSchema.Array("set", "Tracks to write.", SetSpec),
                    McpSchema.Array("remove", "Tracks to drop: {track}, or {target, from_root?, parameter?, component?} (no parameter drops every track on that target).", RemoveSpec),
                    McpSchema.Nested("retime", "Shift, scale, reverse or ping-pong the whole animation's keys (and events), before 'remove' and 'set'.", RetimeSpec),
                    McpSchema.Number("length", "Playable length in seconds (anim_length). Default: kept, growing to cover the last key; a new animation's first keys set it."),
                    McpSchema.String("interpolation", "Applied to every key, as the editor's 'Bezier curves' box: linear or bezier.", options: new[] { "linear", "bezier" })),
                Destructive = true,
                Run = AnimateParameters,
            };

            yield return new McpTool()
            {
                Name = "check_cage_animation",
                Title = "Check CAGEAnimation",
                Description = "Will this CAGEAnimation play in game? Checks, against the script as it is in the editor: something starts it (a link to start/start_cutscene - in its composite, or into an alias or proxy of it or a TriggerSequence listing it anywhere in the level - or start_on_reset) and it is enabled, " +
                    "a CameraResource it drives or binds is activated (activate_camera) and released (deactivate_camera), its targets and event entities resolve, anim_length against its last key, tracks on parameters no retail animation drives, " +
                    "linked event pins, and whether its composite is placed in the level (and which zones stream it in). Returns 'verdict' (plays, check or will_not_play), 'problems' (each with a fix) and 'notes'. Read-only.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the CAGEAnimation is in.", required: true),
                    McpSchema.String("entity", "The CAGEAnimation (id or name).", required: true)),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() =>
                {
                    Commands commands = McpEditor.RequireCommands(forEditing: false);
                    CAGEAnimation animation = FindAnimation(commands, call, out Composite composite);
                    CompileIfShown(composite);
                    return Check(commands, composite, animation);
                }),
            };

            yield return new McpTool()
            {
                Name = "set_animation_events",
                Title = "Set animation events",
                Description = "Edit a CAGEAnimation's event tracks as its editor does: string events (each gives the animation output pins <name> and reverse_<name> to link from), animation-entity events (CMD_PlayAnimation, CameraPlayAnimation, PlayEnvironmentAnimation), tracks, and entity tracks' marker/character/camera bindings; 'update' changes an event in place and rename's carry_links moves its links to the new pins. " +
                    "A CameraPlayAnimation event plays a baked cutscene camera clip (its data_file), which cannot be authored or listed from game data (list_camera_clips lists those the open level uses); a free camera move keys a CameraResource's position (animate_parameters, animate_camera_path). " +
                    "Refuses to drop linked pins unless allow_breaking_links. One undo step; saved with save_level. Make a CAGEAnimation with create_entities {function: 'CAGEAnimation'}.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the CAGEAnimation is in.", required: true),
                    McpSchema.String("entity", "The CAGEAnimation (id or name).", required: true),
                    McpSchema.Array("add_tracks", "New event tracks.", Item("A new track.", Merge(new JObject()
                    {
                        ["type"] = new JObject() { ["type"] = "string", ["enum"] = new JArray("string", "entity"), ["description"] = "string (named events) or entity (animation-entity events, which can be bound)." },
                        ["ref"] = Prop("string", "A name for it that other entries in this call can give as 'track'."),
                    }, BindingProps), "type")),
                    McpSchema.Strings("remove_tracks", "Event track ids to delete, with their events and bindings."),
                    McpSchema.Array("add", "Events to add.", Item("An event.", new JObject()
                    {
                        ["time"] = Prop("number", "Seconds."),
                        ["event"] = Prop("string", "A string event's name."),
                        ["entity"] = Prop("string", "Or: a CMD_PlayAnimation/CameraPlayAnimation/PlayEnvironmentAnimation in the animation's composite (id or name)."),
                        ["duration"] = Prop("number", "Seconds (default 0)."),
                        ["track"] = Prop("string", "Track id or ref. Default: the unbound string track / the only entity track (made if missing)."),
                    }, "time")),
                    McpSchema.Array("remove", "Events to delete; every field given must match.", Item("Which events.", new JObject()
                    {
                        ["event"] = Prop("string", "String event name."),
                        ["entity"] = Prop("string", "Animation entity (id or name)."),
                        ["time"] = Prop("number", "Seconds."),
                        ["track"] = Prop("string", "Track id or ref."),
                    })),
                    McpSchema.Array("move", "Events to move in time.", Item("Move the one event at 'time' to 'to'.", new JObject()
                    {
                        ["time"] = Prop("number", "Its time now."),
                        ["to"] = Prop("number", "Its new time."),
                        ["event"] = Prop("string", "Pick it by name."),
                        ["entity"] = Prop("string", "Or by animation entity."),
                        ["track"] = Prop("string", "Or by track."),
                    }, "time", "to")),
                    McpSchema.Array("update", "Change one event in place (it keeps its other fields, including a stored reverse id).", Item("Pick the one event at 'time' (with event/entity/track to choose), then change it.", new JObject()
                    {
                        ["time"] = Prop("number", "Its time now (s)."),
                        ["event"] = Prop("string", "Pick it by string event name."),
                        ["entity"] = Prop("string", "Or by the animation entity it fires."),
                        ["track"] = Prop("string", "Or by track id or ref."),
                        ["duration"] = Prop("number", "New duration (s)."),
                        ["entity_to"] = Prop("string", "An animation-entity event: fire this CMD_PlayAnimation/CameraPlayAnimation/PlayEnvironmentAnimation instead (id or name)."),
                        ["to"] = Prop("number", "New time (s)."),
                    }, "time")),
                    McpSchema.Array("rename", "Rename string events (their pins are renamed too).", Item("A rename.", new JObject()
                    {
                        ["from"] = Prop("string", "Current name."),
                        ["to"] = Prop("string", "New name."),
                        ["track"] = Prop("string", "Only on this track."),
                        ["carry_links"] = Prop("boolean", "Move the links on <from> and reverse_<from> to the new pins in the same undo step (default false: linked pins are refused unless allow_breaking_links)."),
                    }, "from", "to")),
                    McpSchema.Array("bindings", "Bind entity tracks to a marker, character or camera.", Item("Bindings for one track.", Merge(new JObject()
                    {
                        ["track"] = Prop("string", "Track id or ref."),
                    }, BindingProps), "track")),
                    McpSchema.Boolean("allow_breaking_links", "Go ahead even if removed or renamed event pins are linked (those links are left pointing at nothing).")),
                Destructive = true,
                Run = SetAnimationEvents,
            };

            yield return new McpTool()
            {
                Name = "preview_cage_animation",
                Title = "Preview CAGEAnimation",
                Description = "Pose a CAGEAnimation in the 3D viewport at a time (or several: 'times' gives one picture with a frame for each), as the editor's Animation Mode does, and return a picture. " +
                    "view 'camera' looks through the camera it moves (what the player will see; default the CameraResource it drives or binds, or 'camera'), at that camera's fov, and puts the viewport camera back afterwards; view 'viewport' (default) keeps the viewport's own camera. " +
                    "Opens the animation's composite in the editor and waits (up to 5 min) for the viewport to finish loading it before posing; the viewport must be on (viewport_action {action: 'enable'} turns it on). Nothing in the level changes and the pose is cleared afterwards (it is up for about a second a frame: an inspector or viewport edit made meanwhile would become a keyframe of the preview's copy instead of applying, and the result says so). Only animated transforms (positions and rotations) are shown. " +
                    "in_game: the running game holds it at the time instead, as Animation Mode's In game does (Live Link; the game evaluates every track, as the level does - the game must run this level, as saved and pushed; activate_camera switches to the animated camera for the picture), waits until a game frame shows it, returns the game's frame, and gives the game its animation back.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the CAGEAnimation is in.", required: true),
                    McpSchema.String("entity", "The CAGEAnimation (id or name).", required: true),
                    McpSchema.Number("time", "Seconds into the animation (or give 'times')."),
                    McpSchema.Nested("times", "Several times (s, up to 9): one picture with a labelled frame for each (viewport only).", new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" } }),
                    McpSchema.String("view", "viewport (default: the viewport's own camera) or camera (look through the animated camera).", options: new[] { "viewport", "camera" }),
                    McpSchema.Nested("camera", "view camera / activate_camera: the camera to use, as a path from the animation's composite (ids/names). Default: the CameraResource the animation drives, else the one it binds.", PathProp("")),
                    McpSchema.Integer("max_width", "Scale the picture down to at most this many pixels wide (default 1024)."),
                    McpSchema.Boolean("in_game", "Hold it in the running game (Live Link) rather than pose it in the viewport."),
                    McpSchema.Boolean("activate_camera", "in_game: call the camera's activate_camera before the picture and deactivate_camera after it, so the frame is taken through it."),
                    McpSchema.Nested("instance_path", "in_game: the placement to hold - the composite instances (ids or names) from the level's root down to an instance of the animation's composite, as get_placements gives them ({ids} objects too). Checked against the script. Default: the path the open composite was reached through from the root, else every placement.", PathProp("")),
                    McpSchema.Boolean("screenshot", "in_game: return the game's frame (default true); false reports what the game shows instead.")),
                ReadOnly = false,
                Run = Preview,
            };

            yield return new McpTool()
            {
                Name = "set_animated_model",
                Title = "Set animated model",
                Description = "Choose the environment-animation entry (the rig and per-bone bind data) an EnvironmentModelReference's ANIMATED_MODEL resource uses - what PlayEnvironmentAnimations on its prop composite play on - as the resource editor does. " +
                    "entry: an id (the result says how many of the composite's parts its bones move and how many placements change), or 'new' - with skeleton an entry built for that environment rig from the composite's ModelReferences, without one an empty entry as the editor adds (it moves nothing). " +
                    "Without entry it only reports (get_entity_resources and describe_animated_prop show the same read-only). An unrigged prop is animated by keying its ModelReference with a CAGEAnimation (animate_parameters) instead. " +
                    "One undo step (a new entry stays in the level's table after undo); saved with save_level.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the entity is in.", required: true),
                    McpSchema.String("entity", "The EnvironmentModelReference (id or name).", required: true),
                    McpSchema.String("entry", "An environment-animation entry id, or 'new'. ('none' is refused for an EnvironmentModelReference: every one keeps an entry.) Leave out to only report."),
                    McpSchema.String("skeleton", "With entry 'new': the environment rig to build the entry for (list_skeletons; describe_animated_prop lists rigs props use)."),
                    McpSchema.Boolean("allow_custom", "Allow an entity other than an EnvironmentModelReference (retail gives ANIMATED_MODEL to nothing else)."),
                    McpSchema.Limit(50, "entries listed when reporting"),
                    McpSchema.Offset("entries")),
                Destructive = true,
                Run = SetAnimatedModel,
            };
        }

        #region Finding
        private static CAGEAnimation FindAnimation(Commands commands, McpCall call, out Composite composite)
        {
            composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
            Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity", required: true));
            if (entity is CAGEAnimation animation)
                return animation;
            string name = McpScript.EntityName(commands, composite, entity);
            if (entity is ProxyEntity proxy && proxy.function == FunctionType.CAGEAnimation)
                throw new McpError(name + " is a proxy of a CAGEAnimation: the tracks live on the CAGEAnimation it points at (describe_entity shows its path).");
            throw new McpError(name + " is a " + McpScript.TypeName(commands, composite, entity) + ", not a CAGEAnimation. find_cage_animations lists the level's CAGEAnimations.");
        }

        /// <summary>The live pages of the composite on screen hold links not yet in the data: compile them first.</summary>
        private static void CompileIfShown(Composite composite)
        {
            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            if (display != null && !display.IsDisposed && display.Populated && display.Composite == composite && !UndoStack.Current.Blocked)
                display.SaveAllFlowgraphs();
        }

        /// <summary>An entity path given by a tool, resolved.</summary>
        internal sealed class Target
        {
            public EntityPath Path;
            public Composite Composite;
            public Entity Entity;
            public List<Tuple<Composite, Entity>> Chain;
            public string Name;
        }

        /// <summary>An entity path as every tool takes one (<see cref="McpScript.PathSteps"/>): an array, a string split on '/', or a result's {ids} object.</summary>
        private static List<string> ReadSteps(JToken token, string where)
        {
            bool empty = token == null || token.Type == JTokenType.Null || (token is JArray array && array.Count == 0) || (token.Type == JTokenType.String && string.IsNullOrWhiteSpace((string)token));
            if (empty)
                throw McpError.Invalid("'" + where + "' is an entity path: ids or names from the animation's composite through instances, e.g. [\"Door_1\"] or [\"Room\", \"Door\"] (or a result's {ids} object).");
            return McpScript.PathSteps(token, where);
        }

        /// <summary>
        /// A path from the animation's composite (or the level root), checked to read back to the same entity
        /// the way the game reads stored paths (<see cref="CommandsUtils.ResolveEntityPath(EntityPath, Composite)"/>).
        /// </summary>
        internal static Target ResolveTarget(Commands commands, Composite composite, JToken token, bool fromRoot, string where)
        {
            List<string> steps = ReadSteps(token, where);
            Composite start = fromRoot ? commands.EntryPoints[0] : composite;
            ShortGuid[] ids = McpScript.ResolvePath(commands, start, steps, out Composite targetComposite, out Entity target);
            EntityPath path = new EntityPath(ids);
            List<Tuple<Composite, Entity>> chain = commands.Utils.ResolveEntityPath(path, composite);
            (Composite readComposite, Entity readEntity) = commands.Utils.GetResolvedTarget(chain);
            if (readEntity != target || readComposite != targetComposite)
                throw new McpError("'" + where + "' reads back as something else from " + composite.name + (fromRoot ? "" : " (try from_root: true)") + ", so it cannot be stored.");
            return new Target()
            {
                Path = path,
                Composite = targetComposite,
                Entity = target,
                Chain = chain,
                Name = McpScript.EntityName(commands, targetComposite, target),
            };
        }

        /// <summary>Whether a stored path names the same thing: the same ids, or the same instance chain read back.</summary>
        private static bool SameTarget(Commands commands, Composite composite, EntityPath stored, Target wanted)
        {
            if (stored == null) return false;
            if (stored == wanted.Path) return true;
            List<Tuple<Composite, Entity>> chain = commands.Utils.ResolveEntityPath(stored, composite);
            if (chain == null || chain.Count == 0 || wanted.Chain == null || chain.Count != wanted.Chain.Count) return false;
            for (int i = 0; i < chain.Count; i++)
                if (chain[i].Item2 != wanted.Chain[i].Item2) return false;
            return true;
        }

        private static string PathKey(EntityPath path)
        {
            if (path?.path == null) return "";
            return string.Join("/", path.path.Select(o => o.AsUInt32.ToString("X8")));
        }
        #endregion

        #region Describing
        private static double R(float value) => Math.Round((double)value, 5);

        private static CAGEAnimation.InterpolationMode InterpolationOf(IEnumerable<CAGEAnimation.FloatTrack> tracks)
        {
            //As the editor decides it when it opens: the first key's mode, Bezier when there are no keys
            foreach (CAGEAnimation.FloatTrack track in tracks)
            {
                if (track?.keyframes == null || track.keyframes.Count == 0) continue;
                return track.keyframes[0].mode == CAGEAnimation.InterpolationMode.Bezier ? CAGEAnimation.InterpolationMode.Bezier : CAGEAnimation.InterpolationMode.Linear;
            }
            return CAGEAnimation.InterpolationMode.Bezier;
        }

        private static float Latest(IEnumerable<CAGEAnimation.FloatTrack> floats, IEnumerable<CAGEAnimation.EventTrack> events)
        {
            float latest = 0f;
            foreach (CAGEAnimation.FloatTrack track in floats)
                foreach (CAGEAnimation.FloatTrack.Keyframe key in track?.keyframes ?? new List<CAGEAnimation.FloatTrack.Keyframe>())
                    latest = Math.Max(latest, key.time);
            foreach (CAGEAnimation.EventTrack track in events)
                foreach (CAGEAnimation.EventTrack.Keyframe key in track?.keyframes ?? new List<CAGEAnimation.EventTrack.Keyframe>())
                    latest = Math.Max(latest, key.time);
            return latest;
        }

        private static float? StoredLength(CAGEAnimation animation) => (animation.GetParameter(AnimLength)?.content as cFloat)?.value;

        /// <summary>The length the editor works with: the latest key or event, or the stored anim_length if longer.</summary>
        private static float EffectiveLength(CAGEAnimation animation) => Math.Max(Latest(animation.floatTracks, animation.eventTracks), StoredLength(animation) ?? 0f);

        private static string ComponentName(ShortGuid sub)
        {
            if (sub.IsInvalid || sub == ShortGuidUtils.Generate("", false)) return "";
            foreach (string component in Components)
                if (ShortGuidUtils.Generate(component, false) == sub) return component;
            return McpScript.ParamName(sub);
        }

        private static string TrackTypeName(CAGEAnimation.EventTrack track)
        {
            //As the curve editor decides a lane's type (CurveEditor.GetEventTrackType)
            if (track.keyframes.Any(o => o.track_type == ANIM_TRACK_TYPE.T_GUID)) return "entity";
            if (track.keyframes.Count != 0) return "string";
            if (track.track_type == ANIM_TRACK_TYPE.T_GUID) return "entity";
            if (track.track_type == ANIM_TRACK_TYPE.T_MASTERING) return "mastering";
            return "string";
        }

        private static JObject DescribeTarget(Commands commands, Composite composite, EntityPath path)
        {
            List<Tuple<Composite, Entity>> resolved = commands.Utils.ResolveEntityPath(path, composite);
            JObject result = new JObject() { ["path"] = McpScript.DescribePath(commands, resolved, path?.path) };
            (Composite c, Entity e) = commands.Utils.GetResolvedTarget(resolved);
            if (e == null)
            {
                result["unresolved"] = true;
                return result;
            }
            if (!commands.Utils.CouldResolve(commands.Utils.ResolveAlias(path, composite)))
            {
                if (commands.Utils.CouldResolve(commands.Utils.ResolveHierarchy(path))) result["from_root"] = true;
                else result["reading"] = "proxy-style (starts with a composite id)";
            }
            result["composite"] = c.name;
            result["id"] = McpScript.Id(e.shortGUID);
            result["name"] = McpScript.EntityName(commands, c, e);
            result["type"] = McpScript.TypeName(commands, c, e);
            return result;
        }

        private static string TargetLabel(Commands commands, Composite composite, EntityPath path)
        {
            (Composite c, Entity e) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(path, composite));
            return e == null ? "(unresolved)" : McpScript.EntityName(commands, c, e);
        }

        /// <summary>What the target of a float track rests at when nothing plays (its parameter's value, or null).</summary>
        private static float? RestValue(Commands commands, Composite composite, CAGEAnimation.Connection connection)
        {
            try
            {
                (Composite c, Entity e) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(connection.connectedEntity, composite));
                if (e == null) return null;
                ParameterData content = e.GetParameter(connection.target_param)?.content;
                if (content == null)
                {
                    try { content = commands.Utils.CreateDefaultParameterData(e, c, connection.target_param); } catch { }
                }
                return ComponentValue(content, ComponentName(connection.target_sub_param));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>What an entity is for the precedent table: its function type, "instance" for a composite instance, else its variant.</summary>
        internal static string KindOf(Entity entity)
        {
            if (entity == null) return null;
            if (entity is FunctionEntity function) return function.function.IsFunctionType ? function.function.AsFunctionType.ToString() : "instance";
            return entity.variant.ToString().ToLowerInvariant();
        }

        /// <summary>Null when retail animations plainly drive this kind of target's parameter; otherwise what is known about it.</summary>
        internal static string PrecedentOf(string kind, string parameter)
        {
            if (kind == null || parameter == null) return null;
            int tracks = RetailTracks.TryGetValue(kind + "." + parameter, out int found) ? found : 0;
            if (tracks >= 6) return null;
            if (tracks > 0) return "rare: " + tracks + " retail track" + (tracks == 1 ? " drives " : "s drive ") + kind + "." + parameter;
            if (RetailParameterOnly.TryGetValue(parameter, out int loose) || RetailTracks.Keys.Any(o => o.EndsWith("." + parameter, StringComparison.OrdinalIgnoreCase)))
                return "none on " + kind + " (retail drives '" + parameter + "' only on other types)";
            return "none: no retail animation drives " + kind + "." + parameter + ", so the game may ignore it";
        }

        private static string Precedent(Commands commands, Composite composite, CAGEAnimation.Connection connection)
        {
            if (connection == null || connection.binding_type != ObjectType.ENTITY) return null;
            (Composite _, Entity e) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(connection.connectedEntity, composite));
            return PrecedentOf(KindOf(e), McpScript.ParamName(connection.target_param));
        }

        /// <summary>The note for a track with no retail precedent: what retail does instead.</summary>
        private static string PrecedentNote(string label, string precedent)
        {
            return label + ": " + precedent + ". Retail animates positions (ModelReference, composite instances, LightReference, CameraResource) and a VariableFloat's initial_value linked on to the parameter; check it in game (preview_cage_animation in_game).";
        }

        private static float? ComponentValue(ParameterData content, string component)
        {
            switch (content)
            {
                case cFloat f: return f.value;
                case cTransform t:
                    switch (component)
                    {
                        case "x": return t.position.X;
                        case "y": return t.position.Y;
                        case "z": return t.position.Z;
                        //Cathode eulers: Y is yaw, X is pitch, Z is roll
                        case "Yaw": return t.rotation.Y;
                        case "Pitch": return t.rotation.X;
                        case "Roll": return t.rotation.Z;
                    }
                    return null;
            }
            return null;
        }

        private static int LinkCount(Composite composite, Entity animation, ShortGuid pin)
        {
            int count = animation.childLinks.Count(o => o.thisParamID == pin);
            foreach (Entity other in composite.GetEntities())
                count += other.childLinks.Count(o => o.linkedEntityID == animation.shortGUID && o.linkedParamID == pin);
            return count;
        }

        /// <summary>Every T_STRING pin the event tracks make, in order.</summary>
        private static List<ShortGuid> EventPins(IEnumerable<CAGEAnimation.EventTrack> tracks)
        {
            List<ShortGuid> pins = new List<ShortGuid>();
            HashSet<ShortGuid> seen = new HashSet<ShortGuid>();
            foreach (CAGEAnimation.EventTrack track in tracks)
            {
                foreach (CAGEAnimation.EventTrack.Keyframe key in track?.keyframes ?? new List<CAGEAnimation.EventTrack.Keyframe>())
                {
                    if (key == null || key.track_type != ANIM_TRACK_TYPE.T_STRING) continue;
                    if (seen.Add(key.forward)) pins.Add(key.forward);
                    if (seen.Add(key.reverse)) pins.Add(key.reverse);
                }
            }
            return pins;
        }

        private sealed class DescribeOptions
        {
            public string Filter;
            public int MaxKeys = 40;
            public int MaxTracks = 150;
            public float SampleStep;
            /// <summary>List keys only for these tracks (others get a count and their time span); null lists all.</summary>
            public HashSet<ShortGuid> KeysFor;
        }

        private static JObject Describe(Commands commands, Composite composite, CAGEAnimation animation, DescribeOptions options)
        {
            CAGEAnimation.InterpolationMode mode = InterpolationOf(animation.floatTracks);
            float length = EffectiveLength(animation);
            float? stored = StoredLength(animation);
            JObject result = new JObject()
            {
                ["composite"] = composite.name,
                ["id"] = McpScript.Id(animation.shortGUID),
                ["name"] = McpScript.EntityName(commands, composite, animation),
                ["length"] = R(length),
                ["length_parameter"] = stored == null ? JValue.CreateNull() : (JToken)R(stored.Value),
                ["interpolation"] = mode == CAGEAnimation.InterpolationMode.Bezier ? "bezier" : "linear",
                ["float_track_count"] = animation.floatTracks.Count,
                ["event_track_count"] = animation.eventTracks.Count,
            };
            float playspeed = (animation.GetParameter(PlaySpeed)?.content as cFloat)?.value ?? 1f;
            if (Math.Abs(playspeed - 1f) > 1e-6f)
                result["playspeed"] = R(playspeed);

            Dictionary<ShortGuid, CAGEAnimation.FloatTrack> floats = new Dictionary<ShortGuid, CAGEAnimation.FloatTrack>();
            foreach (CAGEAnimation.FloatTrack track in animation.floatTracks)
                if (track != null && !floats.ContainsKey(track.shortGUID)) floats.Add(track.shortGUID, track);
            HashSet<ShortGuid> eventIds = new HashSet<ShortGuid>(animation.eventTracks.Where(o => o != null).Select(o => o.shortGUID));

            //Float tracks, grouped by the entity they drive
            Dictionary<string, JObject> targets = new Dictionary<string, JObject>();
            List<JObject> order = new List<JObject>();
            HashSet<ShortGuid> bound = new HashSet<ShortGuid>();
            int matching = 0, shown = 0;
            string filter = string.IsNullOrWhiteSpace(options.Filter) ? null : options.Filter.Trim();
            foreach (CAGEAnimation.Connection connection in animation.connections)
            {
                if (connection == null || !floats.TryGetValue(connection.target_track, out CAGEAnimation.FloatTrack track)) continue;
                bound.Add(track.shortGUID);
                string key = PathKey(connection.connectedEntity);
                if (!targets.TryGetValue(key, out JObject target))
                {
                    target = new JObject() { ["target"] = DescribeTarget(commands, composite, connection.connectedEntity), ["tracks"] = new JArray() };
                    targets.Add(key, target);
                    order.Add(target);
                }
                string parameter = McpScript.ParamName(connection.target_param);
                string component = ComponentName(connection.target_sub_param);
                if (filter != null && parameter.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 && target["target"].ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                matching++;
                if (shown >= options.MaxTracks) continue;
                shown++;
                ((JArray)target["tracks"]).Add(DescribeFloatTrack(commands, composite, connection, track, parameter, component, mode, length, options));
            }
            result["targets"] = new JArray(order.Where(o => ((JArray)o["tracks"]).Count != 0));
            if (matching > shown)
                result["tracks_not_listed"] = (matching - shown) + " of " + matching + " matching tracks (raise max_tracks, or narrow with filter)";

            //Event tracks
            JArray eventTracks = new JArray();
            foreach (CAGEAnimation.EventTrack track in animation.eventTracks)
            {
                if (track == null) continue;
                JObject described = new JObject() { ["track"] = McpScript.Id(track.shortGUID), ["type"] = TrackTypeName(track) };
                JObject bindings = new JObject();
                foreach (CAGEAnimation.Connection connection in animation.connections)
                {
                    if (connection == null || connection.target_track != track.shortGUID) continue;
                    string slot = connection.binding_type.ToString().ToLowerInvariant();
                    string name = slot;
                    for (int i = 2; bindings[name] != null; i++) name = slot + "_" + i;
                    bindings[name] = DescribeTarget(commands, composite, connection.connectedEntity);
                }
                if (bindings.Count != 0) described["bindings"] = bindings;
                JArray events = new JArray();
                foreach (CAGEAnimation.EventTrack.Keyframe key in track.keyframes.Where(o => o != null).OrderBy(o => o.time))
                    events.Add(DescribeEvent(commands, composite, key));
                described["events"] = events;
                eventTracks.Add(described);
            }
            result["event_tracks"] = eventTracks;

            List<ShortGuid> pins = EventPins(animation.eventTracks);
            if (pins.Count != 0)
            {
                result["event_pins"] = new JArray(pins.Select(o => new JObject() { ["pin"] = McpScript.ParamName(o), ["links"] = LinkCount(composite, animation, o) }));
                result["event_pins_note"] = "Each string event gives the animation output pins <name> and reverse_<name> (its counterpart for reverse playback); link from them with add_links (source = this CAGEAnimation, param = the pin).";
            }

            //What nothing plays, or plays nothing
            JArray loose = new JArray(animation.floatTracks.Where(o => o != null && !bound.Contains(o.shortGUID)).Select(o => new JObject() { ["track"] = McpScript.Id(o.shortGUID), ["keys"] = o.keyframes.Count }));
            if (loose.Count != 0) result["unbound_float_tracks"] = loose;
            JArray dangling = new JArray();
            foreach (CAGEAnimation.Connection connection in animation.connections)
            {
                if (connection == null || floats.ContainsKey(connection.target_track) || eventIds.Contains(connection.target_track)) continue;
                dangling.Add(new JObject()
                {
                    ["missing_track"] = McpScript.Id(connection.target_track),
                    ["binding"] = connection.binding_type.ToString().ToLowerInvariant(),
                    ["parameter"] = McpScript.ParamName(connection.target_param),
                    ["target"] = DescribeTarget(commands, composite, connection.connectedEntity),
                });
            }
            if (dangling.Count != 0) result["connections_without_track"] = dangling;
            return result;
        }

        private static JObject DescribeFloatTrack(Commands commands, Composite composite, CAGEAnimation.Connection connection, CAGEAnimation.FloatTrack track, string parameter, string component, CAGEAnimation.InterpolationMode mode, float length, DescribeOptions options)
        {
            JObject described = new JObject() { ["track"] = McpScript.Id(track.shortGUID), ["parameter"] = parameter };
            if (component != "") described["component"] = component;
            described["type"] = connection.target_param_type.ToString();
            if (connection.binding_type != ObjectType.ENTITY) described["binding"] = connection.binding_type.ToString().ToLowerInvariant();
            float? rest = RestValue(commands, composite, connection);
            if (rest != null) described["rest"] = R(rest.Value);
            string precedent = Precedent(commands, composite, connection);
            if (precedent != null) described["retail_precedent"] = precedent;
            described["key_count"] = track.keyframes.Count;

            List<CAGEAnimation.FloatTrack.Keyframe> keys = track.keyframes.Where(o => o != null).OrderBy(o => o.time).ToList();
            if (options.KeysFor == null || options.KeysFor.Contains(track.shortGUID))
            {
                bool bezier = mode == CAGEAnimation.InterpolationMode.Bezier;
                JArray listed = new JArray();
                foreach (CAGEAnimation.FloatTrack.Keyframe key in keys.Take(options.MaxKeys))
                {
                    if (!bezier && key.mode == mode)
                    {
                        listed.Add(new JArray(R(key.time), R(key.value.Y)));
                        continue;
                    }
                    JObject k = new JObject() { ["time"] = R(key.time), ["value"] = R(key.value.Y) };
                    if (bezier)
                    {
                        k["tan_in"] = new JArray(R(key.tan_in.X), R(key.tan_in.Y));
                        k["tan_out"] = new JArray(R(key.tan_out.X), R(key.tan_out.Y));
                    }
                    if (key.mode != mode) k["mode"] = key.mode.ToString().ToLowerInvariant();
                    listed.Add(k);
                }
                described["keys"] = listed;
                if (keys.Count > options.MaxKeys) described["keys_listed"] = options.MaxKeys + " of " + keys.Count + " (raise max_keys)";
            }
            else if (keys.Count != 0)
            {
                described["from"] = R(keys[0].time);
                described["to"] = R(keys[keys.Count - 1].time);
            }

            if (options.SampleStep > 0 && keys.Count != 0)
            {
                float step = Math.Max(options.SampleStep, Math.Max(length, 0.0001f) / 200f);
                JArray samples = new JArray();
                bool bezier = mode == CAGEAnimation.InterpolationMode.Bezier;
                for (int i = 0; i <= 200; i++)
                {
                    float t = i * step;
                    if (t > length + Eps) break;
                    samples.Add(new JArray(R(t), R(CageAnimationCurves.ValueAt(track, t, bezier))));
                }
                described["samples"] = samples;
            }
            return described;
        }

        private static JObject DescribeEvent(Commands commands, Composite composite, CAGEAnimation.EventTrack.Keyframe key)
        {
            JObject described = new JObject() { ["time"] = R(key.time) };
            if (key.track_type == ANIM_TRACK_TYPE.T_GUID)
            {
                //Resolved only in the animation's own composite, as the editor does
                Entity entity = composite.GetEntityByID(key.forward);
                described["entity"] = entity == null
                    ? new JObject() { ["id"] = McpScript.Id(key.forward), ["missing"] = true }
                    : new JObject() { ["id"] = McpScript.Id(entity.shortGUID), ["name"] = McpScript.EntityName(commands, composite, entity), ["type"] = McpScript.TypeName(commands, composite, entity) };
            }
            else
            {
                string name = McpScript.ParamName(key.forward);
                described["event"] = name;
                if (key.reverse != ShortGuidUtils.Generate("reverse_" + name, false))
                    described["reverse_pin"] = McpScript.ParamName(key.reverse);
                if (key.track_type != ANIM_TRACK_TYPE.T_STRING)
                    described["key_type"] = key.track_type.ToString();
            }
            if (key.duration != 0f) described["duration"] = R(key.duration);
            return described;
        }
        #endregion

        #region What animates an entity
        /// <summary>
        /// Every CAGEAnimation in the level that drives a parameter of <paramref name="entity"/> (in
        /// <paramref name="composite"/>), binds it to an event track, or fires it as an animation event.
        /// Paths are resolved from each animation's own composite, as the game reads them.
        /// </summary>
        internal static JArray AnimatedBy(Commands commands, Composite composite, Entity entity, int limit = 100)
        {
            JArray found = new JArray();
            foreach (Composite other in commands.Entries)
            {
                if (other == null) continue;
                foreach (CAGEAnimation animation in other.functions.OfType<CAGEAnimation>())
                {
                    HashSet<ShortGuid> floatIds = new HashSet<ShortGuid>(animation.floatTracks.Where(o => o != null).Select(o => o.shortGUID));
                    List<string> drives = new List<string>();
                    List<string> bindings = new List<string>();
                    JArray paths = new JArray();
                    HashSet<string> pathKeys = new HashSet<string>();
                    foreach (CAGEAnimation.Connection connection in animation.connections)
                    {
                        if (connection?.connectedEntity?.path == null || connection.connectedEntity.GetPointedEntityID() != entity.shortGUID) continue;
                        (Composite c, Entity e) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(connection.connectedEntity, other));
                        if (e != entity || c != composite) continue;
                        if (floatIds.Contains(connection.target_track))
                        {
                            string component = ComponentName(connection.target_sub_param);
                            string label = McpScript.ParamName(connection.target_param) + (component == "" ? "" : "." + component);
                            if (!drives.Contains(label)) drives.Add(label);
                        }
                        else
                        {
                            string label = connection.binding_type.ToString().ToLowerInvariant();
                            if (!bindings.Contains(label)) bindings.Add(label);
                        }
                        if (pathKeys.Add(PathKey(connection.connectedEntity)))
                            paths.Add(McpScript.DescribePath(commands, commands.Utils.ResolveEntityPath(connection.connectedEntity, other), connection.connectedEntity.path));
                    }
                    int events = other == composite
                        ? animation.eventTracks.Where(o => o != null).Sum(o => o.keyframes.Count(k => k != null && k.track_type == ANIM_TRACK_TYPE.T_GUID && k.forward == entity.shortGUID))
                        : 0;
                    if (drives.Count == 0 && bindings.Count == 0 && events == 0) continue;
                    JObject item = new JObject()
                    {
                        ["composite"] = other.name,
                        ["animation"] = new JObject() { ["id"] = McpScript.Id(animation.shortGUID), ["name"] = McpScript.EntityName(commands, other, animation) },
                    };
                    if (other != composite) item["via"] = paths;
                    if (drives.Count != 0) item["drives"] = new JArray(drives);
                    if (bindings.Count != 0) item["bound_as"] = new JArray(bindings);
                    if (events != 0) item["fired_as_event"] = events;
                    found.Add(item);
                    if (found.Count >= limit) return found;
                }
            }
            return found;
        }

        private static object FindAnimations(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                if (call.Has("entity"))
                {
                    if (!call.Has("composite"))
                        throw new McpError("Say which composite the entity is in ('composite').");
                    if (call.Has("filter"))
                        throw new McpError("'filter' is for listing; leave it out when asking about an entity.");
                    Composite composite = McpScript.FindComposite(commands, call.Str("composite"));
                    Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity"));
                    //Every animation found (in the level's composite order, a stable one), paged like the listing
                    JArray by = AnimatedBy(commands, composite, entity, int.MaxValue);
                    JObject result = new JObject()
                    {
                        ["composite"] = composite.name,
                        ["entity"] = new JObject() { ["id"] = McpScript.Id(entity.shortGUID), ["name"] = McpScript.EntityName(commands, composite, entity), ["type"] = McpScript.TypeName(commands, composite, entity) },
                    };
                    McpPaging.Page(call, by.ToList(), result, "animated_by", o => o, 100);
                    return result;
                }

                Composite scope = call.Has("composite") ? McpScript.FindComposite(commands, call.Str("composite")) : null;
                string filter = call.Str("filter");
                string drivesType = call.Str("drives_type")?.Trim();
                string drivesParameter = call.Str("drives_parameter")?.Trim();
                if (string.IsNullOrEmpty(drivesType)) drivesType = null;
                if (string.IsNullOrEmpty(drivesParameter)) drivesParameter = null;
                List<(Composite composite, CAGEAnimation animation, Driven driven)> all = new List<(Composite, CAGEAnimation, Driven)>();
                List<string> names = new List<string>();
                HashSet<string> kindsSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Composite composite in scope == null ? commands.Entries : new List<Composite>() { scope })
                {
                    if (composite == null) continue;
                    foreach (CAGEAnimation animation in composite.functions.OfType<CAGEAnimation>())
                    {
                        string name = McpScript.EntityName(commands, composite, animation);
                        names.Add(name);
                        if (!string.IsNullOrWhiteSpace(filter)
                            && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                            && (composite.name ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        Driven driven = DrivenBy(commands, composite, animation);
                        foreach (string kind in driven.Kinds) kindsSeen.Add(kind);
                        if (drivesType != null && !driven.Matches(drivesType, drivesParameter)) continue;
                        if (drivesType == null && drivesParameter != null && !driven.Matches(null, drivesParameter)) continue;
                        all.Add((composite, animation, driven));
                    }
                }
                all = all.OrderBy(o => o.composite.name, StringComparer.OrdinalIgnoreCase).ThenBy(o => McpScript.EntityName(commands, o.composite, o.animation), StringComparer.OrdinalIgnoreCase).ThenBy(o => o.animation.shortGUID.AsUInt32).ToList();
                JObject listing = new JObject();
                McpPaging.Page(call, all, listing, "animations", o =>
                {
                    JObject row = new JObject()
                    {
                        ["composite"] = o.composite.name,
                        ["id"] = McpScript.Id(o.animation.shortGUID),
                        ["name"] = McpScript.EntityName(commands, o.composite, o.animation),
                        ["length"] = R(EffectiveLength(o.animation)),
                        ["float_tracks"] = o.animation.floatTracks.Count,
                        ["targets"] = o.driven.Targets,
                        ["event_tracks"] = o.animation.eventTracks.Count,
                        ["events"] = o.animation.eventTracks.Where(t => t != null).Sum(t => t.keyframes.Count),
                    };
                    if (o.driven.Drives.Count != 0) row["drives"] = new JArray(o.driven.Drives.Take(12));
                    if (o.driven.Bound.Count != 0) row["binds"] = new JArray(o.driven.Bound.Take(12));
                    return row;
                }, 100);
                if (all.Count == 0)
                {
                    if (drivesType != null || drivesParameter != null)
                        call.Note("No animation " + (scope == null ? "in the level" : "in " + scope.name) + " drives " + (drivesType ?? "anything") + (drivesParameter != null ? "." + drivesParameter : "") + "." +
                            (kindsSeen.Count != 0 ? " What they drive or bind: " + string.Join(", ", kindsSeen.OrderBy(o => o).Take(20)) + "." : ""));
                    else if (!string.IsNullOrWhiteSpace(filter))
                        call.Note("No animation's name or composite contains '" + filter + "'." + McpNames.DidYouMean(names.Distinct(), filter));
                }
                return listing;
            });
        }

        /// <summary>What one animation drives (Type.parameter, 'instance' for composite instances) and binds, for listing and filtering.</summary>
        private sealed class Driven
        {
            public List<string> Drives = new List<string>();
            public List<string> Bound = new List<string>();
            public HashSet<string> Kinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public List<string> CompositesTouched = new List<string>();
            public int Targets;

            public bool Matches(string type, string parameter)
            {
                if (type != null)
                {
                    bool kind = Kinds.Contains(type) || CompositesTouched.Any(o => o.IndexOf(type.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!kind) return false;
                    if (parameter == null) return true;
                    return Drives.Any(o => o.StartsWith(type + ".", StringComparison.OrdinalIgnoreCase) && o.EndsWith("." + parameter, StringComparison.OrdinalIgnoreCase))
                        || (CompositesTouched.Count != 0 && Drives.Any(o => o.StartsWith("instance.", StringComparison.OrdinalIgnoreCase) && o.EndsWith("." + parameter, StringComparison.OrdinalIgnoreCase)));
                }
                return Drives.Any(o => o.EndsWith("." + parameter, StringComparison.OrdinalIgnoreCase));
            }
        }

        private static Driven DrivenBy(Commands commands, Composite composite, CAGEAnimation animation)
        {
            Driven driven = new Driven();
            HashSet<ShortGuid> floatIds = new HashSet<ShortGuid>(animation.floatTracks.Where(t => t != null).Select(t => t.shortGUID));
            HashSet<string> targets = new HashSet<string>();
            foreach (CAGEAnimation.Connection connection in animation.connections)
            {
                if (connection == null) continue;
                Entity entity = null;
                try { entity = commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(connection.connectedEntity, composite)).Item2; } catch { }
                string kind = KindOf(entity) ?? "unresolved";
                driven.Kinds.Add(kind);
                if (kind == "instance")
                {
                    string instanced = McpScript.InstancedComposite(commands, entity)?.name;
                    if (instanced != null && !driven.CompositesTouched.Contains(instanced)) driven.CompositesTouched.Add(instanced);
                }
                if (floatIds.Contains(connection.target_track))
                {
                    targets.Add(PathKey(connection.connectedEntity));
                    string label = kind + "." + McpScript.ParamName(connection.target_param);
                    if (!driven.Drives.Contains(label)) driven.Drives.Add(label);
                }
                else
                {
                    string label = connection.binding_type.ToString().ToLowerInvariant() + ": " + kind;
                    if (!driven.Bound.Contains(label)) driven.Bound.Add(label);
                }
            }
            driven.Targets = targets.Count;
            return driven;
        }
        #endregion

        #region get_cage_animation and its pose samples
        private static object GetAnimation(McpCall call)
        {
            double step = call.Num("sample_step", 0);
            if (double.IsNaN(step) || step < 0)
                throw new McpError("'sample_step' must be a positive number of seconds.");
            JObject pose = call.Has("pose_samples") ? call.Object("pose_samples") : null;
            List<KeyValuePair<JObject, List<KeyValuePair<float, Vector3>>>> paths = new List<KeyValuePair<JObject, List<KeyValuePair<float, Vector3>>>>();
            JObject result = McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                CAGEAnimation animation = FindAnimation(commands, call, out Composite composite);
                CompileIfShown(composite);
                JObject described = Describe(commands, composite, animation, new DescribeOptions()
                {
                    Filter = call.Str("filter"),
                    MaxKeys = Math.Max(0, call.Int("max_keys", 40)),
                    MaxTracks = Math.Max(0, call.Int("max_tracks", 150)),
                    SampleStep = (float)step,
                });
                if (pose != null)
                    described["pose_samples"] = PoseSamples(call, commands, composite, animation, pose, paths);
                return described;
            });
            //The collision check reads the level's collision off the UI thread
            foreach (KeyValuePair<JObject, List<KeyValuePair<float, Vector3>>> path in paths)
                path.Key["collision"] = McpCameraTools.CheckPath(call, path.Value, "camera");
            return result;
        }

        /// <summary>
        /// The composed pose of each animated transform (the six component tracks, the resting value where one has no track)
        /// at steps through the animation, in the space of the composite holding the target or in world space. UI thread.
        /// <paramref name="paths"/> gets each target's world points when a collision check was asked for.
        /// </summary>
        private static JArray PoseSamples(McpCall call, Commands commands, Composite composite, CAGEAnimation animation, JObject spec, List<KeyValuePair<JObject, List<KeyValuePair<float, Vector3>>>> paths)
        {
            float step = spec["step"] != null && spec["step"].Type != JTokenType.Null ? ReadNumber(spec["step"], "pose_samples.step") : 0.5f;
            if (step <= 0.001f) throw new McpError("'pose_samples.step' is seconds, more than 0.");
            string space = spec["space"] == null || spec["space"].Type == JTokenType.Null ? "composite" : McpValues.ReadString(spec["space"]).Trim().ToLowerInvariant();
            if (space == "local") space = "composite";
            if (space != "composite" && space != "world") throw new McpError("'pose_samples.space' is composite or world.");
            bool world = space == "world";
            int? placement = spec["placement"] != null && spec["placement"].Type != JTokenType.Null ? McpValues.ReadInt(spec["placement"], "pose_samples.placement") : (int?)null;
            bool check = ReadFlag(spec["check_collision"], "pose_samples.check_collision");
            if (check && !world) throw new McpError("'pose_samples.check_collision' works in world space: add space: 'world'.");
            Target only = spec["target"] != null && spec["target"].Type != JTokenType.Null ? ResolveTarget(commands, composite, spec["target"], ReadFlag(spec["from_root"], "pose_samples.from_root"), "pose_samples.target") : null;

            //The transforms the animation drives, each with its component tracks
            Dictionary<string, CAGEAnimation.FloatTrack> floats = animation.floatTracks.Where(o => o != null).GroupBy(o => o.shortGUID).ToDictionary(o => PathKeyOf(o.Key), o => o.First());
            List<KeyValuePair<EntityPath, Dictionary<string, CAGEAnimation.FloatTrack>>> groups = new List<KeyValuePair<EntityPath, Dictionary<string, CAGEAnimation.FloatTrack>>>();
            Dictionary<string, int> index = new Dictionary<string, int>();
            foreach (CAGEAnimation.Connection connection in animation.connections)
            {
                if (connection == null || connection.target_param != PositionParam || !floats.TryGetValue(PathKeyOf(connection.target_track), out CAGEAnimation.FloatTrack track)) continue;
                if (only != null && !SameTarget(commands, composite, connection.connectedEntity, only)) continue;
                string key = PathKey(connection.connectedEntity);
                if (!index.TryGetValue(key, out int at))
                {
                    index[key] = at = groups.Count;
                    groups.Add(new KeyValuePair<EntityPath, Dictionary<string, CAGEAnimation.FloatTrack>>(connection.connectedEntity, new Dictionary<string, CAGEAnimation.FloatTrack>()));
                }
                string component = ComponentName(connection.target_sub_param);
                if (Components.Contains(component)) groups[at].Value[component] = track;
            }
            if (groups.Count == 0)
                throw new McpError("The animation drives no transform" + (only != null ? " on " + only.Name : "") + ", so there is no pose to sample (sample_step samples its other tracks).");
            if (groups.Count > 10)
                call.Note("pose_samples: the first 10 of " + groups.Count + " animated transforms are sampled; give 'target' for another.");

            bool bezier = InterpolationOf(animation.floatTracks) == CAGEAnimation.InterpolationMode.Bezier;
            float length = EffectiveLength(animation);
            int count = Math.Max(1, (int)Math.Floor(length / step + 1e-4f) + 1);
            if (count > 400)
            {
                step = length / 399f;
                count = 400;
                call.Note("pose_samples: step widened to " + R(step) + " s (at most 400 samples a target).");
            }

            JArray result = new JArray();
            foreach (KeyValuePair<EntityPath, Dictionary<string, CAGEAnimation.FloatTrack>> group in groups.Take(10))
            {
                (Composite holder, Entity entity) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(group.Key, composite));
                JObject entry = new JObject() { ["target"] = DescribeTarget(commands, composite, group.Key) };
                if (entity == null)
                {
                    entry["note"] = "the path does not resolve, so it has no pose";
                    result.Add(entry);
                    continue;
                }
                cTransform rest = InstanceTransform.TransformOf(entity) ?? new cTransform(Vector3.Zero, Vector3.Zero);
                cTransform frame = null;
                if (world)
                {
                    frame = TargetFrame(commands, composite, group.Key, placement, "'pose_samples.placement'", out JObject described);
                    entry["space"] = "world";
                    entry["frame"] = described;
                }
                else
                    entry["space"] = "composite " + holder.name;
                List<Vector3> positions = new List<Vector3>();
                List<Vector3> rotations = new List<Vector3>();
                List<float> times = new List<float>();
                for (int i = 0; i < count; i++)
                {
                    float t = Math.Min(i * step, length);
                    float Value(string c, float fallback) => group.Value.TryGetValue(c, out CAGEAnimation.FloatTrack track) && track.keyframes.Count != 0 ? CageAnimationCurves.ValueAt(track, t, bezier) : fallback;
                    Vector3 position = new Vector3(Value("x", rest.position.X), Value("y", rest.position.Y), Value("z", rest.position.Z));
                    Vector3 rotation = new Vector3(Value("Pitch", rest.rotation.X), Value("Yaw", rest.rotation.Y), Value("Roll", rest.rotation.Z));
                    if (frame != null)
                    {
                        cTransform placed = InstanceTransform.Compose(frame, new cTransform(position, rotation));
                        position = placed.position;
                        rotation = placed.rotation;
                    }
                    times.Add(t);
                    positions.Add(position);
                    rotations.Add(rotation);
                }
                if (frame != null) InstanceTransform.UnwrapRotations(rotations);
                JArray samples = new JArray();
                for (int i = 0; i < times.Count; i++)
                    samples.Add(new JObject()
                    {
                        ["t"] = R(times[i]),
                        ["position"] = McpCollision.V(positions[i]),
                        ["rotation"] = McpCollision.V(rotations[i]),
                        ["forward"] = McpCollision.V(InstanceTransform.Forward(rotations[i])),
                    });
                entry["samples"] = samples;
                if (check)
                    paths.Add(new KeyValuePair<JObject, List<KeyValuePair<float, Vector3>>>(entry, times.Select((t, i) => new KeyValuePair<float, Vector3>(t, positions[i])).ToList()));
                result.Add(entry);
            }
            return result;
        }

        private static string PathKeyOf(ShortGuid id) => id.AsUInt32.ToString("X8");
        #endregion

        #region For the camera tools
        /// <summary>
        /// Write generated keys onto a target's position tracks (x ... Roll), adding the tracks it lacks as the editor does.
        /// <paramref name="replace"/>: they become each track's keys; otherwise they replace only the keys within their own
        /// time span. Components not in <paramref name="keys"/> keep their tracks (a new one rests at its resting value). UI thread.
        /// </summary>
        internal static string WriteTransformKeys(Working w, Target target, Dictionary<string, List<CAGEAnimation.FloatTrack.Keyframe>> keys, bool replace)
        {
            ParameterData resting = RestingContent(w.Commands, target.Composite, target.Entity, PositionParam);
            if (!(resting is cTransform) && resting != null)
                throw new McpError(target.Name + "'s position is not a transform, so it cannot be keyed as one.");
            List<string> made = new List<string>();
            int written = 0;
            foreach (string c in Components)
            {
                CAGEAnimation.FloatTrack track = FindTrack(w, target, PositionParam, c);
                if (track == null)
                {
                    track = AddTrack(w, target, PositionParam, DataType.TRANSFORM, c, ComponentValue(resting, c) ?? 0f);
                    made.Add(c);
                }
                if (!keys.TryGetValue(c, out List<CAGEAnimation.FloatTrack.Keyframe> given) || given.Count == 0)
                    continue;
                foreach (CAGEAnimation.FloatTrack.Keyframe key in given)
                    key.mode = w.Mode;
                if (replace)
                {
                    track.keyframes = new List<CAGEAnimation.FloatTrack.Keyframe>(given);
                    w.Exact.Add(track);
                }
                else
                {
                    float from = given.Min(o => o.time) - Eps, to = given.Max(o => o.time) + Eps;
                    track.keyframes = track.keyframes.Where(o => o.time < from || o.time > to).Concat(given).OrderBy(o => o.time).ToList();
                }
                written = Math.Max(written, given.Count);
                w.Touched.Add(track);
                w.Changed = true;
            }
            return target.Name + ".position: " + (made.Count != 0 ? "animated " + string.Join(", ", made) + "; " : "") + (replace ? "replaced with " : "wrote ") + written + " keys on each of " + string.Join(", ", Components.Where(keys.ContainsKey));
        }

        /// <summary>The time of the working copy's last key or event.</summary>
        internal static float LatestOf(Working w) => Latest(w.FloatTracks, w.EventTracks);
        #endregion

        #region check_cage_animation
        private static readonly ShortGuid StartPin = ShortGuidUtils.Generate("start");
        private static readonly ShortGuid StartCutscenePin = ShortGuidUtils.Generate("start_cutscene");
        private static readonly ShortGuid EnablePin = ShortGuidUtils.Generate("enable");
        private static readonly ShortGuid FinishedPin = ShortGuidUtils.Generate("animation_finished");
        private static readonly ShortGuid ActivateCameraPin = ShortGuidUtils.Generate("activate_camera");
        private static readonly ShortGuid DeactivateCameraPin = ShortGuidUtils.Generate("deactivate_camera");

        private static bool FlagOf(Commands commands, Composite composite, Entity entity, string parameter, bool fallback)
        {
            ParameterData data = entity.GetParameter(parameter)?.content;
            if (data == null)
                try { data = commands.Utils.CreateDefaultParameterData(entity, composite, ShortGuidUtils.Generate(parameter)); } catch { }
            return data is cBool flag ? flag.value : fallback;
        }

        /// <summary>
        /// The links into one pin of an entity, from the rest of its composite, as "Name.pin". Its own links back into itself are
        /// left out: they fire only once something else has started it, so they never start it.
        /// </summary>
        private static List<string> LinksInto(Commands commands, Composite composite, Entity entity, ShortGuid pin)
        {
            List<string> found = new List<string>();
            foreach (Entity other in composite.GetEntities())
            {
                if (other == entity) continue;
                foreach (EntityConnector link in other.childLinks)
                    if (link.linkedEntityID == entity.shortGUID && link.linkedParamID == pin)
                        found.Add(McpScript.EntityName(commands, composite, other) + "." + McpScript.ParamName(link.thisParamID) + (other is VariableEntity ? " (the composite's pin, fed from outside)" : ""));
            }
            return found;
        }

        /// <summary>Whether a stored path's last step (before its terminator) is this id: the cheap test before resolving it.</summary>
        private static bool PathEndsAt(ShortGuid[] path, ShortGuid id)
        {
            if (path == null) return false;
            int last = path.Length - 1;
            if (last >= 0 && path[last] == ShortGuid.Invalid) last--;
            return last >= 0 && path[last] == id;
        }

        /// <summary>
        /// The calls into pins of an entity from elsewhere in the level, which <see cref="LinksInto"/> does not see: links into
        /// the aliases and proxies standing for it (in any composite), and TriggerSequences listing it whose methods include
        /// the pin (calling one calls it on every entry). Only stored paths ending at the entity's id are resolved. UI thread.
        /// </summary>
        private static List<(ShortGuid pin, string from)> CallsFromElsewhere(Commands commands, Composite composite, Entity entity, params ShortGuid[] pins)
        {
            List<(ShortGuid, string)> found = new List<(ShortGuid, string)>();
            bool IsIt(List<Tuple<Composite, Entity>> path)
            {
                try { return commands.Utils.GetResolvedTarget(path) == (composite, entity); }
                catch { return false; }
            }
            foreach (Composite owner in commands.Entries)
            {
                if (owner == null) continue;
                List<Entity> pointers = new List<Entity>();
                foreach (AliasEntity alias in owner.aliases)
                    if (PathEndsAt(alias.alias?.path, entity.shortGUID) && IsIt(commands.Utils.ResolveAlias(alias, owner))) pointers.Add(alias);
                foreach (ProxyEntity proxy in owner.proxies)
                    if (PathEndsAt(proxy.proxy?.path, entity.shortGUID) && IsIt(commands.Utils.ResolveProxy(proxy))) pointers.Add(proxy);
                if (pointers.Count != 0)
                {
                    foreach (Entity other in owner.GetEntities())
                        foreach (EntityConnector link in other.childLinks)
                        {
                            if (!pins.Contains(link.linkedParamID)) continue;
                            Entity pointer = pointers.FirstOrDefault(o => o.shortGUID == link.linkedEntityID);
                            if (pointer == null || other == pointer) continue;
                            found.Add((link.linkedParamID, McpScript.EntityName(commands, owner, other) + "." + McpScript.ParamName(link.thisParamID) + " (in " + owner.name + ", through " + McpScript.Kind(pointer) + " " + McpScript.EntityName(commands, owner, pointer) + ")"));
                        }
                }
                //TriggerSequences (and proxies carrying one) listing it
                foreach (Entity holder in owner.functions.OfType<TriggerSequence>().Cast<Entity>().Concat(owner.proxies.Where(o => o.function == FunctionType.TriggerSequence)))
                {
                    List<TriggerSequence.SequenceEntry> entries = (holder as TriggerSequence)?.sequence ?? (holder as ProxyEntity)?.sequence;
                    List<TriggerSequence.MethodEntry> methods = (holder as TriggerSequence)?.methods ?? (holder as ProxyEntity)?.methods;
                    if (entries == null || methods == null) continue;
                    foreach (ShortGuid pin in pins)
                    {
                        if (!methods.Any(o => o?.method == pin)) continue;
                        TriggerSequence.SequenceEntry entry = entries.FirstOrDefault(o => PathEndsAt(o?.connectedEntity?.path, entity.shortGUID) && IsIt(commands.Utils.ResolveEntityPath(o.connectedEntity, owner)));
                        if (entry != null)
                            found.Add((pin, McpScript.EntityName(commands, owner, holder) + "." + McpScript.ParamName(pin) + " (a TriggerSequence in " + owner.name + " listing it, " + Math.Round((double)entry.timing, 2) + " s in)"));
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// Whether a CAGEAnimation will play in game, judged from the script as it is in the editor: what starts and enables it,
        /// the cameras it moves or binds (activated? released?), targets and event entities that do not resolve, anim_length,
        /// parameters retail never animates, and where its composite is placed. UI thread.
        /// </summary>
        internal static JObject Check(Commands commands, Composite composite, CAGEAnimation animation)
        {
            JArray problems = new JArray();
            List<string> notes = new List<string>();
            void Problem(string code, string severity, string what, string fix) => problems.Add(new JObject() { ["code"] = code, ["severity"] = severity, ["problem"] = what, ["fix"] = fix });
            string name = McpScript.EntityName(commands, composite, animation);
            JObject result = new JObject() { ["composite"] = composite.name, ["id"] = McpScript.Id(animation.shortGUID), ["name"] = name };

            //What starts it and enables it: links in its composite, and links into aliases or proxies of it and TriggerSequences listing it elsewhere
            List<(ShortGuid pin, string from)> elsewhere = CallsFromElsewhere(commands, composite, animation, StartPin, StartCutscenePin, EnablePin);
            List<string> starts = LinksInto(commands, composite, animation, StartPin).Concat(LinksInto(commands, composite, animation, StartCutscenePin))
                .Concat(elsewhere.Where(o => o.pin == StartPin || o.pin == StartCutscenePin).Select(o => o.from)).ToList();
            bool startOnReset = FlagOf(commands, composite, animation, "start_on_reset", false);
            bool enableOnReset = FlagOf(commands, composite, animation, "enable_on_reset", true);
            if (startOnReset) starts.Insert(0, "start_on_reset");
            result["started_by"] = new JArray(starts);
            if (starts.Count == 0)
                Problem("never_started", "blocker", "Nothing starts it: no link into its start or start_cutscene (in its composite, or through an alias, proxy or TriggerSequence elsewhere), and start_on_reset is false.", "Link a trigger to its start (add_links), e.g. a ZoneLoaded's on_loaded or a PlayerTriggerBox's on_entered - or set start_on_reset true. create_camera_animation wires this for a camera move.");
            if (!enableOnReset && LinksInto(commands, composite, animation, EnablePin).Count == 0 && !elsewhere.Any(o => o.pin == EnablePin))
                Problem("never_enabled", "blocker", "enable_on_reset is false and nothing links to its enable, so it stays disabled.", "Link the trigger that starts it to its enable as well, or set enable_on_reset true.");
            bool loops = animation.childLinks.Any(o => o.thisParamID == FinishedPin && o.linkedEntityID == animation.shortGUID && (o.linkedParamID == StartPin || o.linkedParamID == StartCutscenePin));
            if (loops) notes.Add("It loops: its animation_finished starts it again.");

            //Tracks and their targets
            HashSet<ShortGuid> floatIds = new HashSet<ShortGuid>(animation.floatTracks.Where(o => o != null).Select(o => o.shortGUID));
            int keys = animation.floatTracks.Where(o => o?.keyframes != null).Sum(o => o.keyframes.Count);
            int events = animation.eventTracks.Where(o => o?.keyframes != null).Sum(o => o.keyframes.Count);
            if (keys == 0 && events == 0)
                Problem("empty", "blocker", "It has no keys and no events, so playing it does nothing.", "Key something with animate_parameters (or animate_camera_path), or add events with set_animation_events.");
            JArray unresolved = new JArray();
            HashSet<string> precedentSaid = new HashSet<string>();
            List<(Composite composite, Entity entity, string how)> cameras = new List<(Composite, Entity, string)>();
            foreach (CAGEAnimation.Connection connection in animation.connections)
            {
                if (connection == null) continue;
                (Composite c, Entity e) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(connection.connectedEntity, composite));
                if (e == null)
                {
                    unresolved.Add(new JObject() { ["path"] = McpScript.DescribePath(commands, commands.Utils.ResolveEntityPath(connection.connectedEntity, composite), connection.connectedEntity?.path), ["as"] = floatIds.Contains(connection.target_track) ? McpScript.ParamName(connection.target_param) : connection.binding_type.ToString().ToLowerInvariant() });
                    continue;
                }
                string kind = KindOf(e);
                if (kind == "CameraResource" && !cameras.Any(o => o.entity == e))
                    cameras.Add((c, e, floatIds.Contains(connection.target_track) ? "moved (" + McpScript.ParamName(connection.target_param) + ")" : "bound (" + connection.binding_type.ToString().ToLowerInvariant() + ")"));
                if (!floatIds.Contains(connection.target_track)) continue;
                string parameter = McpScript.ParamName(connection.target_param);
                string precedent = PrecedentOf(kind, parameter);
                if (precedent != null && !precedent.StartsWith("rare") && precedentSaid.Add(kind + "." + parameter))
                    Problem("no_retail_precedent", "warning", McpScript.EntityName(commands, c, e) + "." + parameter + ": " + precedent + ".", "Animate a VariableFloat's initial_value and link it to the parameter (retail's way), or a position; check it with preview_cage_animation in_game.");
            }
            if (unresolved.Count != 0)
            {
                result["unresolved"] = unresolved;
                Problem("unresolved_targets", "issue", unresolved.Count + " of its tracks or bindings point at nothing (listed in 'unresolved'): those parts do nothing.", "Re-target them, or drop them with animate_parameters remove / set_animation_events bindings.");
            }

            //Length
            float latest = Latest(animation.floatTracks, animation.eventTracks);
            float? stored = StoredLength(animation);
            if (stored != null && latest > stored.Value + 0.01f)
                Problem("length_short", "issue", "anim_length is " + R(stored.Value) + " s but keys or events run to " + R(latest) + " s; the game stops at anim_length.", "animate_parameters with length " + R(latest) + ".");
            else if (stored != null && latest > Eps && stored.Value > latest + 0.5f)
                notes.Add("anim_length (" + R(stored.Value) + " s) is " + R(stored.Value - latest) + " s past the last " + (keys != 0 ? "key: it holds its last pose" : "event: it runs on") + " that long before animation_finished.");
            float playspeed = (animation.GetParameter(PlaySpeed)?.content as cFloat)?.value ?? 1f;
            if (playspeed <= 0f)
                Problem("playspeed", "blocker", "playspeed is " + R(playspeed) + ", so it never moves.", "set_parameters playspeed 1.");

            //Events: entities that are gone, pins nothing listens to
            JArray missing = new JArray();
            foreach (CAGEAnimation.EventTrack track in animation.eventTracks.Where(o => o != null))
                foreach (CAGEAnimation.EventTrack.Keyframe key in track.keyframes.Where(o => o != null && o.track_type == ANIM_TRACK_TYPE.T_GUID))
                    if (composite.GetEntityByID(key.forward) == null) missing.Add(new JObject() { ["time"] = R(key.time), ["id"] = McpScript.Id(key.forward) });
            if (missing.Count != 0)
                Problem("missing_event_entities", "issue", missing.Count + " animation-entity event(s) fire an entity that is not in " + composite.name + ": " + string.Join(", ", missing.Select(o => (string)o["id"] + " at " + o["time"] + " s")) + ".", "set_animation_events update with entity_to, or remove them.");
            List<ShortGuid> pins = EventPins(animation.eventTracks);
            List<string> silent = pins.Where(o => LinkCount(composite, animation, o) == 0 && !McpScript.ParamName(o).StartsWith("reverse_")).Select(McpScript.ParamName).ToList();
            if (silent.Count != 0)
                notes.Add("Event pins nothing is linked to (they fire, but nothing listens): " + string.Join(", ", silent.Take(12)) + (silent.Count > 12 ? ", ..." : "") + ".");

            //Cameras: switched to, and handed back
            JArray cameraRows = new JArray();
            foreach ((Composite c, Entity e, string how) in cameras)
            {
                string cameraName = McpScript.EntityName(commands, c, e);
                List<(ShortGuid pin, string from)> cameraCalls = CallsFromElsewhere(commands, c, e, ActivateCameraPin, DeactivateCameraPin);
                List<string> activated = LinksInto(commands, c, e, ActivateCameraPin).Concat(cameraCalls.Where(o => o.pin == ActivateCameraPin).Select(o => o.from)).ToList();
                List<string> released = LinksInto(commands, c, e, DeactivateCameraPin).Concat(cameraCalls.Where(o => o.pin == DeactivateCameraPin).Select(o => o.from)).ToList();
                cameraRows.Add(new JObject() { ["camera"] = cameraName, ["composite"] = c.name, ["how"] = how, ["activated_by"] = new JArray(activated), ["deactivated_by"] = new JArray(released) });
                if (how.StartsWith("moved") && activated.Count == 0)
                    Problem("camera_never_activated", "blocker", cameraName + " is moved by the animation but nothing calls its activate_camera, so the view never switches to it.", "Link the trigger that starts the animation to " + cameraName + ".activate_camera (add_links" + (c != composite ? ", in " + c.name : "") + ").");
                if (how.StartsWith("moved") && released.Count == 0 && !loops)
                    notes.Add(cameraName + ": nothing calls deactivate_camera, so the view stays on it after the animation ends (fine for a benchmark or held shot; link " + name + ".animation_finished -> deactivate_camera to hand control back).");
                List<string> others = AnimatedBy(commands, c, e).OfType<JObject>()
                    .Where(o => o["drives"] != null && !((string)o["composite"] == composite.name && (string)o["animation"]?["id"] == McpScript.Id(animation.shortGUID)))
                    .Select(o => (string)o["animation"]?["name"] + " in " + (string)o["composite"]).ToList();
                if (others.Count != 0)
                    notes.Add(cameraName + " is moved by other animations too (" + string.Join(", ", others.Take(5)) + "): whichever plays last sets where it is.");
            }
            if (cameraRows.Count != 0) result["cameras"] = cameraRows;

            //Placed in the level, and streamed in by which zones
            Composite root = commands.EntryPoints[0];
            if (composite != root)
            {
                McpPlacements walker = McpRegion.Walker(commands);
                List<McpPlacements.Placement> found = new List<McpPlacements.Placement>();
                int total = walker.PlacementsOf(root, composite, null, found, 50, default(System.Threading.CancellationToken), realOnly: true);
                JObject placed = new JObject() { ["count"] = total };
                if (total == 0)
                    Problem("not_placed", "blocker", composite.name + " is not placed in the level (or only as a template or deleted placement), so nothing in it runs.", "Place an instance of it (create_entities with the composite) or move the animation into a placed composite.");
                else
                {
                    List<SyncedZone> zones = McpRegion.Zones(commands);
                    JArray rows = new JArray();
                    foreach (McpPlacements.Placement placement in found.Take(5))
                    {
                        List<uint> ids = placement.Chain.Select(o => o.shortGUID.AsUInt32).ToList();
                        //A zone listing this placement (or one above it) streams the animation in; one listing only things inside it streams those, not it
                        List<string> streaming = zones.Where(z => z.roots.Any(r => r != null && r.Count != 0 && r.Count <= ids.Count && !r.Where((id, i) => id != ids[i]).Any())).Select(z => McpRegion.ZoneName(commands, z)).Distinct().ToList();
                        List<string> inside = zones.Where(z => z.roots.Any(r => r != null && r.Count > ids.Count && !ids.Where((id, i) => id != r[i]).Any())).Select(z => McpRegion.ZoneName(commands, z)).Distinct().Except(streaming).ToList();
                        JObject row = new JObject() { ["path"] = McpRegion.DescribeChain(commands, placement.Chain), ["zones"] = streaming.Count == 0 ? (JToken)"none: no zone lists it" : new JArray(streaming) };
                        if (inside.Count != 0) row["its_contents_stream_with"] = new JArray(inside.Take(5));
                        rows.Add(row);
                    }
                    placed["placements"] = rows;
                    if (total > 1) notes.Add(composite.name + " is placed " + total + " times: each placement plays its own copy of the animation when started.");
                }
                result["placement"] = placed;
            }
            else
                result["placement"] = new JObject() { ["count"] = 1, ["note"] = "the level's root composite (always loaded)" };

            string verdict = problems.Any(o => (string)o["severity"] == "blocker") ? "will_not_play" : problems.Count != 0 ? "check" : "plays";
            JObject answer = new JObject() { ["verdict"] = verdict };
            foreach (JProperty property in result.Properties()) answer[property.Name] = property.Value;
            answer["problems"] = problems;
            if (notes.Count != 0) answer["notes"] = new JArray(notes);
            answer["judged_from"] = "the script as it is in the editor (unsaved changes included); the game plays what was last saved";
            return answer;
        }
        #endregion

        #region The working copy
        /// <summary>
        /// A CAGEAnimation being changed by a tool. Its track lists are fresh deep copies, as the editor's
        /// CommitLive takes them, so nothing the undo step keeps as "before" is ever written to; they are
        /// installed together as one <see cref="CageAnimationEdit"/>, which is also what tells an open editor
        /// window and the inspector to take them.
        /// </summary>
        internal sealed class Working
        {
            public readonly Commands Commands;
            public readonly Composite Composite;
            public readonly CAGEAnimation Animation;
            public readonly CageAnimationEdit.Lists Before;
            public readonly List<CAGEAnimation.Connection> Connections;
            public readonly List<CAGEAnimation.FloatTrack> FloatTracks;
            public readonly List<CAGEAnimation.EventTrack> EventTracks;
            public readonly float OriginalLength;
            /// <summary>The animation had no float keys and no events, and its anim_length was its type's default: the first keys set the length.</summary>
            public readonly bool Fresh;
            public CAGEAnimation.InterpolationMode Mode;
            public float? Length;
            public bool Changed;
            public float FinalLength;
            public readonly List<string> Changes = new List<string>();
            /// <summary>Things the caller should be told (the length decided, a hold at the end...).</summary>
            public readonly List<string> Notes = new List<string>();
            /// <summary>Tracks this call made, to seed at their resting value if nothing keys them.</summary>
            public readonly Dictionary<CAGEAnimation.FloatTrack, float> Created = new Dictionary<CAGEAnimation.FloatTrack, float>();
            public readonly HashSet<CAGEAnimation.FloatTrack> Touched = new HashSet<CAGEAnimation.FloatTrack>();
            /// <summary>Tracks whose keys were replaced outright: they hold exactly what was given.</summary>
            public readonly HashSet<CAGEAnimation.FloatTrack> Exact = new HashSet<CAGEAnimation.FloatTrack>();
            /// <summary>The viewport camera's pose, read before the edit, for keys taken from it.</summary>
            public McpViewportTools.ViewportCamera Viewport;
            private bool _seeded;

            public Working(Commands commands, Composite composite, CAGEAnimation animation)
            {
                Commands = commands;
                Composite = composite;
                Animation = animation;
                Before = CageAnimationEdit.Lists.Of(animation);
                Connections = animation.connections.Copy() ?? new List<CAGEAnimation.Connection>();
                FloatTracks = animation.floatTracks.Copy() ?? new List<CAGEAnimation.FloatTrack>();
                EventTracks = animation.eventTracks.Copy() ?? new List<CAGEAnimation.EventTrack>();
                //Keys older editor builds made carry value.X = 1; the editor straightens them on open, and so does this
                foreach (CAGEAnimation.FloatTrack track in FloatTracks)
                    CageAnimationCurves.NormaliseKeyframes(track);
                OriginalLength = EffectiveLength(animation);
                Mode = InterpolationOf(FloatTracks);
                float? stored = StoredLength(animation);
                Fresh = !FloatTracks.Any(o => o?.keyframes != null && o.keyframes.Count != 0)
                    && !EventTracks.Any(o => o?.keyframes != null && o.keyframes.Count != 0)
                    && (stored == null || Math.Abs(stored.Value - DefaultLength) < 1e-4f);
            }

            public CAGEAnimation.FloatTrack FloatTrack(ShortGuid id) => FloatTracks.FirstOrDefault(o => o != null && o.shortGUID == id);

            /// <summary>
            /// A new track starts from the resting value, as the editor seeds one (AddNewConnectionSet): with nothing keyed on it,
            /// one flat key at the start (it holds the rest value for the whole animation, so shortening the animation later
            /// is never blocked by a seed key at its end); with keys, one at the start unless they have it. Done before tangents
            /// are clamped, so the key after a seed is sized to its new segment.
            /// </summary>
            public void Seed()
            {
                if (_seeded) return;
                _seeded = true;
                foreach (KeyValuePair<CAGEAnimation.FloatTrack, float> made in Created)
                {
                    if (!FloatTracks.Contains(made.Key)) continue;
                    List<CAGEAnimation.FloatTrack.Keyframe> keys = made.Key.keyframes;
                    if (keys.Count == 0)
                    {
                        keys.Add(CageAnimationCurves.NewKeyframe(0f, made.Value, Mode));
                        Changed = true;
                        continue;
                    }
                    if (Exact.Contains(made.Key) || keys.Any(o => o.time <= Eps)) continue;
                    CAGEAnimation.FloatTrack.Keyframe start = CageAnimationCurves.NewKeyframe(0f, made.Value, Mode);
                    keys.Insert(0, start);
                    SetTangents(made.Key, start, "flat", true, true);
                    //The key after it was sized to a segment that has just changed: give its incoming tangent the new one, keeping its slope
                    if (keys.Count > 1)
                        Resize(keys[1], true, keys[1].time - start.time);
                    Touched.Add(made.Key);
                    Changed = true;
                }
            }

            /// <summary>Install the copies as one undo step. False when there was nothing to change.</summary>
            public bool Commit(string label)
            {
                Seed();
                float latest = Latest(FloatTracks, EventTracks);
                float length;
                if (Length.HasValue)
                {
                    if (latest > Length.Value + Eps)
                        throw new McpError("'length' (" + R(Length.Value) + " s) is shorter than the last key or event (" + R(latest) + " s). Move or remove those first, or leave 'length' out.");
                    length = Length.Value;
                }
                else if (Fresh && Changed && latest > Eps)
                {
                    //A new animation's anim_length is its type's default of 10 s: the keys it is given decide it instead
                    length = latest;
                    Notes.Add("anim_length set to " + R(latest) + " s, the last key (a new CAGEAnimation starts at " + DefaultLength + " s); pass 'length' to hold the last pose longer.");
                }
                else
                {
                    length = Math.Max(OriginalLength, latest);
                    if (Changed && latest > Eps && length > latest + 0.05f && FloatTracks.Any(o => o?.keyframes != null && o.keyframes.Count != 0))
                        Notes.Add("anim_length is " + R(length) + " s but the last key is at " + R(latest) + " s: the animation holds its last pose for " + R(length - latest) + " s before animation_finished. Pass 'length' to change that.");
                }
                FinalLength = length;

                foreach (CAGEAnimation.FloatTrack track in Touched)
                    if (FloatTracks.Contains(track) && track.keyframes.Count == 0)
                        throw new McpError("That would leave float track " + McpScript.Id(track.shortGUID) + " with no keys. To stop animating it, drop it with 'remove' instead.");

                Parameter stored = Animation.GetParameter(AnimLength);
                bool lengthDiffers = !(stored?.content is cFloat current) || Math.Abs(current.value - length) > 1e-6f;
                if (!Changed && !(Length.HasValue && lengthDiffers))
                    return false;

                //anim_length as a new Parameter in a new list: the undo step keeps the old list and its objects
                List<Parameter> parameters = new List<Parameter>(Animation.parameters);
                Parameter written = new Parameter(AnimLength, new cFloat(length), stored?.variant ?? ParameterVariant.PARAMETER);
                int at = stored == null ? -1 : parameters.IndexOf(stored);
                if (at >= 0) parameters[at] = written;
                else parameters.Add(written);

                CageAnimationEdit.Lists after = new CageAnimationEdit.Lists()
                {
                    Connections = Connections,
                    EventTracks = EventTracks,
                    FloatTracks = FloatTracks,
                    Parameters = parameters,
                };
                UndoStack.Current.Apply(new CageAnimationEdit(Composite, Animation, Before, after, "AI: " + label));
                return true;
            }
        }

        private static Working Begin(McpCall call)
        {
            Commands commands = McpEditor.RequireCommands();
            McpEditor.RequireUndoIdle();
            CAGEAnimation animation = FindAnimation(commands, call, out Composite composite);
            return new Working(commands, composite, animation);
        }

        /// <summary>A working copy of an animation for another tool (the camera tools). UI thread.</summary>
        internal static Working BeginOn(Commands commands, Composite composite, CAGEAnimation animation)
        {
            McpEditor.RequireUndoIdle();
            return new Working(commands, composite, animation);
        }

        private static void CheckFields(JObject item, string where, params string[] allowed)
        {
            List<string> unknown = item.Properties().Select(o => o.Name).Where(o => !allowed.Contains(o)).ToList();
            if (unknown.Count != 0)
                throw new McpError("Unknown field" + (unknown.Count > 1 ? "s " : " ") + string.Join(", ", unknown.Select(o => "'" + o + "'")) + " in " + where + ". It takes: " + string.Join(", ", allowed) + ".");
        }

        private static JObject ItemAt(JToken token, string where)
        {
            if (token is JObject item) return item;
            throw new McpError(where + " must be an object.");
        }

        private static float ReadNumber(JToken token, string where)
        {
            double value = McpValues.ReadDouble(token, where);
            if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > 1e7)
                throw new McpError("'" + where + "' must be a finite number.");
            return (float)value;
        }

        private static float ReadTime(JToken token, string where)
        {
            if (token == null || token.Type == JTokenType.Null)
                throw new McpError("'" + where + "' (a time in seconds) is missing.");
            float time = ReadNumber(token, where);
            if (time < 0f)
                throw new McpError("'" + where + "' cannot be negative (times run from 0).");
            return time;
        }

        private static bool ReadFlag(JToken token, string where) => token != null && token.Type != JTokenType.Null && McpValues.ReadBool(token, where);

        private static string Times(CAGEAnimation.FloatTrack track)
        {
            List<string> times = track.keyframes.OrderBy(o => o.time).Select(o => R(o.time).ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
            return times.Count == 0 ? "none" : string.Join(", ", times.Take(30)) + (times.Count > 30 ? ", ..." : "");
        }
        #endregion

        #region animate_parameters
        private struct KeySpec
        {
            public float Time;
            public float Value;
            public Vector2? TanIn;
            public Vector2? TanOut;
        }

        /// <summary>What one 'set' entry writes to: one track, or the component tracks of a whole transform.</summary>
        private sealed class Channel
        {
            public string Label;
            public CAGEAnimation.FloatTrack Track;
            public Dictionary<string, CAGEAnimation.FloatTrack> Transform;
            public Target Target;
            public ShortGuid Parameter;
            /// <summary>Resting values by component ("" for a FLOAT).</summary>
            public Dictionary<string, float> Rest = new Dictionary<string, float>();
            /// <summary>The transform component a single track drives (x ... Roll); null for a FLOAT or a whole transform.</summary>
            public string Component;
            public List<string> Made = new List<string>();
        }

        private static readonly string[] TangentPresets = { "flat", "smooth", "auto", "cyclic", "straight" };

        private static bool UsesViewportCamera(JToken set)
        {
            if (!(set is JObject spec) || !(spec["keys"] is JArray keys)) return false;
            return keys.OfType<JObject>().Any(o => o["from"] != null && o["from"].Type == JTokenType.String && string.Equals(((string)o["from"]).Trim(), "viewport_camera", StringComparison.OrdinalIgnoreCase));
        }

        private static object AnimateParameters(McpCall call)
        {
            JArray sets = call.Array("set");
            JArray removes = call.Array("remove");
            JObject retime = call.Has("retime") ? call.Object("retime") : null;
            if (sets.Count == 0 && removes.Count == 0 && retime == null && !call.Has("length") && !call.Has("interpolation"))
                throw new McpError("Nothing to do: give 'set', 'remove', 'retime', 'length' or 'interpolation'.");

            //A key from the viewport camera's pose: asked of the viewer first (that waits for the viewport, off the UI thread)
            McpViewportTools.ViewportCamera viewport = sets.Any(UsesViewportCamera) ? McpViewportTools.QueryCamera(call) : null;

            List<string> notes = new List<string>();
            return McpEditor.UI(() =>
            {
                Working w = Begin(call);
                w.Viewport = viewport;
                if (call.Has("interpolation"))
                {
                    string wanted = call.Str("interpolation").Trim().ToLowerInvariant();
                    if (wanted != "linear" && wanted != "bezier")
                        throw new McpError("'interpolation' is linear or bezier.");
                    w.Mode = wanted == "bezier" ? CAGEAnimation.InterpolationMode.Bezier : CAGEAnimation.InterpolationMode.Linear;
                }
                if (call.Has("length"))
                {
                    float length = ReadNumber(call.Token("length"), "length");
                    if (length <= 0f)
                        throw new McpError("'length' must be more than 0 seconds.");
                    w.Length = length;
                }

                if (retime != null)
                    Retime(w, retime, notes);
                for (int i = 0; i < removes.Count; i++)
                    RemoveTracks(w, ItemAt(removes[i], "remove[" + i + "]"), "remove[" + i + "]");
                for (int i = 0; i < sets.Count; i++)
                    ApplySet(w, ItemAt(sets[i], "set[" + i + "]"), "set[" + i + "]", notes);

                //The 'Bezier curves' box: every key of every track takes the one mode
                if (call.Has("interpolation"))
                {
                    int switched = 0;
                    foreach (CAGEAnimation.FloatTrack track in w.FloatTracks)
                        foreach (CAGEAnimation.FloatTrack.Keyframe key in track.keyframes)
                            if (key.mode != w.Mode) { key.mode = w.Mode; switched++; }
                    if (switched != 0)
                    {
                        w.Changed = true;
                        w.Changes.Add("interpolation " + w.Mode.ToString().ToLowerInvariant() + " (" + switched + " keys)");
                    }
                }

                //Seed keys go in before tangents are checked, so the key after a seed is sized to its new segment
                w.Seed();
                ClampTangents(w, notes);
                string label = "Animate " + McpScript.EntityName(w.Commands, w.Composite, w.Animation);
                bool changed = w.Commit(label);
                if (changed && call.Has("length"))
                    w.Changes.Add("length " + R(w.FinalLength) + " s");
                if (changed)
                    McpScriptEdit.Show(w.Composite, new List<Entity>() { w.Animation });

                JObject result = Describe(w.Commands, w.Composite, w.Animation, new DescribeOptions()
                {
                    MaxKeys = 60,
                    MaxTracks = 150,
                    KeysFor = new HashSet<ShortGuid>(w.Touched.Where(o => w.FloatTracks.Contains(o)).Select(o => o.shortGUID)),
                });
                JObject answer = new JObject() { ["changed"] = changed };
                if (changed && w.Changes.Count != 0) answer["changes"] = new JArray(w.Changes);
                foreach (JProperty property in result.Properties().ToList())
                    answer[property.Name] = property.Value;
                notes.AddRange(PrecedentNotes(w));
                foreach (string note in w.Notes.Concat(notes)) call.Note(note);
                if (!changed) call.Note("The animation already held all of that, so no undo step was made.");
                return answer;
            });
        }

        /// <summary>A note for each target parameter this call animated that no retail animation drives.</summary>
        private static List<string> PrecedentNotes(Working w)
        {
            List<string> notes = new List<string>();
            HashSet<string> said = new HashSet<string>();
            foreach (CAGEAnimation.FloatTrack track in w.Touched)
            {
                if (!w.FloatTracks.Contains(track)) continue;
                CAGEAnimation.Connection connection = w.Connections.FirstOrDefault(o => o != null && o.target_track == track.shortGUID);
                if (connection == null) continue;
                (Composite _, Entity e) = w.Commands.Utils.GetResolvedTarget(w.Commands.Utils.ResolveEntityPath(connection.connectedEntity, w.Composite));
                string kind = KindOf(e);
                string parameter = McpScript.ParamName(connection.target_param);
                if (!said.Add(kind + "." + parameter)) continue;
                string precedent = PrecedentOf(kind, parameter);
                if (precedent != null && !precedent.StartsWith("rare"))
                    notes.Add(PrecedentNote(TargetLabel(w.Commands, w.Composite, connection.connectedEntity) + "." + parameter, precedent));
            }
            return notes;
        }

        /// <summary>
        /// retime: shift, scale, reverse or ping-pong the keys of every float track (and the events) of the working copy,
        /// tangents with them, and set the length to match.
        /// </summary>
        private static void Retime(Working w, JObject spec, List<string> notes)
        {
            CheckFields(spec, "retime", "shift", "scale", "reverse", "ping_pong", "from", "to", "include_events");
            bool reverse = ReadFlag(spec["reverse"], "retime.reverse");
            bool pingPong = ReadFlag(spec["ping_pong"], "retime.ping_pong");
            float? shift = spec["shift"] != null && spec["shift"].Type != JTokenType.Null ? ReadNumber(spec["shift"], "retime.shift") : (float?)null;
            float? scale = spec["scale"] != null && spec["scale"].Type != JTokenType.Null ? ReadNumber(spec["scale"], "retime.scale") : (float?)null;
            int ops = (reverse ? 1 : 0) + (pingPong ? 1 : 0) + (shift != null ? 1 : 0) + (scale != null ? 1 : 0);
            if (ops != 1)
                throw new McpError("'retime' takes one of shift, scale, reverse or ping_pong per call" + (ops == 0 ? "" : " (call again for the next)") + ".");
            bool events = spec["include_events"] == null || spec["include_events"].Type == JTokenType.Null || McpValues.ReadBool(spec["include_events"], "retime.include_events");
            float length = Math.Max(w.OriginalLength, Latest(w.FloatTracks, w.EventTracks));
            float from = spec["from"] != null && spec["from"].Type != JTokenType.Null ? ReadTime(spec["from"], "retime.from") : 0f;
            float to = spec["to"] != null && spec["to"].Type != JTokenType.Null ? ReadTime(spec["to"], "retime.to") : length;
            if (pingPong && (spec["from"] != null || spec["to"] != null))
                throw new McpError("'retime.ping_pong' mirrors the whole animation: leave out from/to (reverse takes a range).");
            if ((reverse || scale != null) && to <= from + Eps)
                throw new McpError("'retime.to' (" + R(to) + " s) must be after 'from' (" + R(from) + " s).");
            if (length <= Eps)
                throw new McpError("The animation has no keys or events to retime.");

            List<CAGEAnimation.FloatTrack.Keyframe> all = w.FloatTracks.Where(o => o?.keyframes != null).SelectMany(o => o.keyframes).ToList();
            List<CAGEAnimation.EventTrack.Keyframe> allEvents = events ? w.EventTracks.Where(o => o?.keyframes != null).SelectMany(o => o.keyframes).Where(o => o != null).ToList() : new List<CAGEAnimation.EventTrack.Keyframe>();
            float newLength = length;
            string summary;

            if (shift != null)
            {
                float by = shift.Value;
                if (Math.Abs(by) <= Eps) throw new McpError("'retime.shift' is 0: nothing would move.");
                if (by < 0f)
                {
                    //Pulling keys earlier must not land them on or before keys that stay put
                    foreach (CAGEAnimation.FloatTrack track in w.FloatTracks.Where(o => o?.keyframes != null))
                    {
                        List<CAGEAnimation.FloatTrack.Keyframe> keys = track.keyframes;
                        float firstMoving = keys.Where(o => o.time >= from - Eps).Select(o => o.time).DefaultIfEmpty(float.NaN).Min();
                        if (float.IsNaN(firstMoving)) continue;
                        float lastStaying = keys.Where(o => o.time < from - Eps).Select(o => o.time).DefaultIfEmpty(float.NaN).Max();
                        if (firstMoving + by < -Eps || (!float.IsNaN(lastStaying) && firstMoving + by <= lastStaying + Eps))
                            throw new McpError("Shifting by " + R(by) + " s would move the key at " + R(firstMoving) + " s onto or past " + (float.IsNaN(lastStaying) ? "0 s" : "the key at " + R(lastStaying) + " s") + " on track " + McpScript.Id(track.shortGUID) + ". Remove those keys first, or shift less.");
                    }
                    if (allEvents.Any(o => o.time >= from - Eps && o.time + by < -Eps))
                        throw new McpError("Shifting by " + R(by) + " s would move an event before 0 s.");
                }
                foreach (CAGEAnimation.FloatTrack.Keyframe key in all)
                    if (key.time >= from - Eps) CageAnimationCurves.SetTime(key, key.time + by);
                foreach (CAGEAnimation.EventTrack.Keyframe key in allEvents)
                    if (key.time >= from - Eps) key.time = Math.Max(0f, key.time + by);
                newLength = Math.Max(0.001f, length + by);
                summary = "shifted everything from " + R(from) + " s by " + R(by) + " s";
            }
            else if (scale != null)
            {
                float s = scale.Value;
                if (s <= 0.001f || s > 1000f) throw new McpError("'retime.scale' is a factor more than 0 (2 = twice as long, 0.5 = twice as fast).");
                Func<float, float> map = t => t < from - Eps ? t : t <= to + Eps ? from + (t - from) * s : t + (to - from) * (s - 1f);
                foreach (CAGEAnimation.FloatTrack.Keyframe key in all)
                {
                    bool inside = key.time > from + Eps && key.time < to - Eps;
                    bool atFrom = Math.Abs(key.time - from) <= Eps, atTo = Math.Abs(key.time - to) <= Eps;
                    //Time offsets of the control points stretch with the time; their values stay, so the shape is stretched exactly
                    if (inside || atTo) key.tan_in = new Vector2(key.tan_in.X * s, key.tan_in.Y);
                    if (inside || atFrom) key.tan_out = new Vector2(key.tan_out.X * s, key.tan_out.Y);
                    CageAnimationCurves.SetTime(key, map(key.time));
                }
                foreach (CAGEAnimation.EventTrack.Keyframe key in allEvents)
                {
                    if (key.time > from - Eps && key.time < to - Eps) key.duration *= s;
                    key.time = map(key.time);
                }
                newLength = map(length);
                summary = "scaled " + R(from) + "-" + R(to) + " s by " + R(s);
            }
            else if (reverse)
            {
                foreach (CAGEAnimation.FloatTrack.Keyframe key in all)
                {
                    if (key.time < from - Eps || key.time > to + Eps) continue;
                    Mirror(key, from + to);
                }
                foreach (CAGEAnimation.EventTrack.Keyframe key in allEvents)
                    if (key.time >= from - Eps && key.time <= to + Eps) key.time = Math.Max(from, from + to - key.time - key.duration);
                summary = "reversed " + R(from) + "-" + R(to) + " s";
                bool jumps = w.FloatTracks.Any(o => o?.keyframes != null && o.keyframes.Count > 1
                    && (o.keyframes.Any(k => k.time < from - Eps) || o.keyframes.Any(k => k.time > to + Eps))
                    && Math.Abs(CageAnimationCurves.ValueAt(o, from, true) - CageAnimationCurves.ValueAt(o, to, true)) > 1e-3f);
                if (jumps) notes.Add("Some tracks have keys outside the reversed range and different values at its two ends, so they jump at the range's edges now.");
            }
            else
            {
                //Ping-pong: everything before the end, mirrored after it, so it comes back the way it went
                foreach (CAGEAnimation.FloatTrack track in w.FloatTracks.Where(o => o?.keyframes != null))
                {
                    List<CAGEAnimation.FloatTrack.Keyframe> copies = new List<CAGEAnimation.FloatTrack.Keyframe>();
                    foreach (CAGEAnimation.FloatTrack.Keyframe key in track.keyframes)
                    {
                        if (key.time >= length - Eps) continue;
                        CAGEAnimation.FloatTrack.Keyframe copy = CageAnimationCurves.NewKeyframe(key.time, key.value.Y, key.mode);
                        copy.tan_in = key.tan_in;
                        copy.tan_out = key.tan_out;
                        Mirror(copy, 2f * length);
                        copies.Add(copy);
                    }
                    //A track whose last key is short of the end turns round on a key at the end
                    if (!track.keyframes.Any(o => o.time >= length - Eps) && track.keyframes.Count != 0)
                    {
                        CAGEAnimation.FloatTrack.Keyframe turn = CageAnimationCurves.NewKeyframe(length, CageAnimationCurves.ValueAt(track, length, w.Mode == CAGEAnimation.InterpolationMode.Bezier), w.Mode);
                        copies.Add(turn);
                    }
                    track.keyframes.AddRange(copies);
                }
                if (events)
                    foreach (CAGEAnimation.EventTrack track in w.EventTracks.Where(o => o?.keyframes != null))
                        foreach (CAGEAnimation.EventTrack.Keyframe key in track.keyframes.Where(o => o != null && o.time < length - Eps).ToList())
                            track.keyframes.Add(new CAGEAnimation.EventTrack.Keyframe() { time = 2f * length - key.time - key.duration, forward = key.forward, reverse = key.reverse, track_type = key.track_type, duration = key.duration });
                newLength = 2f * length;
                summary = "ping-pong: " + R(length) + " s there and " + R(length) + " s back";
                notes.Add("To go back and forth forever, link this animation's animation_finished to its own start (add_links).");
            }

            foreach (CAGEAnimation.FloatTrack track in w.FloatTracks.Where(o => o?.keyframes != null))
            {
                track.keyframes.Sort((a, b) => a.time.CompareTo(b.time));
                for (int i = 1; i < track.keyframes.Count; i++)
                    if (track.keyframes[i].time - track.keyframes[i - 1].time <= Eps)
                        throw new McpError("Retiming puts two keys of track " + McpScript.Id(track.shortGUID) + " at " + R(track.keyframes[i].time) + " s.");
                w.Touched.Add(track);
            }
            foreach (CAGEAnimation.EventTrack track in w.EventTracks.Where(o => o?.keyframes != null))
                track.keyframes = track.keyframes.OrderBy(o => o.time).ToList();
            if (!w.Length.HasValue)
                w.Length = Math.Max(newLength, Latest(w.FloatTracks, w.EventTracks));
            w.Changed = true;
            w.Changes.Add(summary + (events ? "" : " (events left where they were)") + "; length " + R(w.Length.Value) + " s");
        }

        /// <summary>A key mirrored in time about <paramref name="sum"/>/2 (t becomes sum - t): its tangents swap sides, their slopes negated.</summary>
        private static void Mirror(CAGEAnimation.FloatTrack.Keyframe key, float sum)
        {
            Vector2 tanIn = key.tan_in, tanOut = key.tan_out;
            key.tan_in = new Vector2(tanOut.X, -tanOut.Y);
            key.tan_out = new Vector2(tanIn.X, -tanIn.Y);
            CageAnimationCurves.SetTime(key, sum - key.time);
        }

        private static string CanonicalComponent(string text, string where)
        {
            string wanted = (text ?? "").Trim();
            string lower = wanted.ToLowerInvariant();
            switch (lower)
            {
                case "position.x": case "px": return "x";
                case "position.y": case "py": return "y";
                case "position.z": case "pz": return "z";
                case "rotation.x": return "Pitch";
                case "rotation.y": return "Yaw";
                case "rotation.z": return "Roll";
            }
            foreach (string component in Components)
                if (string.Equals(component, wanted, StringComparison.OrdinalIgnoreCase)) return component;
            throw new McpError("'" + where + "' is one of x, y, z, Yaw, Pitch, Roll (rotation x is Pitch, y is Yaw, z is Roll).");
        }

        /// <summary>The data type a parameter of an entity holds: its value's, else its type's declared one.</summary>
        private static DataType? ParameterType(Commands commands, Composite composite, Entity entity, ShortGuid parameter)
        {
            ParameterData content = entity.GetParameter(parameter)?.content;
            if (content != null) return content.dataType;
            try
            {
                (ParameterVariant? _, DataType? type, ShortGuid __) = commands.Utils.GetParameterMetadata(entity, parameter, composite);
                return type;
            }
            catch
            {
                return null;
            }
        }

        private static ParameterData RestingContent(Commands commands, Composite composite, Entity entity, ShortGuid parameter)
        {
            ParameterData content = entity.GetParameter(parameter)?.content;
            if (content != null) return content;
            try { return commands.Utils.CreateDefaultParameterData(entity, composite, parameter); } catch { return null; }
        }

        private static string AnimatableHint(Commands commands, Composite composite, Entity entity)
        {
            List<string> names = new List<string>();
            foreach (Parameter parameter in entity.parameters)
                if (parameter.content is cFloat || parameter.content is cTransform) names.Add(McpScript.ParamName(parameter.name));
            try
            {
                foreach ((ShortGuid name, ParameterVariant variant, DataType type) in commands.Utils.GetAllParameters(entity, composite))
                    if (type == DataType.FLOAT || type == DataType.TRANSFORM) names.Add(McpScript.ParamName(name));
            }
            catch { }
            names = names.Distinct().ToList();
            return names.Count == 0 ? " It has no FLOAT or TRANSFORM parameters to animate." : " Parameters it has that can be animated: " + string.Join(", ", names.Take(40)) + (names.Count > 40 ? ", ..." : "") + ".";
        }

        private static CAGEAnimation.FloatTrack FindTrack(Working w, Target target, ShortGuid parameter, string component)
        {
            foreach (CAGEAnimation.Connection connection in w.Connections)
            {
                if (connection == null || connection.target_param != parameter || !string.Equals(ComponentName(connection.target_sub_param), component, StringComparison.OrdinalIgnoreCase)) continue;
                CAGEAnimation.FloatTrack track = w.FloatTrack(connection.target_track);
                if (track == null) continue;
                if (SameTarget(w.Commands, w.Composite, connection.connectedEntity, target)) return track;
            }
            return null;
        }

        /// <summary>A new connection and float track, as the editor's AddNewConnectionSet makes them (the keys come later).</summary>
        private static CAGEAnimation.FloatTrack AddTrack(Working w, Target target, ShortGuid parameter, DataType type, string component, float rest)
        {
            CAGEAnimation.FloatTrack track = new CAGEAnimation.FloatTrack() { shortGUID = ShortGuidUtils.GenerateRandom() };
            w.FloatTracks.Add(track);
            CAGEAnimation.Connection connection = new CAGEAnimation.Connection()
            {
                binding_guid = ShortGuidUtils.GenerateRandom(),
                binding_type = ObjectType.ENTITY,
                target_param = parameter,
                target_param_type = type,
                target_sub_param = ShortGuidUtils.Generate(component),
                target_track = track.shortGUID,
            };
            connection.connectedEntity = new EntityPath((ShortGuid[])target.Path.path.Clone());
            w.Connections.Add(connection);
            w.Created[track] = rest;
            w.Changed = true;
            return track;
        }

        private static Channel ResolveChannel(Working w, JObject spec, string where)
        {
            if (spec["track"] != null)
            {
                if (spec["target"] != null || spec["parameter"] != null || spec["component"] != null || spec["from_root"] != null)
                    throw new McpError(where + ": give 'track', or 'target' and 'parameter' - not both.");
                string reference = (string)spec["track"];
                ShortGuid? id = McpScript.ParseId(reference);
                CAGEAnimation.FloatTrack track = id == null ? null : w.FloatTrack(id.Value);
                if (track == null)
                    throw new McpError(where + ": this animation has no float track '" + reference + "'" + (id != null && w.EventTracks.Any(o => o.shortGUID == id.Value) ? " (that is an event track: set_animation_events edits those)" : "") + ". get_cage_animation lists its tracks.");
                CAGEAnimation.Connection connection = w.Connections.FirstOrDefault(o => o != null && o.target_track == track.shortGUID);
                Channel byId = new Channel() { Track = track, Label = connection == null ? "track " + McpScript.Id(track.shortGUID) : TargetLabel(w.Commands, w.Composite, connection.connectedEntity) + "." + McpScript.ParamName(connection.target_param) + (ComponentName(connection.target_sub_param) == "" ? "" : "." + ComponentName(connection.target_sub_param)) };
                if (connection != null)
                {
                    string sub = ComponentName(connection.target_sub_param);
                    byId.Component = Components.Contains(sub) ? sub : null;
                    float? rest = RestValue(w.Commands, w.Composite, connection);
                    if (rest != null) byId.Rest[byId.Component ?? ""] = rest.Value;
                }
                return byId;
            }

            string parameterName = (string)spec["parameter"];
            if (spec["target"] == null || string.IsNullOrWhiteSpace(parameterName))
                throw new McpError(where + " needs 'target' and 'parameter' (or 'track' for an existing track).");
            Target target = ResolveTarget(w.Commands, w.Composite, spec["target"], ReadFlag(spec["from_root"], where + ".from_root"), where + ".target");
            if (target.Entity is AliasEntity)
                throw new McpError(where + ": " + target.Name + " is an alias. Point the path at the entity it overrides, through its instances.");
            ShortGuid parameter = McpScript.ParamId(parameterName);
            string parameterLabel = McpScript.ParamName(parameter);
            DataType? type = ParameterType(w.Commands, target.Composite, target.Entity, parameter);
            if (type == null)
                throw new McpError(where + ": " + target.Name + " (" + McpScript.TypeName(w.Commands, target.Composite, target.Entity) + ") has no parameter '" + parameterName + "'." + AnimatableHint(w.Commands, target.Composite, target.Entity));
            if (type != DataType.FLOAT && type != DataType.TRANSFORM)
                throw new McpError(where + ": '" + parameterLabel + "' on " + target.Name + " is a " + type + "; a CAGEAnimation can drive FLOAT and TRANSFORM parameters only." + AnimatableHint(w.Commands, target.Composite, target.Entity));

            ParameterData resting = RestingContent(w.Commands, target.Composite, target.Entity, parameter);
            Channel channel = new Channel() { Target = target, Parameter = parameter, Label = target.Name + "." + parameterLabel };
            string component = spec["component"] == null || spec["component"].Type == JTokenType.Null ? null : (string)spec["component"];

            if (type == DataType.FLOAT)
            {
                if (!string.IsNullOrWhiteSpace(component))
                    throw new McpError(where + ": '" + parameterLabel + "' is a FLOAT, which has no components.");
                float rest = ComponentValue(resting, "") ?? 0f;
                channel.Rest[""] = rest;
                channel.Track = FindTrack(w, target, parameter, "");
                if (channel.Track == null)
                {
                    channel.Track = AddTrack(w, target, parameter, DataType.FLOAT, "", rest);
                    channel.Made.Add(parameterLabel);
                }
                return channel;
            }

            //A transform is driven through six tracks; animating it for the first time adds all six, as the editor does
            Dictionary<string, CAGEAnimation.FloatTrack> existing = Components.ToDictionary(o => o, o => FindTrack(w, target, parameter, o));
            foreach (string c in Components)
                channel.Rest[c] = ComponentValue(resting, c) ?? 0f;
            bool none = existing.Values.All(o => o == null);
            if (none)
            {
                foreach (string c in Components)
                    existing[c] = AddTrack(w, target, parameter, DataType.TRANSFORM, c, channel.Rest[c]);
                channel.Made.Add(parameterLabel + " (x, y, z, Yaw, Pitch, Roll)");
            }
            if (!string.IsNullOrWhiteSpace(component))
            {
                string c = CanonicalComponent(component, where + ".component");
                channel.Label += "." + c;
                channel.Component = c;
                channel.Track = existing[c];
                if (channel.Track == null)
                {
                    channel.Track = AddTrack(w, target, parameter, DataType.TRANSFORM, c, channel.Rest[c]);
                    channel.Made.Add(parameterLabel + "." + c);
                }
                return channel;
            }
            channel.Transform = existing;
            return channel;
        }

        private static KeySpec ReadKey(JToken token, string where)
        {
            if (token is JArray pair)
            {
                if (pair.Count != 2)
                    throw new McpError(where + " is [time, value].");
                return new KeySpec() { Time = ReadTime(pair[0], where + "[0]"), Value = ReadNumber(pair[1], where + "[1]") };
            }
            if (token is JObject item)
            {
                if (item["position"] != null || item["rotation"] != null)
                    throw new McpError(where + ": position/rotation keys are for a whole transform; leave out 'component' to key one that way, or give {time, value}.");
                CheckFields(item, where, "time", "value", "tan_in", "tan_out");
                if (item["value"] == null)
                    throw new McpError(where + " needs a 'value'.");
                KeySpec key = new KeySpec() { Time = ReadTime(item["time"], where + ".time"), Value = ReadNumber(item["value"], where + ".value") };
                if (item["tan_in"] != null) key.TanIn = ReadTangent(item["tan_in"], where + ".tan_in");
                if (item["tan_out"] != null) key.TanOut = ReadTangent(item["tan_out"], where + ".tan_out");
                return key;
            }
            throw new McpError(where + " is [time, value] or {time, value, tan_in?, tan_out?}.");
        }

        private static Vector2 ReadTangent(JToken token, string where)
        {
            if (!(token is JArray pair) || pair.Count != 2)
                throw new McpError("'" + where + "' is [seconds, value]: the control point sits a third of it away from the key.");
            float dt = ReadNumber(pair[0], where + "[0]");
            float dv = ReadNumber(pair[1], where + "[1]");
            if (dt < 0f)
                throw new McpError("'" + where + "' cannot have negative seconds (the curve would run backwards in time).");
            return new Vector2(dt, dv);
        }

        /// <summary>A whole-transform key as given: the axes it sets directly, or how its rotation is to be built.</summary>
        private sealed class TransformKey
        {
            public float Time;
            public string Where;
            /// <summary>Component values given directly (x, y, z, Pitch, Yaw, Roll).</summary>
            public readonly Dictionary<string, float> Axes = new Dictionary<string, float>();
            public bool FullPosition, FullRotation;
            public Vector3? LookAt, Forward;
            public float Roll;
            public bool FromViewport;
            /// <summary>A point (in the key's space) the rotation turns the entity about, which stays put: the position is worked out.</summary>
            public Vector3? Pivot;

            public Vector3 Position => new Vector3(Axes["x"], Axes["y"], Axes["z"]);
            public Vector3 Rotation => new Vector3(Axes["Pitch"], Axes["Yaw"], Axes["Roll"]);
        }

        private static readonly string[] PositionAxes = { "x", "y", "z" };
        private static readonly string[] RotationAxes = { "Pitch", "Yaw", "Roll" };

        /// <summary>A whole-transform key: {time, position?, rotation? | look_at? | forward?, roll?} or {time, from: 'viewport_camera'}.</summary>
        private static TransformKey ReadTransformKey(JToken token, string where)
        {
            if (!(token is JObject item))
                throw new McpError(where + ": keys of a whole transform are {time, position?: [x,y,z], rotation?: [pitch,yaw,roll]} (or look_at/forward instead of rotation); to key one component give 'component'.");
            CheckFields(item, where, "time", "position", "rotation", "look_at", "forward", "roll", "from", "pivot");
            TransformKey key = new TransformKey() { Time = ReadTime(item["time"], where + ".time"), Where = where };
            if (item["from"] != null && item["from"].Type != JTokenType.Null)
            {
                string from = McpValues.ReadString(item["from"]).Trim();
                if (!string.Equals(from, "viewport_camera", StringComparison.OrdinalIgnoreCase))
                    throw new McpError("'" + where + ".from' takes 'viewport_camera' (the viewport camera's pose now).");
                if (item["position"] != null || item["rotation"] != null || item["look_at"] != null || item["forward"] != null || item["roll"] != null || item["pivot"] != null)
                    throw new McpError(where + ": a key from the viewport camera takes its whole pose; give only 'time' with it.");
                key.FromViewport = true;
                return key;
            }
            int facing = (item["rotation"] != null ? 1 : 0) + (item["look_at"] != null ? 1 : 0) + (item["forward"] != null ? 1 : 0);
            if (facing > 1)
                throw new McpError(where + ": give one of rotation, look_at or forward.");
            if (item["position"] == null && facing == 0)
                throw new McpError(where + " needs 'position' and/or 'rotation' (or look_at/forward).");
            if (item["roll"] != null && item["look_at"] == null && item["forward"] == null)
                throw new McpError(where + ": 'roll' goes with look_at or forward (a rotation gives its own roll as its z).");
            if (item["position"] != null)
                key.FullPosition = ReadTransformAxes(item["position"], where + ".position", key.Axes, "x", "y", "z");
            if (item["rotation"] != null)
                key.FullRotation = ReadTransformAxes(item["rotation"], where + ".rotation", key.Axes, "Pitch", "Yaw", "Roll");
            if (item["look_at"] != null)
                key.LookAt = McpValues.ReadVector(item["look_at"], where + ".look_at", null);
            if (item["forward"] != null)
            {
                key.Forward = McpValues.ReadVector(item["forward"], where + ".forward", null);
                if (key.Forward.Value.LengthSquared() < 1e-10f)
                    throw new McpError("'" + where + ".forward' is a direction: it cannot be [0, 0, 0].");
            }
            if (item["roll"] != null)
                key.Roll = ReadNumber(item["roll"], where + ".roll");
            if (item["pivot"] != null && item["pivot"].Type != JTokenType.Null)
            {
                if (item["position"] != null)
                    throw new McpError(where + ": 'pivot' works the position out (the entity turns about the pivot from where it rests); leave out 'position'.");
                if (!key.FullRotation && item["look_at"] == null && item["forward"] == null)
                    throw new McpError(where + ": 'pivot' needs the rotation to turn to: rotation [pitch, yaw, roll] (all three), look_at or forward.");
                key.Pivot = McpValues.ReadVector(item["pivot"], where + ".pivot", null);
            }
            return key;
        }

        /// <summary>
        /// [x, y, z], or {x?, y?, z?} keying only the axes it gives (the others are left alone, not keyed at 0), onto the
        /// component names <paramref name="components"/> gives for x, y and z. Each value is checked as a scalar key's is.
        /// True when all three were given.
        /// </summary>
        private static bool ReadTransformAxes(JToken token, string where, Dictionary<string, float> keys, params string[] components)
        {
            string[] axes = { "x", "y", "z" };
            if (token is JArray array)
            {
                if (array.Count != 3)
                    throw new McpError("'" + where + "' takes three numbers [x, y, z].");
                for (int i = 0; i < 3; i++)
                    keys[components[i]] = ReadNumber(array[i], where + "[" + i + "]");
                return true;
            }
            if (token is JObject obj)
            {
                CheckFields(obj, where, axes);
                int given = 0;
                for (int i = 0; i < 3; i++)
                {
                    if (obj[axes[i]] == null) continue;
                    keys[components[i]] = ReadNumber(obj[axes[i]], where + "." + axes[i]);
                    given++;
                }
                if (given == 0)
                    throw new McpError("'" + where + "' gives none of x, y or z.");
                return given == 3;
            }
            throw new McpError("'" + where + "' takes three numbers [x, y, z], or {x?, y?, z?} to key only some of them.");
        }

        /// <summary>What the target holds at a time: its tracks where it has them, its resting value elsewhere (the target's composite space).</summary>
        private static cTransform PoseAt(Working w, Channel channel, float time)
        {
            float[] values = new float[6];
            for (int i = 0; i < Components.Length; i++)
            {
                string c = Components[i];
                CAGEAnimation.FloatTrack track = channel.Transform != null && channel.Transform.TryGetValue(c, out CAGEAnimation.FloatTrack found) ? found : null;
                values[i] = track != null && track.keyframes.Count != 0 ? CageAnimationCurves.ValueAt(track, time, w.Mode == CAGEAnimation.InterpolationMode.Bezier) : channel.Rest[c];
            }
            //Components are x, y, z, Yaw, Pitch, Roll; a rotation is [pitch, yaw, roll]
            return new cTransform(new Vector3(values[0], values[1], values[2]), new Vector3(values[4], values[3], values[5]));
        }

        /// <summary>
        /// The world transform of the space a stored path's target writes its position in: the animation's composite at one of
        /// its placements (or the root, for a path from the root), and the instances the path steps through. UI thread.
        /// </summary>
        internal static cTransform TargetFrame(Commands commands, Composite composite, EntityPath stored, int? placement, string argument, out JObject described)
        {
            List<Tuple<Composite, Entity>> chain = commands.Utils.ResolveEntityPath(stored, composite);
            if (chain == null || chain.Count == 0)
                throw new McpError("The target's path does not resolve from " + composite.name + ", so it has no place in the world.");
            Composite root = commands.EntryPoints[0];
            List<Entity> through = chain.Take(chain.Count - 1).Select(o => o.Item2).ToList();
            List<Entity> prefix = new List<Entity>();
            if (chain[0].Item1 == composite)
            {
                cTransform placed = McpSpatialTools.FrameOf(commands, composite, placement, argument, out described);
                if (through.Count == 0)
                    return placed;
                if (described["placement_path"] is JArray ids)
                {
                    prefix = McpRegion.ChainFromIds(commands, root, ids.Select(o => (McpScript.ParseId((string)o) ?? ShortGuid.Invalid).AsUInt32));
                    if (prefix == null)
                        throw new McpError("The placement of " + composite.name + " could not be walked from the root.");
                }
            }
            else
            {
                described = new JObject() { ["composite"] = root.name, ["note"] = "the path starts in the level's root composite" };
                if (through.Count == 0)
                    return new cTransform(Vector3.Zero, Vector3.Zero);
            }
            List<Entity> full = prefix.Concat(through).ToList();
            cTransform world = McpRegion.Walker(commands).Evaluate(root, full).World ?? new cTransform(Vector3.Zero, Vector3.Zero);
            described["through"] = new JArray(through.Select(o => McpScript.EntityName(commands, chain.First(c => c.Item2 == o).Item1, o)));
            described["frame_position"] = McpCollision.V(world.position);
            described["frame_rotation"] = McpCollision.V(world.rotation);
            return world;
        }

        /// <summary>
        /// The viewport camera's pose (read before the edit) in the space the target's position is written in: from world space
        /// when the viewport shows the level's root, or from the animation's composite when that is what it shows. UI thread.
        /// </summary>
        private static cTransform ViewportPose(Working w, Channel channel, int? placement, string where)
        {
            McpViewportTools.ViewportCamera camera = w.Viewport;
            if (camera == null)
                throw new McpError(where + ": the viewport camera could not be read.");
            cTransform pose = new cTransform(camera.Position, camera.Rotation);
            if (camera.InLevelSpace)
                return InstanceTransform.ToLocal(TargetFrame(w.Commands, w.Composite, channel.Target.Path, placement, "'placement' (on the set entry)", out JObject _), pose);
            if (camera.Scene == w.Composite)
            {
                List<Tuple<Composite, Entity>> chain = channel.Target.Chain;
                if (chain == null || chain.Count == 0 || chain[0].Item1 != w.Composite)
                    throw new McpError(where + ": the target's path starts in the level's root, but the viewport shows " + w.Composite.name + ". Open the level's root composite in the editor so the viewport camera is in world space.");
                List<Entity> through = chain.Take(chain.Count - 1).Select(o => o.Item2).ToList();
                cTransform inner = through.Count == 0 ? null : McpRegion.Walker(w.Commands).Evaluate(w.Composite, through).World;
                return InstanceTransform.ToLocal(inner, pose);
            }
            throw new McpError(where + ": the viewport shows " + (camera.Scene?.name ?? "another composite") + ", which is neither the level's root nor " + w.Composite.name + ", so its camera pose cannot be placed in the animation's space. Open the level's root (or " + w.Composite.name + ") in the editor, then try again.");
        }

        /// <summary>Move each rotation key given here by whole turns to within 180 degrees of the key before it on its track (given or already there).</summary>
        private static int UnwrapKeys(CAGEAnimation.FloatTrack track, List<KeySpec> keys, bool replace)
        {
            int moved = 0;
            List<KeySpec> ordered = keys.OrderBy(o => o.Time).ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                KeySpec key = ordered[i];
                float? previous = null;
                float previousTime = float.MinValue;
                if (i > 0) { previous = ordered[i - 1].Value; previousTime = ordered[i - 1].Time; }
                if (!replace && track != null)
                {
                    CAGEAnimation.FloatTrack.Keyframe before = track.keyframes.Where(o => o.time < key.Time - Eps && o.time > previousTime + Eps && !keys.Any(k => Math.Abs(k.Time - o.time) <= Eps)).OrderBy(o => o.time).LastOrDefault();
                    if (before != null) previous = before.value.Y;
                }
                if (previous == null) continue;
                float unwrapped = InstanceTransform.UnwrapAngle(previous.Value, key.Value);
                if (Math.Abs(unwrapped - key.Value) > 1e-3f) moved++;
                key.Value = unwrapped;
                ordered[i] = key;
            }
            keys.Clear();
            keys.AddRange(ordered);
            return moved;
        }

        private static void ApplySet(Working w, JObject spec, string where, List<string> notes)
        {
            CheckFields(spec, where, "target", "from_root", "track", "parameter", "component", "keys", "replace", "remove_times", "move", "tangents", "space", "placement", "unwrap", "retangent");
            bool replace = ReadFlag(spec["replace"], where + ".replace");
            JArray keyTokens = spec["keys"] == null || spec["keys"].Type == JTokenType.Null ? null : spec["keys"] as JArray ?? throw new McpError("'" + where + ".keys' is a list of keys.");
            List<float> removeTimes = new List<float>();
            if (spec["remove_times"] != null && spec["remove_times"].Type != JTokenType.Null)
            {
                if (!(spec["remove_times"] is JArray times)) throw new McpError("'" + where + ".remove_times' is a list of times.");
                for (int i = 0; i < times.Count; i++) removeTimes.Add(ReadTime(times[i], where + ".remove_times[" + i + "]"));
            }
            List<KeyValuePair<float, float>> moves = new List<KeyValuePair<float, float>>();
            if (spec["move"] != null && spec["move"].Type != JTokenType.Null)
            {
                if (!(spec["move"] is JArray list)) throw new McpError("'" + where + ".move' is a list of {from, to}.");
                for (int i = 0; i < list.Count; i++)
                {
                    JObject move = ItemAt(list[i], where + ".move[" + i + "]");
                    CheckFields(move, where + ".move[" + i + "]", "from", "to");
                    moves.Add(new KeyValuePair<float, float>(ReadTime(move["from"], where + ".move[" + i + "].from"), ReadTime(move["to"], where + ".move[" + i + "].to")));
                }
            }
            string tangents = spec["tangents"] == null || spec["tangents"].Type == JTokenType.Null ? null : ((string)spec["tangents"]).Trim().ToLowerInvariant();
            if (tangents != null && !TangentPresets.Contains(tangents))
                throw new McpError("'" + where + ".tangents' is " + string.Join(", ", TangentPresets.Take(TangentPresets.Length - 1)) + " or " + TangentPresets.Last() + ".");
            bool retangent = ReadFlag(spec["retangent"], where + ".retangent");
            bool unwrap = spec["unwrap"] == null || spec["unwrap"].Type == JTokenType.Null || McpValues.ReadBool(spec["unwrap"], where + ".unwrap");
            string space = spec["space"] == null || spec["space"].Type == JTokenType.Null ? "composite" : ((string)spec["space"]).Trim().ToLowerInvariant();
            if (space == "local") space = "composite";
            if (space != "composite" && space != "world" && space != "relative")
                throw new McpError("'" + where + ".space' is composite (default), world or relative.");
            int? placement = spec["placement"] != null && spec["placement"].Type != JTokenType.Null ? McpValues.ReadInt(spec["placement"], where + ".placement") : (int?)null;
            if (placement != null && space != "world" && !UsesViewportCamera(spec))
                throw new McpError("'" + where + ".placement' goes with space 'world' (or keys from the viewport camera).");
            if (replace && keyTokens == null)
                throw new McpError(where + ": 'replace' needs the 'keys' to replace them with.");
            if (replace && (removeTimes.Count != 0 || moves.Count != 0))
                throw new McpError(where + ": 'replace' rewrites every key; do not combine it with remove_times or move.");

            Channel channel = ResolveChannel(w, spec, where);
            List<string> summary = new List<string>();
            if (channel.Made.Count != 0) summary.Add("animated " + string.Join(", ", channel.Made));

            if (channel.Track != null)
            {
                if (space == "world")
                    throw new McpError(where + ": space 'world' converts whole transforms (positions and rotations together)" + (channel.Component != null ? ": leave out 'component' and key {time, position, rotation}." : "; " + channel.Label + " is a FLOAT, which has no space."));
                string component = channel.Component;
                List<KeySpec> keys = new List<KeySpec>();
                if (keyTokens != null)
                    for (int i = 0; i < keyTokens.Count; i++) keys.Add(ReadKey(keyTokens[i], where + ".keys[" + i + "]"));
                if (space == "relative")
                {
                    float rest = component != null && channel.Rest.TryGetValue(component, out float r) ? r : channel.Rest.TryGetValue("", out float scalar) ? scalar : 0f;
                    for (int i = 0; i < keys.Count; i++) { KeySpec k = keys[i]; k.Value += rest; keys[i] = k; }
                }
                if (unwrap && component != null && RotationAxes.Contains(component))
                {
                    int moved = UnwrapKeys(channel.Track, keys, replace);
                    if (moved != 0) summary.Add("unwrapped " + moved + " angle" + (moved == 1 ? "" : "s"));
                }
                int done = EditTrack(w, channel.Track, channel.Label, keys, replace, removeTimes, moves, tangents, true, where);
                if (retangent) done += Retangent(channel.Track, tangents ?? "smooth");
                if (done != 0) summary.Add(Summary(keys.Count, removeTimes.Count, moves.Count, replace) + (retangent ? (keys.Count != 0 || removeTimes.Count != 0 || moves.Count != 0 ? ", " : "") + "re-tangented" : ""));
            }
            else
            {
                //A whole transform: each key's position and rotation go to their component tracks, converted to the target's space first
                Dictionary<string, List<KeySpec>> perComponent = Components.ToDictionary(o => o, o => new List<KeySpec>());
                int keyCount = 0;
                cTransform frame = null;
                JObject frameDescribed = null;
                if (keyTokens != null)
                {
                    List<TransformKey> given = new List<TransformKey>();
                    for (int i = 0; i < keyTokens.Count; i++)
                        given.Add(ReadTransformKey(keyTokens[i], where + ".keys[" + i + "]"));
                    int added = SubdividePivots(given);
                    if (added != 0) summary.Add("added " + added + " key" + (added == 1 ? "" : "s") + " so swings about a pivot follow the arc");
                    if (space == "world")
                        frame = TargetFrame(w.Commands, w.Composite, channel.Target.Path, placement, "'placement' on " + where, out frameDescribed);
                    foreach (TransformKey key in given)
                    {
                        foreach (KeyValuePair<string, float> part in ResolveTransformKey(w, channel, key, space, frame, placement))
                            perComponent[part.Key].Add(new KeySpec() { Time = key.Time, Value = part.Value });
                        keyCount++;
                    }
                    if (frame != null)
                        summary.Add("from world space" + (frameDescribed?["placement"] != null ? " (placement " + frameDescribed["placement"] + ")" : ""));
                    //A camera keyed from the viewport's pose frames the shot as the viewport does only at the viewport's field of view
                    if (w.Viewport != null && channel.Target?.Entity != null && given.Any(o => o.FromViewport))
                    {
                        Entity camera = StandsFor(w.Commands, channel.Target.Composite, channel.Target.Entity);
                        if (camera != null && KindOf(camera) == "CameraResource")
                        {
                            float fov = (camera.GetParameter(FovParam)?.content as cFloat)?.value ?? 45f;
                            if (Math.Abs(fov - w.Viewport.Fov) > 0.5f)
                                notes.Add(channel.Label + ": the keys from the viewport camera take its pose; the viewport's field of view is " + R(w.Viewport.Fov) + " degrees (vertical) but " + channel.Target.Name + ".fov is " + R(fov) +
                                    ", so in game the shot is framed " + (w.Viewport.Fov > fov ? "tighter" : "wider") + ". set_parameters {fov: " + R(w.Viewport.Fov) + "} on the camera matches the viewport (no retail animation keys fov).");
                        }
                    }
                }
                foreach (string c in Components)
                {
                    if (channel.Transform[c] == null && perComponent[c].Count != 0)
                    {
                        channel.Transform[c] = AddTrack(w, channel.Target, channel.Parameter, DataType.TRANSFORM, c, channel.Rest[c]);
                        summary.Add("animated " + McpScript.ParamName(channel.Parameter) + "." + c);
                    }
                }
                if (unwrap)
                {
                    int moved = 0;
                    foreach (string c in RotationAxes)
                        if (perComponent[c].Count != 0)
                            moved += UnwrapKeys(channel.Transform[c], perComponent[c], replace);
                    if (moved != 0) summary.Add("unwrapped " + moved + " angle" + (moved == 1 ? "" : "s"));
                }
                //Removing or moving a key of a whole transform goes to whichever of its tracks have one there
                foreach (float time in removeTimes)
                    if (!Components.Any(c => channel.Transform[c] != null && CageAnimationCurves.FindKeyframe(channel.Transform[c], time) != null))
                        throw new McpError(where + ": " + channel.Label + " has no key at " + R(time) + " s.");
                foreach (KeyValuePair<float, float> move in moves)
                    if (!Components.Any(c => channel.Transform[c] != null && CageAnimationCurves.FindKeyframe(channel.Transform[c], move.Key) != null))
                        throw new McpError(where + ": " + channel.Label + " has no key at " + R(move.Key) + " s to move.");
                int done = 0;
                foreach (string c in Components)
                {
                    CAGEAnimation.FloatTrack track = channel.Transform[c];
                    if (track == null) continue;
                    bool rewrite = replace && perComponent[c].Count != 0;
                    done += EditTrack(w, track, channel.Label + "." + c, perComponent[c], rewrite, removeTimes, moves, tangents, false, where);
                    if (retangent) done += Retangent(track, tangents ?? "smooth");
                }
                if (done != 0)
                    summary.Add(Summary(keyCount, removeTimes.Count, moves.Count, replace) + (retangent ? (keyCount != 0 || removeTimes.Count != 0 || moves.Count != 0 ? ", " : "") + "re-tangented" : ""));
            }
            if (summary.Count != 0)
                w.Changes.Add(channel.Label + ": " + string.Join("; ", summary));
        }

        /// <summary>
        /// The component values a whole-transform key writes, in the target's composite space: given axes as they are (or plus
        /// the resting value, 'relative'), a rotation built from look_at/forward, a world pose converted, or the viewport camera's pose.
        /// </summary>
        private static Dictionary<string, float> ResolveTransformKey(Working w, Channel channel, TransformKey key, string space, cTransform frame, int? placement)
        {
            Dictionary<string, float> values = new Dictionary<string, float>();
            if (key.FromViewport)
            {
                if (space == "relative")
                    throw new McpError(key.Where + ": a key from the viewport camera is a pose, not an offset: leave out space 'relative'.");
                cTransform pose = ViewportPose(w, channel, placement, key.Where);
                Put(values, pose.position, pose.rotation);
                return values;
            }

            bool partial = (key.Axes.ContainsKey("x") || key.Axes.ContainsKey("y") || key.Axes.ContainsKey("z")) && !key.FullPosition
                || (key.Axes.ContainsKey("Pitch") || key.Axes.ContainsKey("Yaw") || key.Axes.ContainsKey("Roll")) && !key.FullRotation;
            if (partial && space == "world")
                throw new McpError(key.Where + ": world-space keys give all three axes ([x, y, z]); single axes only make sense in the composite's own space.");

            //A rotation facing a point or along a direction, from the key's position (or where the target is at that time)
            Vector3? built = null;
            if (key.LookAt != null || key.Forward != null)
            {
                if (space == "relative")
                    throw new McpError(key.Where + ": look_at/forward give a rotation, not an offset: leave out space 'relative'.");
                Vector3 direction;
                if (key.Forward != null)
                    direction = key.Forward.Value;
                else
                {
                    Vector3 from;
                    if (key.FullPosition) from = key.Position;
                    else
                    {
                        cTransform now = PoseAt(w, channel, key.Time);
                        from = frame != null ? InstanceTransform.PointToWorld(frame, now.position) : now.position;
                    }
                    direction = key.LookAt.Value - from;
                    if (direction.LengthSquared() < 1e-8f)
                        throw new McpError(key.Where + ": look_at is where the key's position is, so there is no direction to face.");
                }
                Vector3 facing = InstanceTransform.LookRotation(direction);
                built = new Vector3(facing.X, facing.Y, key.Roll);
            }

            //Turned about a pivot from where it rests: the position follows the rotation round it
            Vector3? pivoted = null;
            if (key.Pivot != null)
            {
                if (space == "relative")
                    throw new McpError(key.Where + ": a pivot turns the entity from where it rests; leave out space 'relative'.");
                if (!channel.Rest.ContainsKey("Pitch"))
                    throw new McpError(key.Where + ": 'pivot' needs a transform target.");
                cTransform restLocal = new cTransform(new Vector3(channel.Rest["x"], channel.Rest["y"], channel.Rest["z"]), new Vector3(channel.Rest["Pitch"], channel.Rest["Yaw"], channel.Rest["Roll"]));
                cTransform rest = space == "world" ? InstanceTransform.Compose(frame, restLocal) : restLocal;
                pivoted = AboutPivot(rest, key.FullRotation ? key.Rotation : built.Value, key.Pivot.Value);
            }

            if (space == "world")
            {
                Vector3? position = key.FullPosition ? key.Position : pivoted;
                Vector3? rotation = key.FullRotation ? key.Rotation : built;
                if (position != null && rotation != null)
                {
                    cTransform local = InstanceTransform.ToLocal(frame, new cTransform(position.Value, rotation.Value));
                    Put(values, local.position, local.rotation);
                }
                else if (position != null)
                {
                    Vector3 local = InstanceTransform.PointToLocal(frame, position.Value);
                    values["x"] = local.X; values["y"] = local.Y; values["z"] = local.Z;
                }
                else if (rotation != null)
                {
                    Vector3 local = InstanceTransform.ToLocal(new cTransform(Vector3.Zero, frame.rotation), new cTransform(Vector3.Zero, rotation.Value)).rotation;
                    values["Pitch"] = local.X; values["Yaw"] = local.Y; values["Roll"] = local.Z;
                }
                return values;
            }

            foreach (KeyValuePair<string, float> axis in key.Axes)
                values[axis.Key] = space == "relative" ? axis.Value + channel.Rest[axis.Key] : axis.Value;
            if (built != null)
            {
                values["Pitch"] = built.Value.X; values["Yaw"] = built.Value.Y; values["Roll"] = built.Value.Z;
            }
            if (pivoted != null)
            {
                values["x"] = pivoted.Value.X; values["y"] = pivoted.Value.Y; values["z"] = pivoted.Value.Z;
            }
            return values;
        }

        /// <summary>
        /// Between two keys that turn about the same pivot by more than 15 degrees, keys every 15 degrees or less (the rotation
        /// slerped, the position worked out from the pivot), so the entity swings round the arc rather than cutting across it.
        /// Returns how many were added.
        /// </summary>
        private static int SubdividePivots(List<TransformKey> keys)
        {
            List<TransformKey> ordered = keys.OrderBy(o => o.Time).ToList();
            List<TransformKey> extra = new List<TransformKey>();
            for (int i = 0; i < ordered.Count - 1; i++)
            {
                TransformKey a = ordered[i], b = ordered[i + 1];
                if (a.Pivot == null || b.Pivot == null || !a.FullRotation || !b.FullRotation || Vector3.Distance(a.Pivot.Value, b.Pivot.Value) > 1e-4f)
                    continue;
                Quaternion qa = InstanceTransform.ToQuaternion(a.Rotation), qb = InstanceTransform.ToQuaternion(b.Rotation);
                double angle = 2.0 * Math.Acos(Math.Min(1.0, Math.Abs(Quaternion.Dot(qa, qb)))) * 180.0 / Math.PI;
                //Unwrapped values can ask for more than half a turn the long way: follow the values given, not the shortest arc
                double asked = Math.Max(Math.Abs(b.Rotation.X - a.Rotation.X), Math.Max(Math.Abs(b.Rotation.Y - a.Rotation.Y), Math.Abs(b.Rotation.Z - a.Rotation.Z)));
                int steps = (int)Math.Ceiling(Math.Max(angle, asked) / 15.0);
                for (int j = 1; j < steps; j++)
                {
                    float f = j / (float)steps;
                    TransformKey mid = new TransformKey() { Time = a.Time + (b.Time - a.Time) * f, Where = a.Where, FullRotation = true, Pivot = a.Pivot };
                    Vector3 rotation = Vector3.Lerp(a.Rotation, b.Rotation, f);
                    mid.Axes["Pitch"] = rotation.X; mid.Axes["Yaw"] = rotation.Y; mid.Axes["Roll"] = rotation.Z;
                    extra.Add(mid);
                }
            }
            foreach (TransformKey key in extra)
                if (keys.Any(o => Math.Abs(o.Time - key.Time) <= Eps))
                    continue;
                else
                    keys.Add(key);
            return extra.Count(o => keys.Contains(o));
        }

        /// <summary>Where an entity resting at <paramref name="rest"/> ends up turned to <paramref name="rotation"/> about <paramref name="pivot"/> (all in one space).</summary>
        private static Vector3 AboutPivot(cTransform rest, Vector3 rotation, Vector3 pivot)
        {
            Quaternion turn = InstanceTransform.ToQuaternion(rotation) * Quaternion.Inverse(InstanceTransform.ToQuaternion(rest.rotation));
            return pivot + Vector3.Transform(rest.position - pivot, turn);
        }

        private static void Put(Dictionary<string, float> values, Vector3 position, Vector3 rotation)
        {
            values["x"] = position.X; values["y"] = position.Y; values["z"] = position.Z;
            values["Pitch"] = rotation.X; values["Yaw"] = rotation.Y; values["Roll"] = rotation.Z;
        }

        private static string Summary(int keys, int removed, int moved, bool replace)
        {
            List<string> parts = new List<string>();
            if (keys != 0) parts.Add((replace ? "replaced with " : "wrote ") + keys + (keys == 1 ? " key" : " keys"));
            if (removed != 0) parts.Add("removed " + removed);
            if (moved != 0) parts.Add("moved " + moved);
            return string.Join(", ", parts);
        }

        /// <summary>
        /// Change one float track's keys: deletions, then moves, then the keys given (a key at an existing time
        /// updates it, as CageAnimationCurves.SetKeyframe does). Returns how many keys it changed.
        /// </summary>
        private static int EditTrack(Working w, CAGEAnimation.FloatTrack track, string label, List<KeySpec> keys, bool replace, List<float> removeTimes, List<KeyValuePair<float, float>> moves, string tangents, bool strict, string where)
        {
            w.Touched.Add(track);
            int changed = 0;
            for (int i = 0; i < keys.Count; i++)
                for (int j = i + 1; j < keys.Count; j++)
                    if (Math.Abs(keys[i].Time - keys[j].Time) <= Eps)
                        throw new McpError(where + ": two keys for " + label + " are both at " + R(keys[i].Time) + " s.");

            if (replace)
            {
                changed += track.keyframes.Count;
                track.keyframes = new List<CAGEAnimation.FloatTrack.Keyframe>();
                w.Exact.Add(track);
            }
            foreach (float time in removeTimes)
            {
                CAGEAnimation.FloatTrack.Keyframe key = CageAnimationCurves.FindKeyframe(track, time);
                if (key == null)
                {
                    if (strict) throw new McpError(where + ": " + label + " has no key at " + R(time) + " s (its keys are at " + Times(track) + ").");
                    continue;
                }
                track.keyframes.Remove(key);
                changed++;
            }
            foreach (KeyValuePair<float, float> move in moves)
            {
                CAGEAnimation.FloatTrack.Keyframe key = CageAnimationCurves.FindKeyframe(track, move.Key);
                if (key == null)
                {
                    if (strict) throw new McpError(where + ": " + label + " has no key at " + R(move.Key) + " s to move (its keys are at " + Times(track) + ").");
                    continue;
                }
                CAGEAnimation.FloatTrack.Keyframe there = CageAnimationCurves.FindKeyframe(track, move.Value);
                if (there != null && there != key)
                    throw new McpError(where + ": " + label + " already has a key at " + R(move.Value) + " s; remove it first (remove_times).");
                CageAnimationCurves.SetTime(key, move.Value);
                changed++;
            }

            Dictionary<CAGEAnimation.FloatTrack.Keyframe, (bool inside, bool outside)> automatic = new Dictionary<CAGEAnimation.FloatTrack.Keyframe, (bool, bool)>();
            foreach (KeySpec spec in keys)
            {
                CAGEAnimation.FloatTrack.Keyframe key = CageAnimationCurves.FindKeyframe(track, spec.Time);
                bool made = key == null;
                if (made)
                {
                    key = CageAnimationCurves.NewKeyframe(spec.Time, spec.Value, w.Mode);
                    track.keyframes.Add(key);
                    changed++;
                }
                else if (Math.Abs(key.value.Y - spec.Value) > 1e-6f)
                {
                    key.value.Y = spec.Value;
                    changed++;
                }
                if (spec.TanIn.HasValue) { key.tan_in = spec.TanIn.Value; changed++; }
                if (spec.TanOut.HasValue) { key.tan_out = spec.TanOut.Value; changed++; }
                //New keys get sized tangents; an updated key keeps its own unless a preset was asked for
                if (made || tangents != null)
                    automatic[key] = (!spec.TanIn.HasValue, !spec.TanOut.HasValue);
            }
            track.keyframes.Sort((a, b) => a.time.CompareTo(b.time));
            //A key added between two others splits their segment: the neighbours' tangents facing it are sized to the new segments, keeping their slopes
            if (!replace)
            {
                foreach (KeyValuePair<CAGEAnimation.FloatTrack.Keyframe, (bool inside, bool outside)> key in automatic)
                {
                    int index = track.keyframes.IndexOf(key.Key);
                    if (index > 0 && !automatic.ContainsKey(track.keyframes[index - 1]))
                        Resize(track.keyframes[index - 1], false, key.Key.time - track.keyframes[index - 1].time);
                    if (index >= 0 && index < track.keyframes.Count - 1 && !automatic.ContainsKey(track.keyframes[index + 1]))
                        Resize(track.keyframes[index + 1], true, track.keyframes[index + 1].time - key.Key.time);
                }
            }
            foreach (KeyValuePair<CAGEAnimation.FloatTrack.Keyframe, (bool inside, bool outside)> key in automatic)
                SetTangents(track, key.Key, tangents ?? "flat", key.Value.inside, key.Value.outside);
            if (changed != 0) w.Changed = true;
            return changed;
        }

        /// <summary>Apply a tangent preset to every key of a track. Returns how many keys it set.</summary>
        private static int Retangent(CAGEAnimation.FloatTrack track, string preset)
        {
            track.keyframes.Sort((a, b) => a.time.CompareTo(b.time));
            foreach (CAGEAnimation.FloatTrack.Keyframe key in track.keyframes)
                SetTangents(track, key, preset, true, true);
            return track.keyframes.Count;
        }

        /// <summary>
        /// Size one side's tangent to a segment of <paramref name="segment"/> seconds (weight one, as exported keys carry), keeping
        /// its slope. A tangent of the animators' (0.01, 0.01) "linear" spelling is left alone: its slope means nothing.
        /// </summary>
        private static void Resize(CAGEAnimation.FloatTrack.Keyframe key, bool incoming, float segment)
        {
            if (segment <= 1e-6f) return;
            Vector2 tangent = incoming ? key.tan_in : key.tan_out;
            if (Math.Abs(tangent.X) < 0.05f) return;
            Vector2 sized = new Vector2(segment, tangent.Y / tangent.X * segment);
            if (incoming) key.tan_in = sized;
            else key.tan_out = sized;
        }

        /// <summary>
        /// Tangents sized to the neighbouring segments (a tangent's seconds equal to the segment is weight one,
        /// as exported retail keys carry): flat eases in and out, smooth passes through at the slope between
        /// the neighbours, straight points at them (a straight Bezier segment).
        /// </summary>
        private static void SetTangents(CAGEAnimation.FloatTrack track, CAGEAnimation.FloatTrack.Keyframe key, string preset, bool setIn, bool setOut)
        {
            int index = track.keyframes.IndexOf(key);
            CAGEAnimation.FloatTrack.Keyframe previous = index > 0 ? track.keyframes[index - 1] : null;
            CAGEAnimation.FloatTrack.Keyframe next = index >= 0 && index < track.keyframes.Count - 1 ? track.keyframes[index + 1] : null;
            float before = previous != null ? key.time - previous.time : 0f;
            float after = next != null ? next.time - key.time : 0f;
            if (before <= 1e-6f) before = after > 1e-6f ? after : 1f;
            if (after <= 1e-6f) after = before > 1e-6f ? before : 1f;

            float slopeIn = 0f, slopeOut = 0f;
            switch (preset)
            {
                case "straight":
                    if (previous != null) slopeIn = (key.value.Y - previous.value.Y) / before;
                    if (next != null) slopeOut = (next.value.Y - key.value.Y) / after;
                    break;
                case "smooth":
                    if (previous != null && next != null && next.time - previous.time > 1e-6f)
                        slopeIn = slopeOut = (next.value.Y - previous.value.Y) / (next.time - previous.time);
                    break;
                case "auto":
                    //Smooth inside; at an end, the slope towards the one neighbour (Catmull-Rom's one-sided end)
                    if (previous != null && next != null && next.time - previous.time > 1e-6f)
                        slopeIn = slopeOut = (next.value.Y - previous.value.Y) / (next.time - previous.time);
                    else if (next != null)
                        slopeIn = slopeOut = (next.value.Y - key.value.Y) / after;
                    else if (previous != null)
                        slopeIn = slopeOut = (key.value.Y - previous.value.Y) / before;
                    break;
                case "cyclic":
                    {
                        //Smooth, with the track read as one lap of a loop: the first key's neighbour before it is the key before
                        //the last, a lap earlier (and offset by however far the lap climbs), so first and last share a slope
                        List<CAGEAnimation.FloatTrack.Keyframe> keys = track.keyframes;
                        int n = keys.Count;
                        if (n >= 3 && (index == 0 || index == n - 1))
                        {
                            float period = keys[n - 1].time - keys[0].time;
                            float climb = keys[n - 1].value.Y - keys[0].value.Y;
                            float span = (keys[1].time - keys[0].time) + (keys[n - 1].time - keys[n - 2].time);
                            if (period > 1e-6f && span > 1e-6f)
                                slopeIn = slopeOut = (keys[1].value.Y + climb - keys[n - 2].value.Y) / span;
                            //The seam's segments: the first key's in is the lap's last segment, the last key's out the first
                            if (index == 0) before = keys[n - 1].time - keys[n - 2].time;
                            else after = keys[1].time - keys[0].time;
                        }
                        else if (previous != null && next != null && next.time - previous.time > 1e-6f)
                            slopeIn = slopeOut = (next.value.Y - previous.value.Y) / (next.time - previous.time);
                        else if (next != null)
                            slopeIn = slopeOut = (next.value.Y - key.value.Y) / after;
                        else if (previous != null)
                            slopeIn = slopeOut = (key.value.Y - previous.value.Y) / before;
                        break;
                    }
            }
            if (setIn) key.tan_in = new Vector2(before, slopeIn * before);
            if (setOut) key.tan_out = new Vector2(after, slopeOut * after);
        }

        /// <summary>
        /// A tangent reaching more than a whole segment past its neighbour makes the Bezier curve run backwards in
        /// time there. Keys added between existing ones shorten segments, so on the tracks this call touched such
        /// tangents are brought back to weight one, keeping their slope.
        /// </summary>
        private static void ClampTangents(Working w, List<string> notes)
        {
            int clamped = 0;
            foreach (CAGEAnimation.FloatTrack track in w.Touched)
            {
                if (!w.FloatTracks.Contains(track)) continue;
                List<CAGEAnimation.FloatTrack.Keyframe> keys = track.keyframes;
                for (int i = 0; i < keys.Count; i++)
                {
                    CAGEAnimation.FloatTrack.Keyframe key = keys[i];
                    if (i < keys.Count - 1)
                    {
                        float segment = keys[i + 1].time - key.time;
                        if (segment > 1e-6f && key.tan_out.X > 3f * segment)
                        {
                            key.tan_out = new Vector2(segment, key.tan_out.Y * segment / key.tan_out.X);
                            clamped++;
                        }
                    }
                    if (i > 0)
                    {
                        float segment = key.time - keys[i - 1].time;
                        if (segment > 1e-6f && key.tan_in.X > 3f * segment)
                        {
                            key.tan_in = new Vector2(segment, key.tan_in.Y * segment / key.tan_in.X);
                            clamped++;
                        }
                    }
                }
            }
            if (clamped != 0)
            {
                w.Changed = true;
                notes.Add(clamped + " tangent(s) reached past a neighbouring key (the curve would have run backwards in time) and were shortened to that segment, keeping their slope.");
            }
        }

        private static void RemoveTracks(Working w, JObject spec, string where)
        {
            CheckFields(spec, where, "track", "target", "from_root", "parameter", "component");
            List<CAGEAnimation.Connection> going;
            string label;
            if (spec["track"] != null)
            {
                if (spec["target"] != null || spec["parameter"] != null || spec["component"] != null || spec["from_root"] != null)
                    throw new McpError(where + ": give 'track', or 'target' (with parameter/component) - not both.");
                string reference = (string)spec["track"];
                ShortGuid? id = McpScript.ParseId(reference);
                CAGEAnimation.FloatTrack track = id == null ? null : w.FloatTrack(id.Value);
                if (track == null)
                    throw new McpError(where + ": this animation has no float track '" + reference + "'" + (id != null && w.EventTracks.Any(o => o.shortGUID == id.Value) ? " (that is an event track: set_animation_events removes those)" : "") + ".");
                //RemoveFloatTrack: the track and every connection to it
                w.FloatTracks.Remove(track);
                int connections = w.Connections.RemoveAll(o => o != null && o.target_track == track.shortGUID);
                w.Changed = true;
                w.Changes.Add("removed track " + McpScript.Id(track.shortGUID) + (connections == 0 ? " (it drove nothing)" : ""));
                return;
            }

            if (spec["target"] == null)
                throw new McpError(where + " needs 'track', or 'target' (with 'parameter' and 'component' to narrow it).");
            Target target = ResolveTarget(w.Commands, w.Composite, spec["target"], ReadFlag(spec["from_root"], where + ".from_root"), where + ".target");
            ShortGuid? parameter = spec["parameter"] == null || spec["parameter"].Type == JTokenType.Null ? (ShortGuid?)null : McpScript.ParamId((string)spec["parameter"]);
            string component = null;
            if (spec["component"] != null && spec["component"].Type != JTokenType.Null)
            {
                if (parameter == null) throw new McpError(where + ": 'component' needs 'parameter'.");
                component = CanonicalComponent((string)spec["component"], where + ".component");
            }
            going = w.Connections.Where(o => o != null && w.FloatTrack(o.target_track) != null
                && (parameter == null || o.target_param == parameter.Value)
                && (component == null || string.Equals(ComponentName(o.target_sub_param), component, StringComparison.OrdinalIgnoreCase))
                && SameTarget(w.Commands, w.Composite, o.connectedEntity, target)).ToList();
            if (going.Count == 0)
            {
                List<string> driven = w.Connections.Where(o => o != null && w.FloatTrack(o.target_track) != null && SameTarget(w.Commands, w.Composite, o.connectedEntity, target))
                    .Select(o => McpScript.ParamName(o.target_param) + (ComponentName(o.target_sub_param) == "" ? "" : "." + ComponentName(o.target_sub_param))).Distinct().ToList();
                throw new McpError(where + ": the animation drives " + (driven.Count == 0 ? "nothing on " + target.Name : "only " + string.Join(", ", driven) + " on " + target.Name) + ".");
            }
            label = target.Name + (parameter == null ? "" : "." + McpScript.ParamName(parameter.Value)) + (component == null ? "" : "." + component);

            //RemoveEntityFromAnimation / RemoveFloatTrack: the connections, and their tracks unless another connection still plays one
            HashSet<CAGEAnimation.Connection> goingSet = new HashSet<CAGEAnimation.Connection>(going);
            w.Connections.RemoveAll(o => goingSet.Contains(o));
            HashSet<ShortGuid> stillPlayed = new HashSet<ShortGuid>(w.Connections.Where(o => o != null).Select(o => o.target_track));
            int tracks = w.FloatTracks.RemoveAll(o => o != null && going.Any(c => c.target_track == o.shortGUID) && !stillPlayed.Contains(o.shortGUID));
            w.Changed = true;
            w.Changes.Add("stopped animating " + label + " (" + tracks + (tracks == 1 ? " track" : " tracks") + " removed)");
        }
        #endregion

        #region set_animation_events
        private static readonly string[] BindingSlots = { "marker", "character", "camera" };

        private static ObjectType SlotType(string slot)
        {
            switch (slot)
            {
                case "marker": return ObjectType.MARKER;
                case "character": return ObjectType.CHARACTER;
                default: return ObjectType.CAMERA;
            }
        }

        /// <summary>What each binding may point at: the editor's own rule (CAGEAnimationEditor.FunctionTypesForBinding), so the two stay alike.</summary>
        private static FunctionType[] SlotFunctions(ObjectType type)
        {
            return (CAGEAnimationEditor.FunctionTypesForBinding(type) ?? new List<FunctionType>() { FunctionType.CameraResource }).ToArray();
        }

        /// <summary>Retail binds composite instances as markers and characters (127 character and 100 marker bindings over 20 levels), never as cameras.</summary>
        private static bool SlotTakesInstances(ObjectType type) => CAGEAnimationEditor.BindingTakesInstances(type);

        /// <summary>What an alias or proxy stands for (itself otherwise), for checking a binding's type.</summary>
        private static Entity StandsFor(Commands commands, Composite composite, Entity entity)
        {
            try
            {
                if (entity is AliasEntity alias && alias.alias?.path != null)
                    return commands.Utils.GetResolvedTarget(commands.Utils.ResolveAlias(alias.alias.path, composite)).Item2 ?? entity;
                if (entity is ProxyEntity proxy && proxy.proxy?.path != null)
                    return commands.Utils.GetResolvedTarget(commands.Utils.ResolveProxy(proxy.proxy.path)).Item2 ?? entity;
            }
            catch { }
            return entity;
        }

        private static JObject Merge(JObject a, JObject b)
        {
            JObject merged = (JObject)a.DeepClone();
            foreach (JProperty property in b.Properties())
                merged[property.Name] = property.Value.DeepClone();
            return merged;
        }

        private sealed class EventWork
        {
            public Working W;
            public readonly Dictionary<string, CAGEAnimation.EventTrack> Refs = new Dictionary<string, CAGEAnimation.EventTrack>(StringComparer.OrdinalIgnoreCase);
            public readonly List<string> Notes = new List<string>();
            public readonly HashSet<CAGEAnimation.EventTrack> Touched = new HashSet<CAGEAnimation.EventTrack>();
            /// <summary>Renamed pins whose links go with them (old pin to new pin).</summary>
            public readonly Dictionary<ShortGuid, ShortGuid> Carry = new Dictionary<ShortGuid, ShortGuid>();
        }

        /// <summary>Re-point the links on one of an animation's event pins at another pin of it (out of the pin, and any into it).</summary>
        private static void CarryLinks(McpScriptEdit edit, Composite composite, CAGEAnimation animation, ShortGuid from, ShortGuid to)
        {
            string fromName = McpScript.ParamName(from), toName = McpScript.ParamName(to);
            foreach (EntityConnector link in animation.childLinks.Where(o => o.thisParamID == from).ToList())
            {
                Entity target = composite.GetEntityByID(link.linkedEntityID);
                if (target == null) continue;
                string targetPin = McpScript.ParamName(link.linkedParamID);
                edit.RemoveLinks(composite, animation, fromName, target, targetPin);
                edit.AddLink(composite, animation, toName, target, targetPin, allowCustom: true);
            }
            foreach (Entity other in composite.GetEntities().ToList())
            {
                foreach (EntityConnector link in other.childLinks.Where(o => o.linkedEntityID == animation.shortGUID && o.linkedParamID == from).ToList())
                {
                    string ownPin = McpScript.ParamName(link.thisParamID);
                    edit.RemoveLinks(composite, other, ownPin, animation, fromName);
                    edit.AddLink(composite, other, ownPin, animation, toName, allowCustom: true);
                }
            }
        }

        private static object SetAnimationEvents(McpCall call)
        {
            string[] parts = { "add_tracks", "remove_tracks", "add", "remove", "move", "update", "rename", "bindings" };
            if (!parts.Any(call.Has))
                throw new McpError("Nothing to do: give add, remove, move, update, rename, add_tracks, remove_tracks or bindings.");

            return McpEditor.UI(() =>
            {
                Working w = Begin(call);
                CompileIfShown(w.Composite);
                EventWork e = new EventWork() { W = w };

                //Tracks first, so the rest of the call can name the new ones
                List<string> removeTracks = call.StrList("remove_tracks");
                foreach (string reference in removeTracks)
                {
                    CAGEAnimation.EventTrack track = FindEventTrack(e, reference, "remove_tracks");
                    w.EventTracks.Remove(track);
                    w.Connections.RemoveAll(o => o != null && o.target_track == track.shortGUID);
                    w.Changed = true;
                    w.Changes.Add("removed event track " + McpScript.Id(track.shortGUID) + " (" + track.keyframes.Count + " events)");
                }
                JArray addTracks = call.Array("add_tracks");
                for (int i = 0; i < addTracks.Count; i++)
                {
                    string where = "add_tracks[" + i + "]";
                    JObject spec = ItemAt(addTracks[i], where);
                    CheckFields(spec, where, "type", "ref", "marker", "character", "camera", "from_root");
                    string type = ((string)spec["type"] ?? "").Trim().ToLowerInvariant();
                    if (type != "string" && type != "entity")
                        throw new McpError("'" + where + ".type' is string or entity.");
                    CAGEAnimation.EventTrack track = NewEventTrack(w, type == "entity" ? ANIM_TRACK_TYPE.T_GUID : ANIM_TRACK_TYPE.T_STRING);
                    string reference = (string)spec["ref"];
                    if (!string.IsNullOrWhiteSpace(reference))
                    {
                        if (e.Refs.ContainsKey(reference.Trim())) throw new McpError("'" + where + ".ref' '" + reference + "' is used twice.");
                        e.Refs[reference.Trim()] = track;
                    }
                    w.Changes.Add("added " + type + " event track " + McpScript.Id(track.shortGUID) + (string.IsNullOrWhiteSpace(reference) ? "" : " ('" + reference.Trim() + "')"));
                    ApplyBindings(e, track, spec, where);
                }
                JArray bindings = call.Array("bindings");
                for (int i = 0; i < bindings.Count; i++)
                {
                    string where = "bindings[" + i + "]";
                    JObject spec = ItemAt(bindings[i], where);
                    CheckFields(spec, where, "track", "marker", "character", "camera", "from_root");
                    if (!BindingSlots.Any(o => spec[o] != null))
                        throw new McpError(where + " needs marker, character or camera.");
                    ApplyBindings(e, FindEventTrack(e, (string)spec["track"], where + ".track"), spec, where);
                }

                JArray removes = call.Array("remove");
                for (int i = 0; i < removes.Count; i++)
                    RemoveEvents(e, ItemAt(removes[i], "remove[" + i + "]"), "remove[" + i + "]");
                JArray renames = call.Array("rename");
                for (int i = 0; i < renames.Count; i++)
                    RenameEvents(e, ItemAt(renames[i], "rename[" + i + "]"), "rename[" + i + "]");
                JArray updates = call.Array("update");
                for (int i = 0; i < updates.Count; i++)
                    UpdateEvent(e, ItemAt(updates[i], "update[" + i + "]"), "update[" + i + "]");
                JArray moves = call.Array("move");
                for (int i = 0; i < moves.Count; i++)
                    MoveEvent(e, ItemAt(moves[i], "move[" + i + "]"), "move[" + i + "]");
                JArray adds = call.Array("add");
                for (int i = 0; i < adds.Count; i++)
                    AddEvent(e, ItemAt(adds[i], "add[" + i + "]"), "add[" + i + "]");

                //Lanes are kept in time order, as the curve editor leaves them after a drag
                foreach (CAGEAnimation.EventTrack track in e.Touched)
                    track.keyframes = track.keyframes.OrderBy(o => o.time).ToList();

                //The editor's guard (ConfirmRemovingStringEvents): pins that go away take their links with them - unless a rename carries them
                List<ShortGuid> before = EventPins(w.Animation.eventTracks);
                List<ShortGuid> after = EventPins(w.EventTracks);
                List<ShortGuid> gone = before.Where(o => !after.Contains(o)).ToList();
                Dictionary<ShortGuid, ShortGuid> carry = e.Carry.Where(o => gone.Contains(o.Key) && after.Contains(o.Value)).ToDictionary(o => o.Key, o => o.Value);
                JArray broken = new JArray();
                int carried = 0;
                foreach (ShortGuid pin in gone)
                {
                    int links = LinkCount(w.Composite, w.Animation, pin);
                    if (links == 0) continue;
                    if (carry.ContainsKey(pin)) { carried += links; continue; }
                    broken.Add(new JObject() { ["pin"] = McpScript.ParamName(pin), ["links"] = links });
                }
                if (broken.Count != 0 && !call.Bool("allow_breaking_links"))
                    throw new McpError("These event pins are still linked, and this would remove them: " + string.Join(", ", broken.Select(o => (string)o["pin"] + " (" + (int)o["links"] + ")")) + ". Those links would break. Rename with carry_links: true to move them to the new pins, re-link them first, or pass allow_breaking_links: true.");

                string label = "Edit events of " + McpScript.EntityName(w.Commands, w.Composite, w.Animation);
                object mark = UndoStack.Current.Mark();
                bool changed = w.Commit(label);
                if (changed && carried != 0)
                {
                    //The links on the old pins, moved to the new ones as a script edit - folded into the same undo step
                    McpScriptEdit.Outcome relinked = McpScriptEdit.Run("Carry event links", w.Composite, edit =>
                    {
                        foreach (KeyValuePair<ShortGuid, ShortGuid> pin in carry)
                            CarryLinks(edit, w.Composite, w.Animation, pin.Key, pin.Value);
                    }, show: false);
                    int steps = UndoStack.Current.StepsSince(mark);
                    if (relinked.Changed && steps > 1 && !UndoStack.Current.Collapse(steps, "AI: " + label))
                        call.Note("The event edit and the moved links are two steps on the undo history.");
                    w.Changes.Add("moved " + carried + (carried == 1 ? " link" : " links") + " to the renamed pins");
                }
                if (changed)
                    McpScriptEdit.Show(w.Composite, new List<Entity>() { w.Animation });

                JObject described = Describe(w.Commands, w.Composite, w.Animation, new DescribeOptions() { MaxKeys = 0, MaxTracks = 0 });
                JObject answer = new JObject() { ["changed"] = changed };
                if (changed && w.Changes.Count != 0) answer["changes"] = new JArray(w.Changes);
                if (e.Refs.Count != 0) answer["new_tracks"] = new JObject(e.Refs.Select(o => new JProperty(o.Key, McpScript.Id(o.Value.shortGUID))));
                List<ShortGuid> added = after.Where(o => !before.Contains(o)).ToList();
                if (added.Count != 0) answer["pins_added"] = new JArray(added.Select(McpScript.ParamName));
                if (gone.Count != 0) answer["pins_removed"] = new JArray(gone.Select(McpScript.ParamName));
                if (broken.Count != 0 && changed)
                {
                    answer["broken_links"] = broken;
                    call.Note("Links on the removed pins are still in the script, pointing at pins that no longer exist: remove_links (from this CAGEAnimation, param = the pin) clears them.");
                }
                foreach (string key in new[] { "composite", "id", "name", "length", "event_tracks", "event_pins", "connections_without_track" })
                    if (described[key] != null) answer[key] = described[key];
                foreach (string note in e.Notes.Concat(w.Notes)) call.Note(note);
                if (!changed) call.Note("Nothing changed, so no undo step was made.");
                return answer;
            });
        }

        private static CAGEAnimation.EventTrack NewEventTrack(Working w, ANIM_TRACK_TYPE type)
        {
            //CreateEventTrack: a random id and the lane's type
            CAGEAnimation.EventTrack track = new CAGEAnimation.EventTrack() { shortGUID = ShortGuidUtils.GenerateRandom(), track_type = type };
            w.EventTracks.Add(track);
            w.Changed = true;
            return track;
        }

        private static CAGEAnimation.EventTrack FindEventTrack(EventWork e, string reference, string where)
        {
            if (string.IsNullOrWhiteSpace(reference))
                throw new McpError("'" + where + "' names no track.");
            if (e.Refs.TryGetValue(reference.Trim(), out CAGEAnimation.EventTrack byRef))
                return byRef;
            ShortGuid? id = McpScript.ParseId(reference);
            CAGEAnimation.EventTrack track = id == null ? null : e.W.EventTracks.FirstOrDefault(o => o != null && o.shortGUID == id.Value);
            if (track != null) return track;
            bool isFloat = id != null && e.W.FloatTrack(id.Value) != null;
            throw new McpError("'" + where + "': this animation has no event track '" + reference + "'" + (isFloat ? " (that is a float track: animate_parameters edits those)" : "")
                + ". Its event tracks: " + (e.W.EventTracks.Count == 0 ? "none" : string.Join(", ", e.W.EventTracks.Select(o => McpScript.Id(o.shortGUID) + " (" + TrackTypeName(o) + ")"))) + ".");
        }

        private static bool IsBound(Working w, CAGEAnimation.EventTrack track) => w.Connections.Any(o => o != null && o.target_track == track.shortGUID);

        /// <summary>Where a string event goes when no track is named: the animation's own (unbound) string lane, made if missing (GetOrCreateSelfEventTrack).</summary>
        private static CAGEAnimation.EventTrack DefaultStringTrack(EventWork e)
        {
            CAGEAnimation.EventTrack track = e.W.EventTracks.FirstOrDefault(o => o != null && TrackTypeName(o) == "string" && !IsBound(e.W, o));
            if (track != null) return track;
            track = NewEventTrack(e.W, ANIM_TRACK_TYPE.T_STRING);
            e.W.Changes.Add("added string event track " + McpScript.Id(track.shortGUID));
            return track;
        }

        private static CAGEAnimation.EventTrack DefaultEntityTrack(EventWork e, string where)
        {
            List<CAGEAnimation.EventTrack> tracks = e.W.EventTracks.Where(o => o != null && TrackTypeName(o) == "entity").ToList();
            if (tracks.Count == 1) return tracks[0];
            if (tracks.Count > 1)
                throw new McpError(where + ": the animation has " + tracks.Count + " animation-entity tracks (" + string.Join(", ", tracks.Select(o => McpScript.Id(o.shortGUID))) + "); say which with 'track'.");
            CAGEAnimation.EventTrack track = NewEventTrack(e.W, ANIM_TRACK_TYPE.T_GUID);
            e.W.Changes.Add("added entity event track " + McpScript.Id(track.shortGUID));
            return track;
        }

        private static bool IsClear(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null) return true;
            if (value is JArray array) return array.Count == 0;
            if (value.Type == JTokenType.String)
            {
                string text = ((string)value).Trim();
                return text.Length == 0 || string.Equals(text, "none", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private static void ApplyBindings(EventWork e, CAGEAnimation.EventTrack track, JObject spec, string where)
        {
            Working w = e.W;
            bool fromRoot = ReadFlag(spec["from_root"], where + ".from_root");
            foreach (string slot in BindingSlots)
            {
                if (spec.Property(slot) == null) continue;
                //Only an animation-entity lane has the binding slots (the editor shows them for T_GUID tracks)
                if (TrackTypeName(track) != "entity")
                    throw new McpError(where + ": only animation-entity tracks take marker/character/camera bindings; " + McpScript.Id(track.shortGUID) + " is a " + TrackTypeName(track) + " track.");
                ObjectType type = SlotType(slot);
                JToken value = spec[slot];
                if (IsClear(value))
                {
                    int removed = w.Connections.RemoveAll(o => o != null && o.target_track == track.shortGUID && o.binding_type == type);
                    if (removed != 0)
                    {
                        w.Changed = true;
                        w.Changes.Add("unbound the " + slot + " of track " + McpScript.Id(track.shortGUID));
                    }
                    else
                        e.Notes.Add("Track " + McpScript.Id(track.shortGUID) + " had no " + slot + " to unbind.");
                    continue;
                }
                Target target = ResolveTarget(w.Commands, w.Composite, value, fromRoot, where + "." + slot);
                FunctionType[] allowed = SlotFunctions(type);
                //An alias or proxy binds what it stands for (retail binds aliases to instances, root-relative)
                Entity bound = StandsFor(w.Commands, target.Composite, target.Entity);
                bool instance = bound is FunctionEntity placed && !placed.function.IsFunctionType;
                bool ok = bound is FunctionEntity function && (function.function.IsFunctionType ? allowed.Contains(function.function.AsFunctionType) : SlotTakesInstances(type));
                if (!ok)
                    throw new McpError(where + "." + slot + ": " + target.Name + " is a " + McpScript.TypeName(w.Commands, target.Composite, target.Entity) + (bound != target.Entity && bound != null ? " (standing for a " + KindOf(bound) + ")" : "") + "; a " + slot + " binding takes " + string.Join(", ", allowed) + (SlotTakesInstances(type) ? " or a composite instance" : "") + "." + (type == ObjectType.CAMERA && instance ? " A camera binding is for CameraPlayAnimation clips; to move a camera, key a CameraResource's position (animate_parameters)." : ""));
                //FindOrCreateBindingConnection: one connection per binding type on the track
                CAGEAnimation.Connection connection = w.Connections.FirstOrDefault(o => o != null && o.target_track == track.shortGUID && o.binding_type == type);
                if (connection == null)
                {
                    connection = new CAGEAnimation.Connection() { binding_guid = ShortGuidUtils.GenerateRandom(), target_track = track.shortGUID, binding_type = type };
                    w.Connections.Add(connection);
                }
                else if (connection.connectedEntity == target.Path)
                    continue;
                connection.connectedEntity = new EntityPath((ShortGuid[])target.Path.path.Clone());
                w.Changed = true;
                w.Changes.Add("bound the " + slot + " of track " + McpScript.Id(track.shortGUID) + " to " + target.Name);
            }
        }

        /// <summary>An entity event's entity: a play-animation function in the animation's own composite, where the game looks it up.</summary>
        private static FunctionEntity EventEntity(Working w, string reference, string where)
        {
            Entity entity = McpScript.FindEntity(w.Commands, w.Composite, reference);
            if (entity is FunctionEntity function && function.function.IsFunctionType && EventEntityTypes.Contains(function.function.AsFunctionType))
                return function;
            throw new McpError("'" + where + "': " + McpScript.EntityName(w.Commands, w.Composite, entity) + " is a " + McpScript.TypeName(w.Commands, w.Composite, entity) + "; an animation-entity event fires a CMD_PlayAnimation, CameraPlayAnimation or PlayEnvironmentAnimation in " + w.Composite.name + ".");
        }

        private static void AddEvent(EventWork e, JObject spec, string where)
        {
            Working w = e.W;
            CheckFields(spec, where, "time", "event", "entity", "duration", "track");
            float time = ReadTime(spec["time"], where + ".time");
            bool named = spec["event"] != null && spec["event"].Type != JTokenType.Null;
            bool entity = spec["entity"] != null && spec["entity"].Type != JTokenType.Null;
            if (named == entity)
                throw new McpError(where + " needs one of 'event' (a string event's name) or 'entity' (an animation entity).");
            float duration = 0f;
            if (spec["duration"] != null && spec["duration"].Type != JTokenType.Null)
            {
                duration = ReadNumber(spec["duration"], where + ".duration");
                if (duration < 0f) throw new McpError("'" + where + ".duration' cannot be negative.");
            }
            string trackRef = spec["track"] == null || spec["track"].Type == JTokenType.Null ? null : (string)spec["track"];

            CAGEAnimation.EventTrack track;
            CAGEAnimation.EventTrack.Keyframe key;
            string label;
            if (named)
            {
                string name = ((string)spec["event"]).Trim();
                if (name.Length == 0) throw new McpError("'" + where + ".event' is empty.");
                track = trackRef != null ? FindEventTrack(e, trackRef, where + ".track") : DefaultStringTrack(e);
                if (TrackTypeName(track) != "string")
                    throw new McpError(where + ": track " + McpScript.Id(track.shortGUID) + " is a " + TrackTypeName(track) + " track; string events go on a string track (leave 'track' out to use the animation's own).");
                ShortGuid forward = ShortGuidUtils.Generate(name, false);
                if (track.keyframes.Any(o => o.track_type == ANIM_TRACK_TYPE.T_STRING && o.forward == forward && Math.Abs(o.time - time) <= Eps))
                {
                    e.Notes.Add("'" + name + "' is already at " + R(time) + " s on track " + McpScript.Id(track.shortGUID) + ".");
                    return;
                }
                //The editor's EventTrack.Keyframe(time, name): forward = name, reverse = reverse_ + name
                key = new CAGEAnimation.EventTrack.Keyframe(time, name) { duration = duration };
                label = "'" + name + "'";
            }
            else
            {
                FunctionEntity function = EventEntity(w, (string)spec["entity"], where + ".entity");
                track = trackRef != null ? FindEventTrack(e, trackRef, where + ".track") : DefaultEntityTrack(e, where);
                if (TrackTypeName(track) != "entity")
                    throw new McpError(where + ": track " + McpScript.Id(track.shortGUID) + " is a " + TrackTypeName(track) + " track; animation-entity events go on an entity track (add_tracks with type entity makes one).");
                if (track.keyframes.Any(o => o.track_type == ANIM_TRACK_TYPE.T_GUID && o.forward == function.shortGUID && Math.Abs(o.time - time) <= Eps))
                {
                    e.Notes.Add(McpScript.EntityName(w.Commands, w.Composite, function) + " is already at " + R(time) + " s on track " + McpScript.Id(track.shortGUID) + ".");
                    return;
                }
                key = new CAGEAnimation.EventTrack.Keyframe(time, function) { duration = duration };
                label = McpScript.EntityName(w.Commands, w.Composite, function);
            }
            track.keyframes.Add(key);
            e.Touched.Add(track);
            w.Changed = true;
            w.Changes.Add("added " + label + " at " + R(time) + " s on track " + McpScript.Id(track.shortGUID));
        }

        /// <summary>The events a remove or move entry picks: every given field must match.</summary>
        private static List<(CAGEAnimation.EventTrack track, CAGEAnimation.EventTrack.Keyframe key)> MatchEvents(EventWork e, JObject spec, string where, string timeField)
        {
            Working w = e.W;
            JToken timeToken = spec[timeField];
            float? time = timeToken == null || timeToken.Type == JTokenType.Null ? (float?)null : ReadTime(timeToken, where + "." + timeField);
            ShortGuid? named = null, entity = null;
            if (spec["event"] != null && spec["event"].Type != JTokenType.Null)
            {
                string name = ((string)spec["event"]).Trim();
                named = McpScript.ParseId(name) ?? ShortGuidUtils.Generate(name, false);
            }
            if (spec["entity"] != null && spec["entity"].Type != JTokenType.Null)
            {
                string reference = (string)spec["entity"];
                Entity found = null;
                try { found = McpScript.FindEntity(w.Commands, w.Composite, reference); } catch (McpError) { }
                entity = found?.shortGUID ?? McpScript.ParseId(reference) ?? throw new McpError("'" + where + ".entity': no entity '" + reference + "' in " + w.Composite.name + ".");
            }
            if (named != null && entity != null)
                throw new McpError(where + ": give 'event' or 'entity', not both.");
            if (time == null && named == null && entity == null)
                throw new McpError(where + " needs 'event', 'entity' or '" + timeField + "' to say which events.");
            List<CAGEAnimation.EventTrack> tracks = spec["track"] != null && spec["track"].Type != JTokenType.Null
                ? new List<CAGEAnimation.EventTrack>() { FindEventTrack(e, (string)spec["track"], where + ".track") }
                : w.EventTracks.Where(o => o != null).ToList();

            List<(CAGEAnimation.EventTrack, CAGEAnimation.EventTrack.Keyframe)> matches = new List<(CAGEAnimation.EventTrack, CAGEAnimation.EventTrack.Keyframe)>();
            foreach (CAGEAnimation.EventTrack track in tracks)
            {
                foreach (CAGEAnimation.EventTrack.Keyframe key in track.keyframes)
                {
                    if (key == null) continue;
                    if (time != null && Math.Abs(key.time - time.Value) > Eps) continue;
                    if (named != null && (key.track_type == ANIM_TRACK_TYPE.T_GUID || key.forward != named.Value)) continue;
                    if (entity != null && (key.track_type != ANIM_TRACK_TYPE.T_GUID || key.forward != entity.Value)) continue;
                    matches.Add((track, key));
                }
            }
            return matches;
        }

        private static string EventLabel(Working w, CAGEAnimation.EventTrack.Keyframe key)
        {
            if (key.track_type == ANIM_TRACK_TYPE.T_GUID)
            {
                Entity entity = w.Composite.GetEntityByID(key.forward);
                return entity == null ? McpScript.Id(key.forward) : McpScript.EntityName(w.Commands, w.Composite, entity);
            }
            return "'" + McpScript.ParamName(key.forward) + "'";
        }

        private static void RemoveEvents(EventWork e, JObject spec, string where)
        {
            CheckFields(spec, where, "event", "entity", "time", "track");
            List<(CAGEAnimation.EventTrack track, CAGEAnimation.EventTrack.Keyframe key)> matches = MatchEvents(e, spec, where, "time");
            if (matches.Count == 0)
                throw new McpError(where + ": no event matches. get_cage_animation lists the animation's events.");
            foreach ((CAGEAnimation.EventTrack track, CAGEAnimation.EventTrack.Keyframe key) in matches)
            {
                track.keyframes.Remove(key);
                e.Touched.Add(track);
                e.W.Changes.Add("removed " + EventLabel(e.W, key) + " at " + R(key.time) + " s");
            }
            e.W.Changed = true;
        }

        private static void MoveEvent(EventWork e, JObject spec, string where)
        {
            CheckFields(spec, where, "time", "to", "event", "entity", "track");
            if (spec["time"] == null) throw new McpError(where + " needs 'time': when the event is now.");
            float to = ReadTime(spec["to"], where + ".to");
            List<(CAGEAnimation.EventTrack track, CAGEAnimation.EventTrack.Keyframe key)> matches = MatchEvents(e, spec, where, "time");
            if (matches.Count == 0)
                throw new McpError(where + ": no event is at that time" + (spec["event"] != null || spec["entity"] != null || spec["track"] != null ? " matching what was given" : "") + ".");
            if (matches.Count > 1)
                throw new McpError(where + ": " + matches.Count + " events are at that time (" + string.Join(", ", matches.Select(o => EventLabel(e.W, o.key))) + "); give 'event', 'entity' or 'track' to pick one.");
            float from = matches[0].key.time;
            matches[0].key.time = to;
            e.Touched.Add(matches[0].track);
            e.W.Changed = true;
            e.W.Changes.Add("moved " + EventLabel(e.W, matches[0].key) + " from " + R(from) + " s to " + R(to) + " s");
        }

        /// <summary>update: change the one matched event in place - its duration, the entity it fires, its time - keeping everything else it holds.</summary>
        private static void UpdateEvent(EventWork e, JObject spec, string where)
        {
            CheckFields(spec, where, "time", "event", "entity", "track", "duration", "entity_to", "to");
            if (spec["time"] == null) throw new McpError(where + " needs 'time': when the event is now.");
            if (spec["duration"] == null && spec["entity_to"] == null && spec["to"] == null)
                throw new McpError(where + " changes nothing: give duration, entity_to or to.");
            List<(CAGEAnimation.EventTrack track, CAGEAnimation.EventTrack.Keyframe key)> matches = MatchEvents(e, spec, where, "time");
            if (matches.Count == 0)
                throw new McpError(where + ": no event is at that time" + (spec["event"] != null || spec["entity"] != null || spec["track"] != null ? " matching what was given" : "") + ". get_cage_animation lists the animation's events.");
            if (matches.Count > 1)
                throw new McpError(where + ": " + matches.Count + " events are at that time (" + string.Join(", ", matches.Select(o => EventLabel(e.W, o.key))) + "); give 'event', 'entity' or 'track' to pick one.");
            (CAGEAnimation.EventTrack track, CAGEAnimation.EventTrack.Keyframe key) = matches[0];
            List<string> done = new List<string>();
            string label = EventLabel(e.W, key);
            if (spec["entity_to"] != null && spec["entity_to"].Type != JTokenType.Null)
            {
                if (key.track_type != ANIM_TRACK_TYPE.T_GUID)
                    throw new McpError(where + ": " + label + " is a string event; 'entity_to' re-points an animation-entity event (rename renames a string event).");
                FunctionEntity function = EventEntity(e.W, (string)spec["entity_to"], where + ".entity_to");
                if (function.shortGUID != key.forward)
                {
                    //As the editor's Reassign: the id it fires changes, the stored reverse id stays
                    key.forward = function.shortGUID;
                    done.Add("fires " + McpScript.EntityName(e.W.Commands, e.W.Composite, function));
                }
            }
            if (spec["duration"] != null && spec["duration"].Type != JTokenType.Null)
            {
                float duration = ReadNumber(spec["duration"], where + ".duration");
                if (duration < 0f) throw new McpError("'" + where + ".duration' cannot be negative.");
                if (Math.Abs(duration - key.duration) > 1e-6f) { key.duration = duration; done.Add("duration " + R(duration) + " s"); }
            }
            if (spec["to"] != null && spec["to"].Type != JTokenType.Null)
            {
                float to = ReadTime(spec["to"], where + ".to");
                if (Math.Abs(to - key.time) > Eps) { done.Add("moved to " + R(to) + " s"); key.time = to; }
            }
            if (done.Count == 0)
            {
                e.Notes.Add(where + ": " + label + " already holds that.");
                return;
            }
            e.Touched.Add(track);
            e.W.Changed = true;
            e.W.Changes.Add("updated " + label + " (" + string.Join(", ", done) + ")");
        }

        private static void RenameEvents(EventWork e, JObject spec, string where)
        {
            CheckFields(spec, where, "from", "to", "track", "carry_links");
            string from = ((string)spec["from"] ?? "").Trim();
            string to = ((string)spec["to"] ?? "").Trim();
            if (from.Length == 0 || to.Length == 0)
                throw new McpError(where + " needs 'from' and 'to' names.");
            ShortGuid old = McpScript.ParseId(from) ?? ShortGuidUtils.Generate(from, false);
            if (ReadFlag(spec["carry_links"], where + ".carry_links"))
            {
                e.Carry[old] = ShortGuidUtils.Generate(to);
                ShortGuid oldReverse = e.W.EventTracks.Where(o => o != null).SelectMany(o => o.keyframes).FirstOrDefault(o => o != null && o.track_type != ANIM_TRACK_TYPE.T_GUID && o.forward == old)?.reverse ?? ShortGuidUtils.Generate("reverse_" + from, false);
                e.Carry[oldReverse] = ShortGuidUtils.Generate("reverse_" + to);
            }
            List<CAGEAnimation.EventTrack> tracks = spec["track"] != null && spec["track"].Type != JTokenType.Null
                ? new List<CAGEAnimation.EventTrack>() { FindEventTrack(e, (string)spec["track"], where + ".track") }
                : e.W.EventTracks.Where(o => o != null).ToList();
            int renamed = 0;
            foreach (CAGEAnimation.EventTrack track in tracks)
            {
                foreach (CAGEAnimation.EventTrack.Keyframe key in track.keyframes)
                {
                    if (key == null || key.track_type == ANIM_TRACK_TYPE.T_GUID || key.forward != old) continue;
                    //As the editor's Event name box: both pins regenerated from the new name
                    key.forward = ShortGuidUtils.Generate(to);
                    key.reverse = ShortGuidUtils.Generate("reverse_" + to);
                    key.track_type = ANIM_TRACK_TYPE.T_STRING;
                    e.Touched.Add(track);
                    renamed++;
                }
            }
            if (renamed == 0)
                throw new McpError(where + ": no string event is called '" + from + "'.");
            e.W.Changed = true;
            e.W.Changes.Add("renamed '" + from + "' to '" + to + "' (" + renamed + (renamed == 1 ? " event)" : " events)"));
        }
        #endregion

        #region preview_cage_animation
        /// <summary>
        /// Animation Mode's host for a preview a tool takes: a copy of the animation, so the session's
        /// recording (a gizmo drag while the picture is taken) can never reach the level's data.
        /// </summary>
        private sealed class PreviewHost : IAnimationModeHost
        {
            public CAGEAnimation Animation { get; set; }
            public Composite AnimationComposite { get; set; }
            public LevelContent AnimationContent { get; set; }
            public float AnimationLength { get; set; }
            public bool BezierInterpolation { get; set; }
            /// <summary>An edit made in the inspector or viewport while the pose was up: taken as a keyframe of the copy, so not applied. UI thread.</summary>
            public string EditTaken;
            public void OnAnimationEditedExternally(string undoLabel, bool structureChanged) { EditTaken = undoLabel ?? "an edit"; }
        }

        /// <summary>The viewer has what it was last sent on screen: connected, the level populated, no populate in flight. UI thread.</summary>
        private static bool ViewerSettled() => Send.Connected && ViewerResourceSync.ViewerReady && ViewerPopulateSync.ActivePopulateToken == 0;

        private static readonly TimeSpan ViewerReadyTimeout = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Wait, as capture_viewport does, until the viewer has taken the composite on screen, finished populating it and
        /// holds the level's models and materials as they are now. A pose has to go after that: a populate starts the
        /// viewer's scene afresh, dropping a pose sent before it, and nothing sends it again. <paramref name="populateEventsBefore"/>,
        /// when not -1, is <see cref="ViewerPopulateSync.PopulateEvents"/> from before a composite was opened.
        /// </summary>
        private static void AwaitViewerSettled(McpCall call, int populateEventsBefore)
        {
            if (!McpEditor.WaitFor(call, () => Send.Connected, TimeSpan.FromSeconds(60), "Waiting for the viewport"))
                throw new McpError("The viewport is not connected yet (it may still be starting, or loading the level).");
            //Give the viewer a moment to start on a composite just opened, so its old scene is not taken for the new one
            if (populateEventsBefore >= 0)
                McpEditor.WaitFor(call, () => ViewerPopulateSync.PopulateEvents != populateEventsBefore || !Send.Connected, TimeSpan.FromSeconds(3), "Waiting for the viewport to take the composite");
            AwaitViewerPopulated(call);

            //Resource edits go to the viewer on a coalescing timer: let any pending batch land first
            bool synced = false;
            McpEditor.UI(() => ViewerResourceSync.AfterNextSync(() => synced = true));
            if (!McpEditor.WaitFor(call, () => synced, TimeSpan.FromSeconds(30), "Sending changed models and materials to the viewport"))
                call.Note("Some model, material or texture changes may not have reached the viewport yet.");
        }

        private static void AwaitViewerPopulated(McpCall call)
        {
            if (!McpEditor.WaitFor(call, () => ViewerSettled() || !Send.Connected, ViewerReadyTimeout, "Waiting for the viewport to finish loading"))
                throw new McpError("The viewport is still loading after " + (int)ViewerReadyTimeout.TotalMinutes + " minutes. Try again later (get_viewport_state shows when it is ready).");
            if (!McpEditor.UI(() => ViewerSettled()))
                throw new McpError("The viewport disconnected while loading (it may have crashed): get_viewport_state with log_lines shows its output.");
        }

        /// <summary>
        /// A preview takes Animation Mode from an editor window - and, in the game, the game's animation drive from runtime_utils
        /// animate too (a viewport preview leaves the drive alone). UI thread.
        /// </summary>
        private static void RequireAnimationModeFree(bool inGame)
        {
            if (AnimationModeSession.Active)
                throw new McpError("Animation Mode is on in a CAGEAnimation editor window; a preview would take it over. Turn it off there first.");
            if (inGame && LiveLinkAnimationDrive.Active)
                throw new McpError("The game is driving an animation for runtime_utils animate; a preview would take the drive over. Give it back first: runtime_utils {action: 'animate', release: true}.");
        }

        /// <summary>'time' or 'times' (up to 9), each checked.</summary>
        private static List<float> PreviewTimes(McpCall call)
        {
            if (call.Has("time") && call.Has("times"))
                throw new McpError("Give 'time' or 'times', not both.");
            if (call.Has("times"))
            {
                JArray array = call.Array("times");
                if (array.Count == 0 || array.Count > 9)
                    throw new McpError("'times' takes 1 to 9 times (seconds).");
                return array.Select((o, i) => ReadTime(o, "times[" + i + "]")).ToList();
            }
            if (!call.Has("time"))
                throw new McpError("'time' (seconds into the animation) is required, or 'times' for several frames.");
            return new List<float>() { ReadTime(call.Token("time"), "time") };
        }

        /// <summary>
        /// The camera a preview looks through or activates, as a path stored from the animation's composite: the one given, else the
        /// CameraResource the animation moves, else the one an event track binds. Null when there is none. UI thread.
        /// </summary>
        private static EntityPath CameraPathOf(Commands commands, Composite composite, CAGEAnimation animation, McpCall call, out string name)
        {
            name = null;
            if (call.Has("camera"))
            {
                Target target = ResolveTarget(commands, composite, call.Token("camera"), false, "camera");
                if (KindOf(StandsFor(commands, target.Composite, target.Entity)) != "CameraResource")
                    throw new McpError("'camera': " + target.Name + " is a " + McpScript.TypeName(commands, target.Composite, target.Entity) + ", not a CameraResource.");
                name = target.Name;
                return target.Path;
            }
            HashSet<ShortGuid> floatIds = new HashSet<ShortGuid>(animation.floatTracks.Where(o => o != null).Select(o => o.shortGUID));
            foreach (bool moved in new[] { true, false })
            {
                foreach (CAGEAnimation.Connection connection in animation.connections)
                {
                    if (connection == null || floatIds.Contains(connection.target_track) != moved) continue;
                    if (moved && connection.target_param != PositionParam) continue;
                    (Composite c, Entity e) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(connection.connectedEntity, composite));
                    if (KindOf(e) != "CameraResource") continue;
                    name = McpScript.EntityName(commands, c, e);
                    return connection.connectedEntity;
                }
            }
            return null;
        }

        private static object Preview(McpCall call)
        {
            List<float> times = PreviewTimes(call);
            string view = (call.Str("view") ?? "viewport").Trim().ToLowerInvariant();
            if (view != "viewport" && view != "camera")
                throw new McpError("'view' is viewport (the viewport's own camera) or camera (through the animated camera).");
            if (ReadFlag(call.Token("in_game"), "in_game"))
            {
                if (times.Count != 1)
                    throw new McpError("in_game takes one 'time': the game holds the animation there for its frame.");
                if (call.Has("view"))
                    throw new McpError("'view' is for the viewport; in_game takes the game's own view (activate_camera: true switches it to the animated camera).");
                return PreviewInGame(call, times[0]);
            }
            if (call.Has("instance_path") || call.Has("screenshot") || call.Has("activate_camera"))
                throw new McpError("'instance_path', 'screenshot' and 'activate_camera' are for in_game.");
            if (call.Has("camera") && view != "camera")
                throw new McpError("'camera' says which camera to look through: add view: 'camera'.");
            PreviewHost host = null;
            int populateEvents = -1;
            string animationName = null;
            EntityPath cameraPath = null;
            string cameraName = null;
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                if (!Singleton.ViewportEnabled)
                    throw new McpError("The viewport is turned off. viewport_action {action: 'enable'} turns it on (Options > Viewport > Enable Viewport); or preview in the running game with in_game: true.");
                RequireAnimationModeFree(inGame: false);
                CAGEAnimation animation = FindAnimation(content.Level.Commands, call, out Composite composite);
                animationName = McpScript.EntityName(content.Level.Commands, composite, animation);
                if (view == "camera")
                {
                    cameraPath = CameraPathOf(content.Level.Commands, composite, animation, call, out cameraName);
                    if (cameraPath == null)
                        throw new McpError(animationName + " moves and binds no CameraResource, so there is no camera to look through. Pass 'camera' (a path from " + composite.name + "), or use view 'viewport'.");
                }
                CompositeDisplay display = Singleton.Editor.CompositeDisplay;
                if (display == null || display.IsDisposed || !display.Populated || display.Composite != composite)
                {
                    populateEvents = ViewerPopulateSync.PopulateEvents;
                    McpScriptEdit.Show(composite, null);
                }
                Singleton.Editor.CompositeDisplay?.ShowLevelViewerPanel(true);
                host = new PreviewHost()
                {
                    Animation = animation.Copy(),
                    AnimationComposite = composite,
                    AnimationContent = content,
                    AnimationLength = EffectiveLength(animation),
                    BezierInterpolation = InterpolationOf(animation.floatTracks) == CAGEAnimation.InterpolationMode.Bezier,
                };
            });

            //Posed only once the viewer has the composite, and Animation Mode held just for the moment of the picture:
            //while it is on, the user's own inspector and viewport edits become keyframes of the throwaway copy
            AwaitViewerSettled(call, populateEvents);
            //Looking through the camera moves the viewport's: where it was is put back afterwards
            McpViewportTools.ViewportCamera before = view == "camera" ? McpViewportTools.QueryCamera(call) : null;

            int columns = times.Count <= 3 ? times.Count : times.Count == 4 ? 2 : 3;
            int maxWidth = Math.Max(64, call.Int("max_width", 1024));
            List<McpImage> frames = new List<McpImage>();
            List<string> looked = new List<string>();
            try
            {
                AnimationModeSession session = null;
                for (int i = 0; i < times.Count; i++)
                {
                    float time = times[i];
                    McpEditor.UI(() =>
                    {
                        if (session == null)
                        {
                            if (AnimationModeSession.Active)
                                throw new McpError("Animation Mode was turned on in a CAGEAnimation editor window meanwhile; try again when it is off.");
                            CompositeDisplay display = Singleton.Editor.CompositeDisplay;
                            if (display == null || display.IsDisposed || display.Composite != host.AnimationComposite)
                                throw new McpError("The editor moved to another composite before the preview could be posed; try again.");
                            //The placement being previewed is the one the editor walked into, as Animation Mode takes it
                            Composite root = display.Path?.AllComposites.FirstOrDefault() ?? host.AnimationComposite;
                            List<uint> drill = display.Path?.AllEntities.Select(o => o.shortGUID.AsUInt32).ToList() ?? new List<uint>();
                            session = AnimationModeSession.Begin(host, root, drill);
                        }
                        session?.SetTime(time);
                    });
                    Thread.Sleep(i == 0 ? 600 : 300);

                    //A populate started since then drops the pose with its scene: wait it out, and pose again just before the picture
                    AwaitViewerPopulated(call);
                    List<uint> through = null;
                    McpEditor.UI(() =>
                    {
                        if (session == null || !ReferenceEquals(AnimationModeSession.Current, session))
                            throw new McpError("Animation Mode was turned on in a CAGEAnimation editor window meanwhile; try again when it is off.");
                        CompositeDisplay display = Singleton.Editor.CompositeDisplay;
                        if (display == null || display.IsDisposed || display.Composite != host.AnimationComposite)
                            throw new McpError("The editor moved to another composite before the picture was taken; try again.");
                        session.Refresh();
                        if (cameraPath != null)
                        {
                            //The camera's instance path from the composite the viewport shows: the editor's drill, then the camera's own steps
                            List<uint> drill = display.Path?.AllEntities.Select(o => o.shortGUID.AsUInt32).ToList() ?? new List<uint>();
                            if (!EntityInstancePath.TryResolve(host.AnimationContent.Level.Commands, host.AnimationComposite, drill, cameraPath, out through, out bool relative))
                                throw new McpError("The camera's path does not resolve from " + host.AnimationComposite.name + ".");
                            Composite scene = McpViewportTools.SceneComposite();
                            Composite levelRoot = host.AnimationContent.Level.Commands.EntryPoints[0];
                            if (!relative && scene != levelRoot)
                                throw new McpError("The camera's path starts in the level's root, but the viewport shows " + scene?.name + ": open the level's root composite and try again.");
                        }
                    });
                    if (through != null)
                    {
                        McpViewportTools.ViewportCamera seen = McpViewportTools.LookThrough(call, through);
                        if (looked.Count == 0) looked.Add(cameraName + " (fov " + Math.Round(seen.Fov, 1) + ")");
                    }

                    McpTool capture = McpTools.Find("capture_viewport");
                    if (capture == null)
                        throw new McpError("The viewport capture tool is not available.");
                    //The viewer was waited for above: capture's own wait would only hold the pose (and Animation Mode) up longer
                    JObject args = new JObject() { ["wait"] = false };
                    if (times.Count == 1) { if (call.Has("max_width")) args["max_width"] = call.Int("max_width"); }
                    else args["max_width"] = Math.Max(64, maxWidth / columns);
                    McpCall inner = new McpCall(capture, args, call.Cancel, (text, done, total) => call.Progress(text, done, total));
                    object picture = capture.Run(inner);
                    foreach (string note in inner.Notes) call.Note(note);
                    if (!(picture is McpImage image))
                        return picture;
                    frames.Add(image);
                }
            }
            finally
            {
                string taken = null;
                try { McpEditor.UI(() => { AnimationModeSession.EndFor(host); taken = host.EditTaken; }); } catch { }
                if (taken != null)
                    call.Note("While the pose was up, a change made in the editor (" + taken + ") went into the preview's throwaway copy of the animation as a keyframe, and was not applied: make it again.");
                if (before != null)
                {
                    try { McpViewportTools.PlaceCamera(call, before.Position, before.Forward, before.Up, -1f); }
                    catch (McpError e) { call.Note("The viewport camera could not be put back where it was: " + e.Message); }
                }
            }

            string lookedThrough = looked.Count != 0 ? " through " + looked[0] : "";
            string past = times.Any(t => t > host.AnimationLength + Eps) ? " (past its end at " + R(host.AnimationLength) + " s it holds the last keys)" : "";
            if (frames.Count == 1)
            {
                frames[0].Caption = animationName + " posed at " + R(times[0]) + " s" + lookedThrough + past + ". " + frames[0].Caption;
                return frames[0];
            }
            return ContactSheet(frames, times, columns, animationName + " at " + string.Join(", ", times.Select(t => R(t) + " s")) + lookedThrough + past + ", left to right then down.");
        }

        /// <summary>Several captured frames side by side, each labelled with its time.</summary>
        private static McpImage ContactSheet(List<McpImage> frames, List<float> times, int columns, string caption)
        {
            List<System.Drawing.Bitmap> pictures = new List<System.Drawing.Bitmap>();
            try
            {
                foreach (McpImage frame in frames)
                    using (System.IO.MemoryStream stream = new System.IO.MemoryStream(frame.Data))
                        pictures.Add(new System.Drawing.Bitmap(stream));
                int width = pictures.Max(o => o.Width), height = pictures.Max(o => o.Height);
                int rows = (pictures.Count + columns - 1) / columns;
                using (System.Drawing.Bitmap sheet = new System.Drawing.Bitmap(width * columns, height * rows))
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(sheet))
                using (System.Drawing.Font font = new System.Drawing.Font("Segoe UI", Math.Max(9f, height / 18f), System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel))
                using (System.Drawing.SolidBrush shade = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(170, 0, 0, 0)))
                {
                    g.Clear(System.Drawing.Color.Black);
                    for (int i = 0; i < pictures.Count; i++)
                    {
                        int x = (i % columns) * width, y = (i / columns) * height;
                        g.DrawImage(pictures[i], x, y, pictures[i].Width, pictures[i].Height);
                        string label = R(times[i]).ToString(System.Globalization.CultureInfo.InvariantCulture) + " s";
                        System.Drawing.SizeF size = g.MeasureString(label, font);
                        g.FillRectangle(shade, x + 4, y + 4, size.Width + 8, size.Height + 4);
                        g.DrawString(label, font, System.Drawing.Brushes.White, x + 8, y + 6);
                    }
                    using (System.IO.MemoryStream output = new System.IO.MemoryStream())
                    {
                        sheet.Save(output, System.Drawing.Imaging.ImageFormat.Jpeg);
                        return new McpImage() { Data = output.ToArray(), MimeType = "image/jpeg", Caption = caption + " " + sheet.Width + "x" + sheet.Height + " pixels." };
                    }
                }
            }
            finally
            {
                foreach (System.Drawing.Bitmap picture in pictures) picture.Dispose();
            }
        }

        /// <summary>
        /// The ids of an instance_path, read against the script: composite instances (names or ids, or a result's {ids} object) from the
        /// level's root down to an instance of <paramref name="composite"/>. A step that is not an instance, or a path that ends at an
        /// instance of another composite, is refused here rather than sent to the game unchecked. UI thread.
        /// </summary>
        private static List<ShortGuid> InstancePathTo(Commands commands, Composite composite, JToken token)
        {
            Composite root = commands.EntryPoints[0];
            if (composite == root)
                throw McpError.Invalid("The animation is in the level's root composite, which is placed once: leave 'instance_path' out.");
            List<Entity> chain = McpScript.ChainFrom(commands, root, McpScript.PathSteps(token, "instance_path"));
            Composite current = root;
            for (int i = 0; i < chain.Count; i++)
            {
                Composite next = McpScript.InstancedComposite(commands, chain[i]);
                if (next == null)
                    throw McpError.Invalid("'instance_path' step " + i + " (" + McpScript.EntityName(commands, current, chain[i]) + " in " + current.name + ") is a " + McpScript.TypeName(commands, current, chain[i]) +
                        ", not a composite instance: the path runs through instances only, from the root down to an instance of " + composite.name + ".");
                current = next;
            }
            if (current != composite)
                throw McpError.Invalid("'instance_path' ends at " + (chain.Count == 0 ? "the root" : "an instance of " + current.name) + ", not of " + composite.name + " (where the animation is). get_placements {composite: '" + composite.name + "'} lists its placements; pass one's ids.");
            return chain.Select(o => o.shortGUID).ToList();
        }

        /// <summary>
        /// preview_cage_animation in_game: Animation Mode with "In game", held just for the picture - the running game holds
        /// the animation at the time (evaluating every track itself), the tool waits until a game frame shows it, takes the
        /// game's frame, and the mode's end gives the game its animation back.
        /// </summary>
        private static object PreviewInGame(McpCall call, float time)
        {
            //The placement asked for, read against the script before anything goes to the game
            List<ShortGuid> givenPath = null;
            if (call.Has("instance_path"))
                McpEditor.UI(() =>
                {
                    Commands script = McpEditor.RequireCommands(forEditing: false);
                    FindAnimation(script, call, out Composite animationComposite);
                    givenPath = InstancePathTo(script, animationComposite, call.Token("instance_path"));
                });
            McpLevelTools.RequireLiveLink();
            PreviewHost host = null;
            Commands commands = null;
            Composite root = null;
            List<uint> drill = null;
            LiveLinkAnimationDrive.Target target = null;
            (Composite composite, Entity entity, List<ShortGuid> path, string name) camera = (null, null, null, null);
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                RequireAnimationModeFree(inGame: true);
                commands = content.Level.Commands;
                CAGEAnimation animation = FindAnimation(commands, call, out Composite composite);
                //The placement: as given, else the one the editor walked into (as call_method takes it), else every placement
                CompositeDisplay display = Singleton.Editor.CompositeDisplay;
                bool shown = display != null && !display.IsDisposed && display.Composite == composite;
                List<ShortGuid> path = null;
                if (givenPath != null)
                    path = givenPath;
                else if (shown)
                    path = LiveLink.InstancePath(display, commands);
                //The viewer's half of the mode poses the same placement, if it shows the composite
                root = (shown ? display.Path?.AllComposites.FirstOrDefault() : null) ?? composite;
                drill = shown ? display.Path?.AllEntities.Select(o => o.shortGUID.AsUInt32).ToList() ?? new List<uint>() : new List<uint>();
                host = new PreviewHost()
                {
                    Animation = animation.Copy(),
                    AnimationComposite = composite,
                    AnimationContent = content,
                    AnimationLength = EffectiveLength(animation),
                    BezierInterpolation = InterpolationOf(animation.floatTracks) == CAGEAnimation.InterpolationMode.Bezier,
                };
                target = new LiveLinkAnimationDrive.Target()
                {
                    Root = LiveLink.RootOf(commands),
                    Composite = composite.shortGUID,
                    Entity = animation.shortGUID,
                    Path = path ?? new List<ShortGuid>(),
                    Label = McpScript.EntityName(commands, composite, animation),
                };
                if (ReadFlag(call.Token("activate_camera"), "activate_camera"))
                {
                    EntityPath stored = CameraPathOf(commands, composite, animation, call, out string cameraName);
                    if (stored == null)
                        throw new McpError(target.Label + " moves and binds no CameraResource. Pass 'camera' (a path from " + composite.name + "), or leave activate_camera out.");
                    List<Tuple<Composite, Entity>> chain = commands.Utils.ResolveEntityPath(stored, composite);
                    (Composite cameraComposite, Entity cameraEntity) = commands.Utils.GetResolvedTarget(chain);
                    //The camera's placement: the animation's, then the instances its path steps through (a path from the root is its own)
                    List<ShortGuid> through = chain.Take(chain.Count - 1).Select(o => o.Item2.shortGUID).ToList();
                    bool relative = chain[0].Item1 == composite;
                    if (relative && (path == null || path.Count == 0) && through.Count != 0)
                        throw new McpError(cameraName + " is inside an instance in " + composite.name + ": pass 'instance_path' (the animation's placement) so the game knows which copy of it to activate.");
                    camera = (cameraComposite, cameraEntity, relative ? (path ?? new List<ShortGuid>()).Concat(through).ToList() : through, cameraName);
                }
            });
            McpLevelTools.RequireRunningLevel(commands);

            bool activated = false;
            try
            {
                if (camera.entity != null)
                {
                    LiveLink.Reply reply = McpEditor.UI(() => LiveLink.CallAfterEdits(commands, camera.composite, camera.entity, ActivateCameraPin, camera.path)).Result;
                    if (!reply.Ok)
                        throw new McpError("The game could not activate " + camera.name + ": " + reply.Message);
                    activated = true;
                }
                uint sequence = 0;
                McpEditor.UI(() =>
                {
                    RequireAnimationModeFree(inGame: true);
                    AnimationModeSession session = AnimationModeSession.Begin(host, root, drill, target);
                    session?.SetTime(time);
                    sequence = LiveLinkAnimationDrive.Sequence;
                });
                LiveLink.GameAnimation game = McpLevelTools.AwaitGameAnimation(call, sequence, TimeSpan.FromSeconds(15));

                string caption = target.Label + " held in the game at " + R((float)game.Time) + " s"
                    + (time > game.Length ? " (asked for " + R(time) + " s: the game holds it just short of its end at " + R((float)game.Length) + " s)" : "")
                    + (game.Found > 1 ? ", in " + game.Applied + " of " + game.Found + " placements" : "")
                    + ".";
                if (call.Has("screenshot") && !ReadFlag(call.Token("screenshot"), "screenshot"))
                    return new JObject() { ["result"] = caption, ["game"] = McpLevelTools.DescribeGameAnimation(game) };

                //The game writes its frame to a file before it answers
                string file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "OpenCAGE_anim_" + Guid.NewGuid().ToString("N") + ".bmp");
                try
                {
                    LiveLink.Reply shot = LiveLink.Screenshot(file).Result;
                    if (!shot.Ok)
                        throw new McpError("The game held it, but could not take a screenshot: " + shot.Message);
                    int maxWidth = Math.Max(64, call.Int("max_width", 1024));
                    using (System.Drawing.Bitmap frame = new System.Drawing.Bitmap(file))
                    {
                        float scale = Math.Min(1f, (float)maxWidth / frame.Width);
                        using (System.Drawing.Bitmap picture = new System.Drawing.Bitmap(frame, Math.Max(1, (int)(frame.Width * scale)), Math.Max(1, (int)(frame.Height * scale))))
                        using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
                        {
                            picture.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
                            return new McpImage()
                            {
                                Data = stream.ToArray(),
                                MimeType = "image/jpeg",
                                Caption = caption + " The game's frame, " + picture.Width + "x" + picture.Height + " pixels.",
                            };
                        }
                    }
                }
                finally
                {
                    try { System.IO.File.Delete(file); } catch { }
                }
            }
            finally
            {
                string taken = null;
                try { McpEditor.UI(() => { AnimationModeSession.EndFor(host); taken = host.EditTaken; }); } catch { }
                //Given back before the result, so a tool after this one finds the game's own animation
                try { McpEditor.WaitFor(null, () => !LiveLinkAnimationDrive.Releasing, TimeSpan.FromSeconds(3), ""); } catch { }
                if (taken != null)
                    call.Note("While the animation was held, a change made in the editor (" + taken + ") went into the preview's throwaway copy of the animation as a keyframe, and was not applied: make it again.");
                if (activated)
                {
                    try
                    {
                        LiveLink.Reply released = McpEditor.UI(() => LiveLink.CallAfterEdits(commands, camera.composite, camera.entity, DeactivateCameraPin, camera.path)).Result;
                        if (!released.Ok) call.Note("The game could not deactivate " + camera.name + " afterwards (" + released.Message + "): call_method deactivate_camera on it hands the view back.");
                    }
                    catch (Exception e) { call.Note("The game could not deactivate " + camera.name + " afterwards (" + e.Message + ")."); }
                }
            }
        }
        #endregion

        #region set_animated_model
        private static bool HasResourceParameter(Commands commands, FunctionType function)
        {
            //EntityInspector.FunctionHasResourceParameter: the type declares an internal 'resource'
            foreach ((ShortGuid name, ParameterVariant variant, DataType type) in commands.Utils.GetAllParameters(function))
                if (name == ShortGuids.resource && variant == ParameterVariant.INTERNAL && type == DataType.RESOURCE)
                    return true;
            return false;
        }

        /// <summary>One entry as every tool describes it (McpAnimationAssetTools.DescribeEntry): id, rig, bones, meshes, bind poses, helpers, set, users.</summary>
        private static JObject DescribeEnvironmentAnimation(EnvironmentAnimations.EnvironmentAnimation entry, Commands commands = null)
        {
            return McpAnimationAssetTools.DescribeEntry(entry, commands);
        }

        private static object SetAnimatedModel(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                bool writing = call.Has("entry");
                LevelContent content = McpEditor.RequireLevel(forEditing: writing);
                Level level = content.Level;
                Commands commands = level.Commands;
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity", required: true));
                string name = McpScript.EntityName(commands, composite, entity);
                if (!(entity is FunctionEntity function) || !function.function.IsFunctionType)
                    throw new McpError(name + " is a " + McpScript.TypeName(commands, composite, entity) + "; only function entities carry resources.");
                FunctionType type = function.function.AsFunctionType;
                if (EntityInspector.FunctionResourcesAreGenerated(type))
                    throw new McpError(type + " entities have their resources generated from their parameters when the level is saved; they are not set by hand.");
                if (EntityInspector.FunctionIsMarkerResourceOnly(type))
                    throw new McpError(type + " entities carry only a marker resource, which is managed for them; they take no animated model.");
                bool parameterMode = HasResourceParameter(commands, type);
                if (!parameterMode && type != FunctionType.PhysicsSystem && (function.resources == null || function.resources.Count == 0))
                    throw new McpError(type + " entities do not take resources.");
                //Retail gives ANIMATED_MODEL to EnvironmentModelReference alone: a ModelReference or PhysicsSystem never plays one
                if (type != FunctionType.EnvironmentModelReference && writing && !call.Bool("allow_custom"))
                {
                    FunctionEntity emr = composite.functions.FirstOrDefault(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.EnvironmentModelReference);
                    throw new McpError(name + " is a " + type + "; an ANIMATED_MODEL plays on a composite's EnvironmentModelReference" + (emr != null ? " (" + McpScript.EntityName(commands, composite, emr) + " here)" : " (this composite has none)")
                        + ". To animate an unrigged prop, key its ModelReference's position or rotation with a CAGEAnimation (animate_parameters). Pass allow_custom:true to set it here anyway.");
                }

                List<ResourceReference> current = parameterMode
                    ? (function.GetParameter(ShortGuids.resource)?.content as cResource)?.value ?? new List<ResourceReference>()
                    : function.resources ?? new List<ResourceReference>();
                ResourceReference animated = current.FirstOrDefault(o => o != null && o.resource_type == ResourceType.ANIMATED_MODEL);
                List<EnvironmentAnimations.EnvironmentAnimation> entries = level.EnvironmentAnimations?.Entries ?? new List<EnvironmentAnimations.EnvironmentAnimation>();
                JObject result = new JObject()
                {
                    ["composite"] = composite.name,
                    ["entity"] = new JObject() { ["id"] = McpScript.Id(entity.shortGUID), ["name"] = name, ["type"] = type.ToString() },
                };

                //Every placement of the composite shares one entity, so a change here reaches all of them
                int instances = commands.Entries.Sum(c => c.functions.Count(f => !f.function.IsFunctionType && f.function == composite.shortGUID));
                if (!writing)
                {
                    result["animated_model"] = animated == null ? JValue.CreateNull() : (JToken)DescribeEnvironmentAnimation(animated.AnimatedModel, commands) ?? "(reference with no entry)";
                    if (animated?.AnimatedModel != null)
                    {
                        int moved = McpAnimationAssetTools.PartsMoved(composite, animated.AnimatedModel, out int parts);
                        result["parts_moved"] = moved + " of " + parts;
                    }
                    result["composite_instances"] = instances;
                    result["entry_count"] = entries.Count;
                    McpPaging.Page(call, entries.Where(o => o != null).OrderBy(o => o.ID).ToList(), result, "entries", o => DescribeEnvironmentAnimation(o), 50);
                    result["unchanged"] = "Only a report: give 'entry' to change it.";
                    return result;
                }

                McpEditor.RequireUndoIdle();
                string entry = call.Str("entry").Trim();
                bool remove = string.Equals(entry, "none", StringComparison.OrdinalIgnoreCase);
                bool makeNew = string.Equals(entry, "new", StringComparison.OrdinalIgnoreCase);
                string skeletonName = call.Str("skeleton")?.Trim();
                if (skeletonName != null && !makeNew) throw new McpError("'skeleton' goes with entry 'new': it builds the new entry for that rig.");
                EnvironmentAnimations.EnvironmentAnimation chosen = null;
                if (remove)
                {
                    //The save gives every EnvironmentModelReference an ANIMATED_MODEL again, with no entry behind it
                    if (type == FunctionType.EnvironmentModelReference)
                        throw new McpError("An EnvironmentModelReference always keeps an ANIMATED_MODEL (saving gives it one back, with nothing behind it). Point it at another entry, or delete the entity (delete_entities) if the prop shouldn't animate.");
                    if (animated == null)
                        throw new McpError(name + " has no ANIMATED_MODEL reference to remove.");
                }
                else if (makeNew)
                {
                    if (level.EnvironmentAnimations == null || entries.Count == 0)
                        throw new McpError("This level has no environment animations, so an ANIMATED_MODEL cannot be added (the editor refuses the same).");
                }
                else if (int.TryParse(entry, out int id))
                {
                    chosen = entries.FirstOrDefault(o => o != null && o.ID == id);
                    if (chosen == null)
                        throw new McpError("The level has no environment-animation entry " + id + ". Its entries: " + string.Join(", ", entries.Where(o => o != null).OrderBy(o => o.ID).Take(40).Select(o => o.ID + (string.IsNullOrEmpty(o.SkeletonName) ? "" : " (" + o.SkeletonName + ")"))) + (entries.Count > 40 ? ", ..." : "") + ".");
                    if (animated != null && ReferenceEquals(animated.AnimatedModel, chosen))
                    {
                        call.Note(name + " already uses entry " + id + "; nothing changed.");
                        result["animated_model"] = DescribeEnvironmentAnimation(chosen, commands);
                        result["changed"] = false;
                        return result;
                    }
                }
                else
                    throw new McpError("'entry' is an environment-animation entry id or 'new'" + (type == FunctionType.EnvironmentModelReference ? "" : ", or 'none'") + ".");

                if (makeNew && skeletonName != null)
                {
                    //A full entry for a retail environment rig, its parts in the order the composite draws them, as import_model builds one
                    CathodeLib.Animation animations = Singleton.Animations;
                    CathodeLib.Animation.SkeletonAsset asset = animations?.GetSkeleton(skeletonName);
                    Skeleton rig = asset?.Skeleton ?? asset?.Skeleton64;
                    if (rig == null || rig.Bones.Count == 0)
                        throw new McpError("There is no rig '" + skeletonName + "' in ANIMATION.PAK" + (animations == null ? "." : McpGlobalAssetChecks.DidYouMean(animations.Skeletons.Select(o => o.ToString()), skeletonName, " (list_skeletons lists them).")));
                    if (animations.SkeletonDefs.TryGetValue(rig.Name, out CathodeLib.Animation.SkeletonDef def) && !def.IsEnvironment)
                        throw new McpError(rig.Name + " is a character rig; an EnvironmentModelReference plays an environment (prop) rig's sets.");
                    List<ShortGuid> renderables = composite.functions.Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.ModelReference)
                        .SelectMany(o => ((o.GetParameter(ShortGuids.resource)?.content as cResource)?.value ?? new List<ResourceReference>()).Where(r => r != null && r.resource_type == ResourceType.RENDERABLE_INSTANCE).Select(r => r.resource_id))
                        .Distinct().ToList();
                    string how;
                    chosen = level.EnvironmentAnimations.AddForSkeleton(animations.SkeletonIndex?.GetSkeleton(rig.Name)?.Name ?? rig.Name, rig, animations, ModelCompositeBuilder.OtherLevels(level), renderables, out how);
                    call.Note("Entry " + chosen.ID + " was built for " + rig.Name + " (" + how + ") over the composite's " + renderables.Count + " ModelReference part(s); undo takes the reference away but leaves the entry.");
                }
                else if (makeNew)
                {
                    //As the resource editor's Add New Reference does: an empty entry with a fresh id, in the level's table
                    //Empty but complete: the save writes every one of these lists (and hashes the skeleton name), so none may be null
                    chosen = new EnvironmentAnimations.EnvironmentAnimation()
                    {
                        Matrix = System.Numerics.Matrix4x4.Identity,
                        SkeletonName = "",
                        BoneMappings = new List<ShortGuid>(),
                        MeshMappings = new List<ShortGuid>(),
                        InverseBindPoses = new List<System.Numerics.Matrix4x4>(),
                        HavokToCathodeMappings = new List<System.Numerics.Matrix4x4>(),
                        HelperMatrices = new List<EnvironmentAnimations.WeightedHelperData>(),
                    };
                    chosen.ID = level.EnvironmentAnimations.AllocateUniqueId();
                    level.EnvironmentAnimations.Entries.Add(chosen);
                    call.Note("Entry " + chosen.ID + " was added to the level's environment animations empty (no rig or mappings), as the editor adds one: it moves nothing until filled (entry 'new' with skeleton builds a working one). Undo takes the reference away but leaves the entry.");
                }

                string label = "AI: Edit resources of " + UndoLabels.Entity(composite, function);
                if (parameterMode)
                {
                    Parameter existing = function.GetParameter(ShortGuids.resource);
                    bool had = existing != null;
                    int index = had ? function.parameters.IndexOf(existing) : function.parameters.Count;
                    ParameterData beforeContent = ParameterValues.Clone(existing?.content);
                    ParameterVariant beforeVariant = existing?.variant ?? ParameterVariant.INTERNAL;
                    cResource after = existing?.content is cResource live ? (cResource)ParameterValues.Clone(live) : new cResource(function.shortGUID);
                    ChangeAnimatedModel(after.value, after.shortGUID, chosen, remove);
                    Parameter parameter = existing ?? new Parameter(ShortGuids.resource, new cResource(function.shortGUID), ParameterVariant.INTERNAL);
                    UndoStack.Current.Apply(new ResourceSessionEdit(composite, function, parameter, had, index, beforeContent, beforeVariant, after, label));
                }
                else
                {
                    List<ResourceReference> before = ResourceSessionEdit.CloneReferences(function.resources);
                    List<ResourceReference> after = ResourceSessionEdit.CloneReferences(function.resources);
                    ChangeAnimatedModel(after, function.shortGUID, chosen, remove);
                    UndoStack.Current.Apply(new ResourceSessionEdit(composite, function, before, after, label));
                }
                DirtyTracker.MarkLevelDataModified();
                McpScriptEdit.Show(composite, new List<Entity>() { function });

                result["changed"] = true;
                result["animated_model"] = remove ? JValue.CreateNull() : (JToken)DescribeEnvironmentAnimation(chosen, commands);
                if (!remove)
                {
                    //Whether the entry's bones reach this composite's parts: an entry made for another prop moves nothing here
                    int moved = McpAnimationAssetTools.PartsMoved(composite, chosen, out int parts);
                    result["parts_moved"] = moved + " of " + parts;
                    if (moved == 0 && parts != 0 && (chosen.BoneMappings?.Count ?? 0) != 0)
                        call.Note("Entry " + chosen.ID + "'s bones name none of " + composite.name + "'s " + parts + " drawn parts, so nothing here will move: it was made for another prop (see used_by). describe_animated_prop shows what an entry drives.");
                }
                result["composite_instances"] = instances;
                if (instances > 1) call.Note("This changes every one of the " + instances + " instances of " + composite.name + ".");
                return result;
            });
        }

        /// <summary>Point the list's ANIMATED_MODEL reference at an entry (adding one if missing), or drop it. The list is a copy.</summary>
        private static void ChangeAnimatedModel(List<ResourceReference> references, ShortGuid resourceId, EnvironmentAnimations.EnvironmentAnimation entry, bool remove)
        {
            int at = references.FindIndex(o => o != null && o.resource_type == ResourceType.ANIMATED_MODEL);
            if (remove)
            {
                if (at >= 0) references.RemoveAt(at);
                return;
            }
            if (at >= 0)
                references[at].AnimatedModel = entry;
            else
                references.Add(new ResourceReference(ResourceType.ANIMATED_MODEL) { resource_id = resourceId, AnimatedModel = entry });
        }
        #endregion
    }
}
