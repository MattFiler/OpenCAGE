using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.Scripting.Refactor;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.DockPanels;
using OpenCAGE.Undo;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;

namespace OpenCAGE.MCP
{
    /// <summary>Changing the open level's script: composites, entities, parameters, links, pins, and the refactors.</summary>
    /// <remarks>
    /// Each script tool is a step (<c>Step...</c>) that works on an <see cref="McpScriptEdit"/>: a call runs one step as one
    /// undo step, and script_batch runs several as one, sharing their refs. World-space arguments go through one placement
    /// of the composite (<see cref="FrameFor"/>), and nested entities are reached through an alias found or made on the path.
    /// </remarks>
    internal static class McpScriptTools
    {
        #region Schemas
        private const string SpaceHelp = "'composite' (default): position and rotation are relative to the composite the entity is in. 'world': they are in world space (the level's), converted for one placement of the composite (see placement)";
        private const string PlacementHelp = "With space 'world', which placement of the composite to convert through: its number (0-based, in get_placements order; needed when the composite is placed more than once) or the path of instance ids/names from the level root to that placement's instance";

        private static JObject SpaceSchema() => new JObject() { ["type"] = "string", ["enum"] = new JArray("composite", "world"), ["description"] = SpaceHelp + "." };

        //A path as results give one back: {ids} or {path, ids} (a get_placements placement, say), ids read first
        private static JObject PathObject() => new JObject()
        {
            ["type"] = "object",
            ["properties"] = new JObject()
            {
                ["ids"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" } },
                ["path"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" } },
            },
            ["additionalProperties"] = true,
        };

        private static JObject PlacementSchema() => new JObject()
        {
            ["description"] = PlacementHelp + ".",
            ["anyOf"] = new JArray(new JObject() { ["type"] = "integer" }, new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" } }, new JObject() { ["type"] = "string" }, PathObject()),
        };

        private static readonly JObject EntryPath = new JObject()
        {
            ["description"] = "Entity ids/names through instances to the entity, as an array (or one string split on '/'), or a result's {ids} object.",
            ["anyOf"] = new JArray(new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" } }, new JObject() { ["type"] = "string" }, PathObject()),
        };

        //A rotation built for you: face a point, or along a direction (with roll), instead of [pitch, yaw, roll]
        private const string AimHelp = "Instead of rotation: look_at [x, y, z] (a point to face from the position, in the same space) or forward [x, y, z] (a direction to face), with roll (degrees) - an entity faces its +Z, which is where a spot light shines and a camera looks";

        private static JObject AimObject() => new JObject()
        {
            ["type"] = "object",
            ["properties"] = new JObject()
            {
                ["look_at"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" } },
                ["forward"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" } },
                ["roll"] = new JObject() { ["type"] = "number" },
            },
        };

        private static JObject Path(string description)
        {
            JObject path = (JObject)EntryPath.DeepClone();
            path["description"] = description;
            return path;
        }

        private static readonly JObject SequenceEntry = new JObject()
        {
            ["type"] = "object",
            ["properties"] = new JObject()
            {
                ["path"] = EntryPath,
                ["delay"] = new JObject() { ["type"] = "number", ["description"] = "Seconds to wait before triggering it." },
                ["from_root"] = new JObject() { ["type"] = "boolean", ["description"] = "The path starts at the level's root composite, not the sequence's composite." },
            },
            ["required"] = new JArray("path"),
        };

        private static readonly JObject EntitySpec = new JObject()
        {
            ["type"] = "object",
            ["description"] = "One entity to make. Give exactly one of function, composite, variable, alias or proxy.",
            ["properties"] = new JObject()
            {
                ["ref"] = new JObject() { ["type"] = "string", ["description"] = "A handle for this entity, for links (and, in script_batch, later steps) to refer to it." },
                ["function"] = new JObject() { ["type"] = "string", ["description"] = "A built-in type (FunctionType), e.g. 'ThinkOnce', 'Character', 'PlayerTriggerBox', 'ModelReference' (list_function_types lists them)." },
                ["composite"] = new JObject() { ["type"] = "string", ["description"] = "Place an instance of this composite (path or id)." },
                ["variable"] = new JObject()
                {
                    ["type"] = "object",
                    ["description"] = "A variable: a pin of this composite, which its instances show as a parameter and link point.",
                    ["properties"] = new JObject()
                    {
                        ["name"] = new JObject() { ["type"] = "string" },
                        ["pin"] = new JObject()
                        {
                            ["type"] = "string",
                            ["description"] = "method: an event coming in - outside, X.relay -> instance.<pin>; inside, <variable> -> inner.method. target: an event going out - inside, inner.relay -> <variable>; outside, instance.<pin> -> X.method. " +
                                "reference: an entity passed in. input_/output_ + bool, int, float, string, position, direction, enum, enum_string, object, animation_info, zone, zone_link: a value passed in or out (e.g. 'input_float'); " +
                                "inside, the entity that uses an input links from its pin to the variable.",
                        },
                        ["enum_type"] = new JObject() { ["type"] = "string", ["description"] = "For enum pins: the enum's name; for enum_string pins: the enum string type." },
                    },
                    ["required"] = new JArray("name", "pin"),
                },
                ["alias"] = Path("An alias: the path of entity ids/names from this composite, through instances, to an entity inside a nested instance. It overrides that entity's parameters in this composite's placements, and is the handle for linking to it from here. One already on that path is reused ('reused': true) unless 'new'."),
                ["proxy"] = Path("A proxy: the path of entity ids/names from the level's root composite, through instances, to a function entity anywhere (a result's {ids} object too) - the handle for linking to it from a composite that does not hold it. One already on that path is reused unless 'new'."),
                ["new"] = new JObject() { ["type"] = "boolean", ["description"] = "alias/proxy: make another even when one is on that path already." },
                ["name"] = new JObject() { ["type"] = "string", ["description"] = "Its name (default: the type, numbered)." },
                ["position"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["description"] = "[x, y, z] metres, Y up: relative to the composite, or world with space 'world'." },
                ["rotation"] = new JObject()
                {
                    ["description"] = McpSchema.RotationText + ". Or {look_at | forward, roll?}.",
                    ["anyOf"] = new JArray(new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" } }, AimObject()),
                },
                ["look_at"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["description"] = AimHelp + "." },
                ["forward"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["description"] = "Instead of rotation: the direction [x, y, z] to face (see look_at)." },
                ["roll"] = new JObject() { ["type"] = "number", ["description"] = "With look_at or forward: degrees of roll about the facing direction (default 0)." },
                ["space"] = SpaceSchema(),
                ["placement"] = PlacementSchema(),
                ["sequence"] = new JObject()
                {
                    ["type"] = "object",
                    ["description"] = "A TriggerSequence's contents (as set_trigger_sequence takes them), set before this call's links are made, so links to its method pins work.",
                    ["properties"] = new JObject()
                    {
                        ["entries"] = new JObject() { ["type"] = "array", ["items"] = SequenceEntry, ["description"] = "What it triggers, in order." },
                        ["methods"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" }, ["description"] = "Method names it takes (calling one on the sequence calls it on each entry in turn)." },
                    },
                },
                ["node"] = new JObject()
                {
                    ["type"] = "object",
                    ["description"] = "Its flowgraph node: {page?, x?, y?}. By default a linked entity is drawn beside what it joins and an unlinked one in a column beside the page's nodes.",
                    ["properties"] = new JObject() { ["page"] = new JObject() { ["type"] = "string" }, ["x"] = new JObject() { ["type"] = "number" }, ["y"] = new JObject() { ["type"] = "number" } },
                },
                ["parameters"] = new JObject() { ["type"] = "object", ["additionalProperties"] = true, ["description"] = "Parameter values by name (as set_parameters takes them)." },
            },
        };

        private const string LinkHelp = "A link, held by its source: from.param -> to.to_param. Events: A.relay -> B.method (ThinkOnce.on_think -> Sound.start; a method's relay such as 'triggered' fires when it runs). " +
            "Data: the entity that USES a value links from its own pin to what provides it (Checkpoint.player_spawn_position -> PositionMarker.reference, or -> a variable of the composite); links never run into a value pin. " +
            "So a value parameter with a link out of it takes its value from the other end (the last such link wins), not from the value set on it. " +
            "Links join entities of one composite: an entity inside a nested instance is reached through an alias (from_path / to_path), one elsewhere in the level through a proxy (with from_root / to_root). " +
            "To delay an event, put seconds on the target's method pin with set_parameters (e.g. {\"trigger\": 2}): it applies to every link into that pin.";

        private static JObject LinkSchema(bool perLinkComposite)
        {
            JObject properties = new JObject();
            if (perLinkComposite)
                properties["composite"] = new JObject() { ["type"] = "string", ["description"] = "The composite this link is in (default: the call's composite)." };
            properties["from"] = new JObject() { ["type"] = "string", ["description"] = "Source entity: a ref from this call, an id, or a name." };
            properties["from_path"] = Path("Instead of from: entity ids/names from the composite through instances to a nested entity (an alias of it here is found or made), or with from_root, from the level's root (a proxy is found or made).");
            properties["from_root"] = new JObject() { ["type"] = "boolean", ["description"] = "from_path starts at the level's root composite." };
            properties["param"] = new JObject() { ["type"] = "string", ["description"] = "The source's pin: a relay or target pin for an event, or for data the pin of the entity that uses the value." };
            properties["to"] = new JObject() { ["type"] = "string", ["description"] = "Target entity: a ref, an id, or a name." };
            properties["to_path"] = Path("Instead of to: as from_path.");
            properties["to_root"] = new JObject() { ["type"] = "boolean", ["description"] = "to_path starts at the level's root composite." };
            properties["to_param"] = new JObject() { ["type"] = "string", ["description"] = "The target's pin: a method pin for an event (e.g. 'trigger', 'spawn'), or 'reference' (or a variable's pin) for data." };
            properties["allow_custom"] = new JObject() { ["type"] = "boolean", ["description"] = "Allow pins the entities do not normally have, and links the checks refuse." };
            return new JObject()
            {
                ["type"] = "object",
                ["description"] = LinkHelp,
                ["properties"] = properties,
                ["required"] = new JArray("param", "to_param"),
            };
        }

        private static readonly JObject ParameterChange = new JObject()
        {
            ["type"] = "object",
            ["properties"] = new JObject()
            {
                ["composite"] = new JObject() { ["type"] = "string", ["description"] = "Its composite (default: the call's)." },
                ["entity"] = new JObject() { ["type"] = "string", ["description"] = "The entity (id, name, or in script_batch a ref)." },
                ["path"] = Path("Instead of entity: ids/names from the composite through instances to a nested entity: the values go on an alias of it in the composite (found, or made)."),
                ["parameters"] = new JObject() { ["type"] = "object", ["additionalProperties"] = true, ["description"] = "Values by name (a transform may face a point: {\"position\": [...], \"look_at\": [x, y, z]})." },
                ["space"] = SpaceSchema(),
                ["placement"] = PlacementSchema(),
            },
            ["required"] = new JArray("parameters"),
        };

        private static McpSchema.Prop ShowProp() => McpSchema.Boolean("show", "Take the editor to the change and select it. Default: yes, unless the user has gone to another composite since your last change was shown (then the editor is left where they are, and a note says so); true always, false never.");
        #endregion

        public static IEnumerable<McpTool> Tools()
        {
            McpScriptEdit.Hook();

            yield return new McpTool()
            {
                Name = "create_entities",
                Title = "Create entities",
                Description = "Add entities to a composite - built-in functions, instances of composites, variables (pins), aliases, proxies - with names, positions and parameters, and optionally link them (to each other via 'ref', or to existing entities). " +
                    "Positions are relative to the composite, or world space with space 'world' (converted through one placement of it; the result gives each one's world position); look_at / forward build the rotation that faces a point or direction (an entity faces its +Z: a spot light shines and a camera looks along it). An alias or proxy already on the same path is reused. A TriggerSequence can be filled ('sequence') in the same call. " +
                    "A box volume (PlayerTriggerBox and the like) stands on its position: +-half_dimensions.x and .z about it, 0 to 2 x half_dimensions.y up, in its own rotation. " +
                    "A new CAGEAnimation has anim_length 10 until animate_parameters' first keys set it; a camera move is best made with create_camera_animation (camera, animation and trigger wired the retail way). " +
                    "One undo step; the flowgraph pages are updated to draw the new links. Returns the ids made, with notes on content no zone claims, composites placed more than once and what needs a Save & Build.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "Where to add them (path or id; 'root' for the level's root).", required: true),
                    McpSchema.Array("entities", "What to make.", EntitySpec, required: true),
                    McpSchema.Array("links", "Links to make once they exist.", LinkSchema(false)),
                    McpSchema.String("page", "The flowgraph page new links are drawn on when they cannot go beside what they join (default: the page last shown)."),
                    McpSchema.Nested("space", "Every entity's space unless it gives its own: " + SpaceHelp + ".", SpaceSchema()),
                    McpSchema.Nested("placement", PlacementHelp + ".", PlacementSchema()),
                    McpSchema.Boolean("allow_custom", "Allow parameters the entities do not normally have, values the editor derives itself, abstract types and links the checks refuse."),
                    ShowProp()),
                Run = call => RunOne(call, StepCreateEntities, CreateLabel(call), showByDefault: true, scriptView: true),
            };

            yield return new McpTool()
            {
                Name = "create_composite",
                Title = "Create composite",
                Description = "Make a new composite (a reusable script/prefab), optionally filling it (same entities/links format as create_entities, with the same checks and notes) and placing an instance of it in another composite. Give pins with 'variable' entities so instances can be configured and linked. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("path", "Its path, with backslash folders, e.g. 'MCP\\Corridor_Lights'.", required: true),
                    McpSchema.Array("entities", "Entities to put in it.", EntitySpec),
                    McpSchema.Array("links", "Links between them.", LinkSchema(false)),
                    McpSchema.String("page", "The name of the flowgraph page its content is drawn on (default: the composite's name)."),
                    McpSchema.String("place_in", "Also place an instance of it in this composite (e.g. 'root')."),
                    McpSchema.Position("position", "Where to place that instance (needs place_in), relative to place_in, or world with space 'world'"),
                    McpSchema.Rotation("rotation", "Its rotation (needs place_in)"),
                    McpSchema.Nested("space", "For position and rotation: " + SpaceHelp + " (here, place_in).", SpaceSchema()),
                    McpSchema.Nested("placement", PlacementHelp + " (here, place_in).", PlacementSchema()),
                    McpSchema.String("instance_name", "The instance's name (needs place_in)."),
                    McpSchema.Boolean("allow_custom", "Allow parameters the entities do not normally have, values the editor derives itself, abstract types and links the checks refuse."),
                    ShowProp()),
                Run = call => RunOne(call, StepCreateComposite, "Create composite " + McpScript.NormalisePath(call.Str("path", required: true)), showByDefault: true),
            };

            yield return new McpTool()
            {
                Name = "set_parameters",
                Title = "Set parameters",
                Description = "Set parameter values on entities. Values are JSON: true/false, numbers, strings, [x,y,z] vectors, {\"position\":[x,y,z],\"rotation\":[x,y,z]} transforms (rotation in degrees, x pitch, y yaw, z roll; give one part to keep the other), " +
                    "colours [r,g,b] 0-255, enum entry names (e.g. \"TELEPORT\"), a material mapping's name for 'mapping', a texture's name for an EnvironmentMap's Texture or a LightReference's gobo_texture, enum-string values as list_enum_string_values lists them (e.g. a LEVEL_NAME for a SwitchLevel's level_name). " +
                    "A number on a method or relay pin is that pin's delay in seconds, as the flowgraph's Set Delay (remove_parameters clears it). Setting 'name' renames. " +
                    "'path' reaches an entity inside nested instances: the values go on an alias of it in 'composite' (found, or made), so they change that composite's placements only - a per-placement override. " +
                    "Transforms can be given in world space (space 'world'), and can face a point or direction instead of giving a rotation ({\"position\": [...], \"look_at\": [x, y, z]}, or forward, with roll; an entity faces its +Z). " +
                    "Notes say when the game will not use a value (a link feeds it, an alias overrides it, an animation drives it). Each change gives placements_affected (how many placements of its composite it reaches), fed_by for values a link out of the parameter replaces (the game reads the other end), and needs_build when the game only takes it after save_level build=true (models, lights, effects, collision and the like; the viewport shows it at once). One undo step for all of them.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id; 'root' for the level's root). Each change may name its own instead."),
                    McpSchema.String("entity", "One entity (id or name) - or use 'path', or 'changes' for several."),
                    McpSchema.Nested("path", "Instead of entity: ids/names from 'composite' through instances to a nested entity; the values go on an alias of it in 'composite' (found, or made).", EntryPath),
                    McpSchema.Map("parameters", "Parameter values by name, for 'entity' or 'path'."),
                    McpSchema.Nested("space", "For transform values: " + SpaceHelp + ".", SpaceSchema()),
                    McpSchema.Nested("placement", PlacementHelp + ".", PlacementSchema()),
                    McpSchema.Array("changes", "Several entities at once, in one or more composites.", ParameterChange),
                    McpSchema.Boolean("allow_custom", "Allow parameters the entity does not normally have, and values the editor derives itself (e.g. delete_me)."),
                    ShowProp()),
                Idempotent = true,
                Run = call => RunOne(call, StepSetParameters, SetLabel(call), showByDefault: true),
            };

            yield return new McpTool()
            {
                Name = "remove_parameters",
                Title = "Remove parameters",
                Description = "Take parameters off an entity (it then uses the default; on an alias, the override is removed) - or, with reset, put them back to their default value as the inspector's Reset does (an alias's override is removed either way). 'path' names a nested entity's override held by an alias in 'composite'. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id).", required: true),
                    McpSchema.String("entity", "The entity (id or name)."),
                    McpSchema.Nested("path", "Instead of entity: ids/names from 'composite' through instances to a nested entity, whose override (an alias in 'composite') loses them.", EntryPath),
                    McpSchema.Strings("parameters", "Parameter names.", required: true),
                    McpSchema.Boolean("reset", "Keep the parameters, at their default values, and clear their 'set by hand' mark."),
                    ShowProp()),
                Destructive = true,
                Idempotent = true,
                Run = call => RunOne(call, StepRemoveParameters, (call.Bool("reset") ? "Reset" : "Remove") + " parameters", showByDefault: true),
            };

            yield return new McpTool()
            {
                Name = "rename_entity",
                Title = "Rename entity",
                Description = "Give an entity a new name (rename_pin renames a composite's variable). One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id).", required: true),
                    McpSchema.String("entity", "The entity (id or name).", required: true),
                    McpSchema.String("name", "The new name.", required: true),
                    ShowProp()),
                Idempotent = true,
                Run = call => RunOne(call, StepRename, "Rename to " + call.Str("name", required: true), showByDefault: true),
            };

            yield return new McpTool()
            {
                Name = "rename_pin",
                Title = "Rename composite pin",
                Description = "Rename a composite's variable - a pin of its instances - everywhere it is named: links on it inside the composite, and on every instance of the composite in the level its links, its value, the overrides aliases and proxies hold for it, animation bindings of it, and the pages drawing them. Refused when the name is taken. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite whose pin it is (path or id).", required: true),
                    McpSchema.String("pin", "The pin (variable) to rename: its name or id.", required: true),
                    McpSchema.String("name", "The new name.", required: true),
                    ShowProp()),
                Run = call => RunOne(call, StepRenamePin, "Rename pin " + call.Str("pin", required: true) + " to " + call.Str("name", required: true), showByDefault: true),
            };

            yield return new McpTool()
            {
                Name = "delete_entities",
                Title = "Delete entities",
                Description = "Delete entities, with their links (as the editor's Delete does) and everything elsewhere in the level that reaches them or inside them: aliases, trigger sequence entries, animation bindings, and for a variable its instances' links and values on that pin (each deleted entity's 'also' lists them, 'also_removed' counts them; leaving them would lose them the next time those composites open, where undo cannot bring them back). " +
                    "Proxies that reach them stay, with their links, as dead proxies (as the editor leaves them): each deleted entity's 'left_dead' lists them, to point elsewhere (retarget_proxy) or delete. " +
                    "Returns each deleted entity as {id, name, kind, type, composite, also, left_dead}. dry_run lists what would go without deleting. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id); entities may name their own."),
                    McpSchema.Array("entities", "Entity ids or names, or {composite, entity} for one in another composite.", new JObject()
                    {
                        ["anyOf"] = new JArray(
                            new JObject() { ["type"] = "string" },
                            new JObject()
                            {
                                ["type"] = "object",
                                ["properties"] = new JObject() { ["composite"] = new JObject() { ["type"] = "string" }, ["entity"] = new JObject() { ["type"] = "string" } },
                                ["required"] = new JArray("entity"),
                            }),
                    }, required: true),
                    McpSchema.Boolean("dry_run", "Only list what would be deleted, changing nothing.")),
                Destructive = true,
                Run = call => RunOne(call, StepDelete, DeleteLabel(call), showByDefault: false),
            };

            yield return new McpTool()
            {
                Name = "add_links",
                Title = "Add links",
                Description = "Link entities. Each link runs from a source entity's pin to a target entity's pin (describe_entity lists pins); a link into a nested instance or elsewhere in the level goes through an alias or proxy found or made for it (from_path / to_path). A link that runs the wrong way for data, or out of a method pin, is refused with the link to make instead. One undo step; pages are updated to draw them.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id); links may name their own."),
                    McpSchema.Array("links", "The links.", LinkSchema(true), required: true),
                    McpSchema.String("page", "The flowgraph page for links that cannot go beside what they join."),
                    McpSchema.Boolean("allow_custom", "Allow pins the entities do not normally have, and links the checks refuse, for every link.")),
                Idempotent = true,
                Run = call => RunOne(call, StepAddLinks, "Add links", showByDefault: false, scriptView: true),
            };

            yield return new McpTool()
            {
                Name = "remove_links",
                Title = "Remove links",
                Description = "Remove links. Leave out to / to_param / param to remove every link from the source that matches the rest; leave out from (giving to) to remove every link into the target that matches - 'everything that triggers X'. Returns the links removed. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id); links may name their own."),
                    McpSchema.Array("links", "The links to remove.", new JObject()
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject()
                        {
                            ["composite"] = new JObject() { ["type"] = "string", ["description"] = "The composite the link is in (default: the call's)." },
                            ["from"] = new JObject() { ["type"] = "string", ["description"] = "The source (id or name); leave out to match any source, with 'to'." },
                            ["param"] = new JObject() { ["type"] = "string", ["description"] = "The source's pin." },
                            ["to"] = new JObject() { ["type"] = "string", ["description"] = "The target (id or name)." },
                            ["to_param"] = new JObject() { ["type"] = "string", ["description"] = "The target's pin." },
                        },
                    }, required: true)),
                Destructive = true,
                Idempotent = true,
                Run = call => RunOne(call, StepRemoveLinks, "Remove links", showByDefault: false),
            };

            yield return new McpTool()
            {
                Name = "set_trigger_sequence",
                Title = "Set trigger sequence",
                Description = "Edit what a TriggerSequence fires, in order, each after a delay. 'entries' (paths through instances from its composite, or from the level root with from_root) replace the list, or join it with append / insert_at; 'remove' and 'update' change existing entries. " +
                    "'methods' (e.g. 'light_switch_on') become method pins with _relay and _finished pins: calling method M on the sequence calls M on each entry in order (after its delay), so list every method you will call. Replacing methods refuses while links use the pins it takes away, unless drop_links. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the sequence is in.", required: true),
                    McpSchema.String("entity", "The TriggerSequence (or a proxy of one), by id or name.", required: true),
                    McpSchema.Array("entries", "Entities to trigger, in order.", SequenceEntry),
                    McpSchema.Integer("insert_at", "Put the new entries at this position (0 = first), keeping the rest."),
                    McpSchema.Boolean("append", "Add the new entries (and methods) at the end instead of replacing the lists."),
                    McpSchema.Boolean("dedupe", "Skip new entries naming an entity already in the sequence (default true)."),
                    McpSchema.Array("remove", "Entries to take out: an index, or a path (every entry naming that entity goes); {\"path\":..., \"from_root\": true} for a path from the root.", new JObject()
                    {
                        ["anyOf"] = new JArray(
                            new JObject() { ["type"] = "integer" },
                            new JObject() { ["type"] = "string" },
                            new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" } },
                            new JObject() { ["type"] = "object", ["properties"] = new JObject() { ["path"] = EntryPath, ["from_root"] = new JObject() { ["type"] = "boolean" } } }),
                    }),
                    McpSchema.Array("update", "Change existing entries, by their index before this call.", new JObject()
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject()
                        {
                            ["index"] = new JObject() { ["type"] = "integer" },
                            ["delay"] = new JObject() { ["type"] = "number", ["description"] = "A new delay in seconds." },
                            ["path"] = EntryPath,
                            ["from_root"] = new JObject() { ["type"] = "boolean" },
                            ["move_to"] = new JObject() { ["type"] = "integer", ["description"] = "Move it to this position in the list." },
                        },
                        ["required"] = new JArray("index"),
                    }),
                    McpSchema.Strings("methods", "The method names it supports (replaces them unless append)."),
                    McpSchema.Boolean("drop_links", "When replacing methods: also remove the links on the method pins that go."),
                    ShowProp()),
                Destructive = true,
                Run = call => RunOne(call, StepTriggerSequence, "Set trigger sequence", showByDefault: true),
            };

            yield return new McpTool()
            {
                Name = "script_batch",
                Title = "Script batch",
                Description = "Run several script edits as ONE undo step: steps of create_entities, create_composite, set_parameters, remove_parameters, rename_entity, rename_pin, delete_entities, add_links, remove_links and set_trigger_sequence, each with that tool's own arguments, in order. " +
                    "A ref made in one step names that entity in later steps working in the same composite (in entity, from/to, and as the first step of a path). If any step fails nothing is changed, and the error says which step. dry_run runs it all, reports, and puts it back. " +
                    "For a task spread over other tools too, use undo_group instead.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("label", "What the batch does, for the undo history ('AI: <label>').", required: true),
                    McpSchema.Array("steps", "The edits, in order.", new JObject()
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject()
                        {
                            ["tool"] = new JObject() { ["type"] = "string", ["enum"] = new JArray(_steps.Keys.ToArray()), ["description"] = "The script tool." },
                            ["arguments"] = new JObject() { ["type"] = "object", ["additionalProperties"] = true, ["description"] = "Its arguments, as the tool takes them (without show or dry_run)." },
                        },
                        ["required"] = new JArray("tool", "arguments"),
                    }, required: true),
                    McpSchema.Boolean("dry_run", "Run every step and report the results, then put everything back."),
                    ShowProp()),
                Destructive = true,
                Run = Batch,
            };

            yield return new McpTool()
            {
                Name = "deinstance",
                Title = "De-instance composite instance",
                Description = "Pull a composite instance apart: its composite's entities are copied into the composite it is placed in (positions, parameters, aliases and links carried over, script pages imported) and the instance goes. Other instances are unaffected. dry_run reports what it would carry over inexactly, or why it cannot, without changing anything or moving the editor. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the instance is in.", required: true),
                    McpSchema.String("instance", "The instance entity (id or name).", required: true),
                    McpSchema.Boolean("allow_notes", "Go ahead even if it reports things it cannot carry over exactly (default true)."),
                    McpSchema.Boolean("dry_run", "Only report the plan's blocking problems and notes.")),
                Destructive = true,
                Run = Deinstance,
            };

            yield return new McpTool()
            {
                Name = "group_into_composite",
                Title = "Group into new composite",
                Description = "The editor's Create Composite From Selected: moves entities (with their parameters and script pages) into a new composite and places an instance of it where they were; links that crossed the boundary are kept through new pins and aliases. dry_run reports the plan's problems and notes without changing anything or moving the editor. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite they are in.", required: true),
                    McpSchema.Strings("entities", "The entities (ids or names).", required: true),
                    McpSchema.String("name", "The new composite's path (default: beside the current one, named after it)."),
                    McpSchema.Boolean("dry_run", "Only report the name it would get, and the plan's blocking problems and notes.")),
                Run = GroupIntoComposite,
            };

            yield return new McpTool()
            {
                Name = "duplicate_composite",
                Title = "Duplicate composite",
                Description = "Copy a composite under a new path: the same entities with the same ids, links, parameters, pins and flowgraph pages, its models, collision and physics shared with the original as two placements share them. Because the ids match, an instance switched to the copy keeps every alias, proxy, trigger sequence and animation that reached into it - so use_for (instances of the original to switch to the copy) gives one placement a version of its own to change, like the editor's Create Composite Variant. dry_run reports the name it would get and any problems without changing anything. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite to copy (path or id).", required: true),
                    McpSchema.String("path", "The copy's path, with backslash folders (default: beside the original, '<name>_Copy')."),
                    McpSchema.Array("use_for", "Instances of the original to switch to the copy.", new JObject()
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject()
                        {
                            ["composite"] = new JObject() { ["type"] = "string", ["description"] = "The composite the instance is in (path or id)." },
                            ["instance"] = new JObject() { ["type"] = "string", ["description"] = "The instance entity (id or name)." },
                        },
                        ["required"] = new JArray("composite", "instance"),
                        ["additionalProperties"] = false,
                    }),
                    McpSchema.Boolean("dry_run", "Only report the name it would get, and any blocking problems and notes.")),
                Run = DuplicateComposite,
            };
        }

        #region Reading arguments
        /// <summary>A text field of an object argument: null when missing, an error when it is not text.</summary>
        internal static string Field(JObject spec, string name)
        {
            JToken token = spec?[name];
            if (token == null || token.Type == JTokenType.Null)
                return null;
            if (token.Type == JTokenType.String || token.Type == JTokenType.Integer || token.Type == JTokenType.Float || token.Type == JTokenType.Boolean)
                return token.Type == JTokenType.String ? (string)token : token.ToString(Newtonsoft.Json.Formatting.None);
            throw McpError.Invalid("'" + name + "' must be text, not " + token.ToString(Newtonsoft.Json.Formatting.None) + ".");
        }

        private static bool Flag(JObject spec, string name) => spec?[name] != null && spec[name].Type != JTokenType.Null && McpValues.ReadBool(spec[name], name);

        /// <summary>
        /// A path of entity ids/names as any tool takes one (<see cref="McpScript.PathSteps"/>): an array, one string split on '/', '\'
        /// or '>', or a result's own {ids} / {path, ids} object - so a path a result gives can be passed back as it is.
        /// </summary>
        internal static List<string> ReadPath(JToken token) => McpScript.PathSteps(token);

        /// <summary>A folder placeholder manage_composites create_folder makes: a composite named with a trailing separator, holding nothing.</summary>
        internal static bool IsFolderPlaceholder(Composite composite) => (composite?.name ?? "").EndsWith("\\") || (composite?.name ?? "").EndsWith("/");

        /// <summary>A composite to edit (as McpScript.FindComposite finds it); a folder placeholder is refused.</summary>
        private static Composite FindComposite(Commands commands, string reference)
        {
            Composite composite = McpScript.FindComposite(commands, reference);
            if (IsFolderPlaceholder(composite))
                throw new McpError(McpErrorCodes.NotFound, "'" + composite.name + "' is a folder (made by manage_composites create_folder), not a composite. find_composites lists the composites in it.");
            return composite;
        }

        /// <summary>'composite' (the default) or 'world'.</summary>
        private static bool ReadWorld(JToken token, string where)
        {
            if (token == null || token.Type == JTokenType.Null)
                return false;
            string text = McpValues.ReadString(token).Trim().ToLowerInvariant();
            if (text == "world") return true;
            if (text == "composite" || text == "local") return false;
            throw McpError.Invalid("'" + where + "' is 'composite' (relative to the composite: the default) or 'world'.");
        }
        #endregion

        #region Running a step
        /// <summary>What one call's steps share (all of a script_batch): refs to what they made, notes, and checks for once the change is in.</summary>
        private sealed class Scope
        {
            public readonly Dictionary<string, (Composite Composite, Entity Entity)> Refs = new Dictionary<string, (Composite, Entity)>(StringComparer.OrdinalIgnoreCase);
            public readonly List<string> Notes = new List<string>();
            //New content to check zone membership of, and animation entities to check, once the change is in
            public readonly List<(Composite Composite, Entity Entity)> Placed = new List<(Composite, Entity)>();
            public readonly List<(Composite Composite, Entity Entity)> AnimationChecks = new List<(Composite, Entity)>();
            //What the game only takes after a Save & Build (named for the note), and what was worked out on the way
            public readonly List<string> Built = new List<string>();
            public readonly Dictionary<Composite, bool> Carries = new Dictionary<Composite, bool>();
            public readonly Dictionary<Composite, int> Placements = new Dictionary<Composite, int>();

            public void Note(string text)
            {
                if (!string.IsNullOrEmpty(text) && !Notes.Contains(text))
                    Notes.Add(text);
            }
        }

        private delegate JObject Step(McpScriptEdit edit, McpCall args, Scope scope);

        private static readonly Dictionary<string, Step> _steps = new Dictionary<string, Step>(StringComparer.OrdinalIgnoreCase)
        {
            ["create_entities"] = StepCreateEntities,
            ["create_composite"] = StepCreateComposite,
            ["set_parameters"] = StepSetParameters,
            ["remove_parameters"] = StepRemoveParameters,
            ["rename_entity"] = StepRename,
            ["rename_pin"] = StepRenamePin,
            ["delete_entities"] = StepDelete,
            ["add_links"] = StepAddLinks,
            ["remove_links"] = StepRemoveLinks,
            ["set_trigger_sequence"] = StepTriggerSequence,
        };

        /// <summary>One tool call: its step as one undo step, then what to tell the caller.</summary>
        private static object RunOne(McpCall call, Step step, string label, bool showByDefault, bool scriptView = false)
        {
            List<Composite> touching = McpEditor.UI(() => Prepare(McpEditor.RequireCommands(), call, strict: true));
            Scope scope = new Scope();
            JObject result = null;
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run(label, touching[0], edit => result = step(edit, call, scope), Options(call, showByDefault, touching));
            result = Finish(call, scope, outcome, result ?? new JObject());
            if (scriptView && result["composite"] is JValue named && named.Type == JTokenType.String)
            {
                Composite composite = McpEditor.UI(() => McpScript.FindComposite(McpEditor.RequireCommands(forEditing: false), (string)named));
                result["script_view"] = PagesNow(composite);
            }
            return result;
        }

        private static McpScriptEdit.RunOptions Options(McpCall call, bool showByDefault, IEnumerable<Composite> touching)
        {
            bool asked = call.Has("show");
            return new McpScriptEdit.RunOptions()
            {
                Show = asked ? call.Bool("show") : showByDefault,
                ForceShow = asked && call.Bool("show"),
                DryRun = call.Bool("dry_run"),
                AlsoTouches = touching,
            };
        }

        /// <summary>
        /// Before a step runs (UI thread): the composites it names, the first being the one it is about (its 'composite',
        /// else the first a change, entity or link names, else place_in). Unless <paramref name="strict"/> (a batch, where
        /// an earlier step may make it), one that cannot be found yet is skipped. An editor open on a sequence the step
        /// sets is closed first: it records its own changes as a step of its own.
        /// </summary>
        private static List<Composite> Prepare(Commands commands, McpCall args, bool strict)
        {
            List<Composite> found = new List<Composite>();
            void Add(string reference)
            {
                if (string.IsNullOrWhiteSpace(reference)) return;
                try
                {
                    Composite composite = FindComposite(commands, reference);
                    if (!found.Contains(composite)) found.Add(composite);
                }
                catch (McpError) when (!strict) { }
            }
            string tool = args.Tool?.Name;
            if (tool == "create_composite")
            {
                if (args.Has("place_in")) Add(args.Str("place_in"));
                else
                {
                    Composite shown = Singleton.Editor?.CompositeDisplay?.Composite;
                    found.Add(shown != null && commands.Entries.Contains(shown) ? shown : commands.EntryPoints[0]);
                }
                return found;
            }
            Add(args.Str("composite"));
            //Items naming their own composite (an entity spec's 'composite' is what it instances, not where it goes)
            List<string> lists = new List<string>() { "changes", "links" };
            if (tool == "delete_entities") lists.Add("entities");
            foreach (string list in lists)
                foreach (JToken item in args.Array(list))
                    if (item is JObject spec && spec["composite"] != null)
                        Add(Field(spec, "composite"));
            if (found.Count == 0 && strict)
                throw McpError.Invalid("Say which composite: 'composite' (a path or id, 'root' for the level's root)" + (tool == "set_parameters" || tool == "add_links" || tool == "remove_links" || tool == "delete_entities" ? ", or one on each item." : "."));
            if (tool == "set_trigger_sequence" && found.Count != 0)
            {
                Entity entity = null;
                try { entity = McpScript.FindEntity(commands, found[0], args.Str("entity")); } catch (McpError) when (!strict) { }
                if (entity != null)
                    foreach (TriggerSequenceEditor editor in Application.OpenForms.OfType<TriggerSequenceEditor>().ToList())
                        if (editor.Entity == entity) editor.Close();
            }
            return found;
        }

        /// <summary>After the change: the checks that read it as it now stands, and the notes for the caller.</summary>
        private static JObject Finish(McpCall call, Scope scope, McpScriptEdit.Outcome outcome, JObject result)
        {
            if (outcome.Changed && !outcome.DryRun)
                McpEditor.UI(() => AfterChange(McpEditor.RequireCommands(forEditing: false), scope));
            if (scope.Built.Count != 0)
            {
                List<string> named = scope.Built.Distinct().ToList();
                scope.Note("needs_build: the game takes the change to " + string.Join(", ", named.Take(6)) + (named.Count > 6 ? " and " + (named.Count - 6) + " more" : "") +
                    " only after save_level build=true - the build makes the movers, lights, collision and zone data it reads (a plain save does not); the viewport shows it already.");
            }
            foreach (string note in scope.Notes) call.Note(note);
            foreach (string note in outcome.Notes) call.Note(note);
            if (outcome.DryRun)
                result["dry_run"] = "Nothing was changed: this is what the call would do.";
            else if (!outcome.Changed && result["unchanged"] == null)
                result["unchanged"] = "Nothing needed changing.";
            if (outcome.Shared.Count != 0)
                result["placements"] = new JObject(outcome.Shared.Select(o => new JProperty(o.Composite.name, o.Placements)));
            return result;
        }

        /// <summary>A label for one entity's step: its name, else its type (never empty).</summary>
        private static string Label(JToken token)
        {
            if (!(token is JObject spec))
                return "entity";
            string Text(string field) => spec[field]?.Type == JTokenType.String && ((string)spec[field]).Trim().Length != 0 ? ((string)spec[field]).Trim() : null;
            string composite = Text("composite") == null ? null : McpScript.NormalisePath(Text("composite")).Split('\\').LastOrDefault();
            return Text("name") ?? Text("function") ?? (string.IsNullOrEmpty(composite) ? null : composite)
                ?? (spec["variable"] != null ? "variable" : spec["alias"] != null ? "alias" : spec["proxy"] != null ? "proxy" : "entity");
        }

        private static string CreateLabel(McpCall call)
        {
            JArray specs = call.Array("entities");
            return specs.Count == 1 ? "Add " + Label(specs[0]) : "Add " + specs.Count + " entities";
        }

        private static string SetLabel(McpCall call)
        {
            int count = call.Array("changes").Count + (call.Has("entity") || call.Has("path") ? 1 : 0);
            if (count == 1)
            {
                string named = call.Str("entity") ?? (call.Has("path") ? ReadPath(call.Token("path")).LastOrDefault() : null) ?? Field(call.Array("changes")[0] as JObject, "entity");
                if (named != null) return "Set parameters on " + named;
            }
            return "Set parameters on " + count + " entities";
        }

        private static string DeleteLabel(McpCall call)
        {
            int count = call.Array("entities").Count;
            return "Delete " + count + " entit" + (count == 1 ? "y" : "ies");
        }

        /// <summary>An entity of the composite: a ref made earlier in the call (in this composite), else its id or name.</summary>
        private static Entity Resolve(McpScriptEdit edit, Composite composite, string reference, Scope scope)
        {
            if (reference != null && scope != null && scope.Refs.TryGetValue(reference.Trim(), out (Composite Composite, Entity Entity) made))
            {
                if (made.Composite == composite)
                    return made.Entity;
                throw McpError.Invalid("'" + reference + "' is a ref made in " + made.Composite.name + ", but this works in " + composite.name + ". Reach it from here through an alias path (from_path/to_path, 'path') or a proxy.");
            }
            return McpScript.FindEntity(edit.Commands, composite, reference);
        }

        /// <summary>A path whose first step may be a ref made earlier in this composite.</summary>
        private static List<string> ExpandRefs(List<string> steps, Composite composite, Scope scope)
        {
            if (steps.Count != 0 && scope != null && scope.Refs.TryGetValue(steps[0], out (Composite Composite, Entity Entity) made) && made.Composite == composite)
                return new[] { McpScript.Id(made.Entity.shortGUID) }.Concat(steps.Skip(1)).ToList();
            return steps;
        }

        /// <summary>
        /// The entity a path from <paramref name="composite"/> names: one step is the entity itself; more reach inside nested
        /// instances, through an alias in <paramref name="composite"/> - the one already on that path, else (when
        /// <paramref name="create"/>) a new one, else null. <paramref name="via"/> describes the alias.
        /// </summary>
        private static Entity EntityOnPath(McpScriptEdit edit, Composite composite, JToken token, Scope scope, bool create, out JObject via)
        {
            via = null;
            Commands commands = edit.Commands;
            List<string> steps = ExpandRefs(ReadPath(token), composite, scope);
            if (steps.Count == 1)
                return Resolve(edit, composite, steps[0], scope);
            ShortGuid[] path = McpScript.ResolvePath(commands, composite, steps, out Composite targetComposite, out Entity target);
            if (target is AliasEntity)
                throw McpError.Invalid("The path ends at an alias; give the path to what that alias points at.");
            if (target is VariableEntity)
                throw McpError.Invalid("The path ends at a variable (a pin of " + targetComposite.name + "): set it on the instance instead - its parameter of that name.");
            List<AliasEntity> existing = McpScriptEdit.AliasesOn(composite, path);
            AliasEntity alias;
            bool created = false;
            if (existing.Count != 0)
                alias = existing[0];
            else if (create)
                alias = edit.FindOrAddAlias(composite, path, null, out created);
            else
                return null;
            via = new JObject()
            {
                ["alias"] = McpScript.Id(alias.shortGUID),
                ["name"] = McpScript.EntityName(commands, composite, alias),
                ["created"] = created,
                ["target"] = McpScript.EntityName(commands, targetComposite, target) + " in " + targetComposite.name,
            };
            if (existing.Count > 1)
                scope?.Note(composite.name + " has " + existing.Count + " aliases on the path to " + McpScript.EntityName(commands, targetComposite, target) + " (" + string.Join(", ", existing.Select(o => McpScript.Id(o.shortGUID))) +
                    "): the first was used. Several aliases on one path apply in no set order when the level is built - delete the extras (delete_entities).");
            return alias;
        }
        #endregion

        #region World space
        /// <summary>
        /// The world transform an entity's position is written relative to, in one placement of its composite (UI thread):
        /// the composite's placement, and for an alias, the instances its path passes through as well. A composite placed
        /// more than once needs <paramref name="placement"/> (a number, or a path from the root to its instance). With no
        /// <paramref name="entity"/>, the composite's own frame.
        /// </summary>
        internal static cTransform FrameFor(Commands commands, Composite composite, Entity entity, JToken placement, out JObject described)
        {
            Composite root = commands.EntryPoints[0];
            List<Entity> chain;
            if (placement != null && placement.Type != JTokenType.Null && placement.Type != JTokenType.Integer)
            {
                chain = McpRegion.ChainFromNames(commands, ReadPath(placement));
                Composite placed = chain.Count == 0 ? root : McpScript.InstancedComposite(commands, chain[chain.Count - 1]);
                if (placed != composite)
                    throw McpError.Invalid("The placement path ends at " + McpRegion.DescribeChain(commands, chain) + (placed == null ? ", which is not a composite instance" : ", an instance of " + placed.name) + ", not of " + composite.name + ". get_placements lists where it is placed.");
                described = new JObject() { ["composite"] = composite.name, ["placement_path"] = McpRegion.ChainJson(commands, chain)["path"] };
            }
            else
            {
                int? index = placement == null || placement.Type == JTokenType.Null ? (int?)null : McpValues.ReadInt(placement, "placement");
                McpSpatialTools.FrameOf(commands, composite, index, "'placement' (0-based, or a path from the root to the instance)", out described);
                JArray ids = described["placement_path"] as JArray;
                chain = ids == null ? new List<Entity>() : (McpRegion.ChainFromIds(commands, root, ids.Select(o => McpScript.ParseId((string)o)?.AsUInt32 ?? 0u)) ?? new List<Entity>());
            }
            if (entity is AliasEntity alias)
            {
                List<Tuple<Composite, Entity>> resolved = commands.Utils.ResolveAlias(alias, composite);
                if (resolved.Count == 0 || resolved.Any(o => o.Item2 == null))
                    throw new McpError(McpErrorCodes.Refused, "The alias points at nothing, so it has no frame to place it in.");
                chain.AddRange(resolved.Take(resolved.Count - 1).Select(o => o.Item2));
            }
            else if (entity is ProxyEntity)
                throw McpError.Invalid("A proxy is not placed itself: give world positions to the entity it stands for, in its own composite.");
            if (chain.Count == 0)
                return new cTransform(Vector3.Zero, Vector3.Zero);
            return McpRegion.Walker(commands).Evaluate(root, chain).World ?? new cTransform(Vector3.Zero, Vector3.Zero);
        }

        /// <summary>A transform value given in world space, as the value to write in <paramref name="frame"/>: only the parts given.</summary>
        private static JObject ToFrame(cTransform frame, JToken value, string name)
        {
            JToken position = null, rotation = null;
            if (value is JArray)
                position = value;
            else if (value is JObject obj)
            {
                position = obj["position"];
                rotation = obj["rotation"];
                if (position == null && rotation == null)
                    throw McpError.Invalid("'" + name + "' in world space takes {\"position\": [x, y, z], \"rotation\": [x, y, z]} (either part) or [x, y, z].");
            }
            else
                throw McpError.Invalid("'" + name + "' in world space takes {\"position\": [x, y, z], \"rotation\": [x, y, z]} (either part) or [x, y, z].");
            JObject local = new JObject();
            if (position != null && position.Type != JTokenType.Null)
            {
                if (!(position is JArray))
                    throw McpError.Invalid("A world position is a whole [x, y, z] (metres).");
                local["position"] = McpValues.Vector(InstanceTransform.PointToLocal(frame, McpValues.ReadVector(position, name + ".position", null)));
            }
            if (rotation != null && rotation.Type != JTokenType.Null)
            {
                if (!(rotation is JArray))
                    throw McpError.Invalid("A world rotation is a whole [x, y, z] (degrees).");
                local["rotation"] = McpValues.Vector(InstanceTransform.ToLocal(frame, new cTransform(Vector3.Zero, McpValues.ReadVector(rotation, name + ".rotation", null))).rotation);
            }
            return local;
        }

        /// <summary>Where an entity's transform now puts it in the world, and the placement that was read through.</summary>
        private static JObject WorldOf(McpScriptEdit edit, Composite composite, Entity entity, ShortGuid parameter, cTransform frame, JObject described)
        {
            cTransform local = edit.CurrentValue(composite, entity, parameter) as cTransform ?? new cTransform(Vector3.Zero, Vector3.Zero);
            cTransform world = InstanceTransform.Compose(frame, local);
            JObject result = new JObject() { ["position"] = McpValues.Vector(world.position), ["rotation"] = McpValues.Vector(world.rotation) };
            if (described?["placement"] != null) result["placement"] = described["placement"];
            else if (described?["placement_path"] != null) result["placement"] = described["placement_path"];
            return result;
        }
        #endregion

        #region Creating
        private static JObject StepCreateEntities(McpScriptEdit edit, McpCall args, Scope scope)
        {
            JArray specs = args.Array("entities", required: true);
            if (specs.Count == 0)
                throw McpError.Invalid("'entities' is empty.");
            Composite composite = FindComposite(edit.Commands, args.Str("composite", required: true));
            bool allowCustom = args.Bool("allow_custom");
            edit.UsePage(composite, args.Str("page"));
            JArray made = new JArray();
            JArray links = new JArray();
            foreach (JToken spec in specs)
                made.Add(Make(edit, composite, spec, scope, allowCustom, args.Token("space"), args.Token("placement")));
            foreach (JToken link in args.Array("links"))
                links.Add(Link(edit, composite, link, scope, allowCustom, perLinkComposite: false));
            JObject result = new JObject() { ["composite"] = composite.name, ["created"] = made };
            if (links.Count != 0) result["links"] = links;
            return result;
        }

        private static JObject StepCreateComposite(McpScriptEdit edit, McpCall args, Scope scope)
        {
            Commands commands = edit.Commands;
            string path = args.Str("path", required: true);
            string placeIn = args.Str("place_in");
            if (placeIn == null && (args.Has("position") || args.Has("rotation") || args.Has("instance_name") || args.Has("space") || args.Has("placement")))
                throw McpError.Invalid("'position', 'rotation', 'space', 'placement' and 'instance_name' are for placing an instance of the new composite: give 'place_in' too (e.g. 'root').");
            bool allowCustom = args.Bool("allow_custom");
            Composite created = edit.AddComposite(path);
            edit.UsePage(created, args.Str("page"));
            JArray made = new JArray();
            JArray links = new JArray();
            foreach (JToken spec in args.Array("entities"))
                made.Add(Make(edit, created, spec, scope, allowCustom, null, null));
            foreach (JToken link in args.Array("links"))
                links.Add(Link(edit, created, link, scope, allowCustom, perLinkComposite: false));
            //A new composite's content is placed only through the instance below: that is what a zone has to claim
            scope.Placed.RemoveAll(o => o.Composite == created);
            JObject result = McpScript.CompositeSummary(commands, created);
            result["created"] = made;
            if (links.Count != 0) result["links"] = links;
            if (placeIn != null)
            {
                Composite host = FindComposite(commands, placeIn);
                FunctionEntity placed = edit.AddInstance(host, created, args.Str("instance_name"));
                JObject instance = new JObject() { ["composite"] = host.name, ["id"] = McpScript.Id(placed.shortGUID), ["name"] = McpScript.EntityName(commands, host, placed) };
                JObject world = Place(edit, host, placed, args.Token("position"), args.Token("rotation"), ReadWorld(args.Token("space"), "space"), args.Token("placement"));
                if (world != null) instance["world"] = world;
                if (PlacementsOf(commands, host, scope) != 0 && Carries(commands, created, scope))
                {
                    instance["needs_build"] = true;
                    scope.Built.Add((string)instance["name"]);
                }
                edit.Select.Add(placed);
                scope.Placed.Add((host, placed));
                result["instance"] = instance;
            }
            return result;
        }

        //Abstract bases other types build on: no vanilla level places one by itself (SmokeCylinderAttachmentInterface, which some do, is not here)
        private static readonly HashSet<FunctionType> _abstractTypes = new HashSet<FunctionType>()
        {
            FunctionType.AttachmentInterface, FunctionType.BooleanLogicInterface, FunctionType.CameraBehaviorInterface, FunctionType.CloseableInterface,
            FunctionType.CompositeInterface, FunctionType.EvaluatorInterface, FunctionType.GateInterface, FunctionType.GateResourceInterface,
            FunctionType.GetComponentInterface, FunctionType.InspectorInterface, FunctionType.ModifierInterface, FunctionType.ProxyInterface,
            FunctionType.ScriptInterface, FunctionType.SensorAttachmentInterface, FunctionType.SensorInterface, FunctionType.TransformerInterface,
            FunctionType.ZoneInterface,
        };

        /// <summary>Make one entity from its spec, in <paramref name="composite"/> (or reuse the alias or proxy already on its path).</summary>
        private static JObject Make(McpScriptEdit edit, Composite composite, JToken token, Scope scope, bool allowCustom, JToken defaultSpace, JToken defaultPlacement)
        {
            if (!(token is JObject spec))
                throw McpError.Invalid("Each entity is an object, e.g. {\"function\": \"ThinkOnce\"}.");
            Commands commands = edit.Commands;
            string name = Field(spec, "name");
            string[] kinds = new[] { "function", "composite", "variable", "alias", "proxy" }.Where(o => spec[o] != null).ToArray();
            if (kinds.Length != 1)
                throw McpError.Invalid("Give each entity exactly one of: function, composite, variable, alias, proxy" + (kinds.Length > 1 ? " (this one has " + string.Join(" and ", kinds) + ")." : "."));
            if (spec["new"] != null && kinds[0] != "alias" && kinds[0] != "proxy")
                throw McpError.Invalid("'new' is for an alias or proxy (make another even when one is on that path).");

            Entity entity;
            bool reused = false;
            switch (kinds[0])
            {
                case "function":
                    {
                        string typeName = Field(spec, "function");
                        if (!Enum.TryParse(typeName?.Trim(), true, out FunctionType type) || !Enum.IsDefined(typeof(FunctionType), type))
                        {
                            //A composite named like a type is a common slip: point at it
                            Composite named = null;
                            try { named = McpScript.FindComposite(commands, typeName); } catch { }
                            throw McpError.NotFound("function type", typeName, Enum.GetNames(typeof(FunctionType)),
                                named != null ? "There is a composite '" + named.name + "': use \"composite\" to place an instance of it." : "list_function_types lists them.");
                        }
                        if (_abstractTypes.Contains(type) && !allowCustom)
                        {
                            List<string> derived = Enum.GetValues(typeof(FunctionType)).Cast<FunctionType>().Where(o => o != type && commands.Utils.GetInheritedFunction(o) == type).Select(o => o.ToString()).OrderBy(o => o).Take(10).ToList();
                            throw new McpError(McpErrorCodes.Refused, type + " is an abstract base type other types build on; the game does not place one by itself (no vanilla level does)." +
                                (derived.Count != 0 ? " Types built on it: " + string.Join(", ", derived) + "." : "") + " Pass allow_custom: true to add it anyway.");
                        }
                        entity = edit.AddFunction(composite, type, name);
                        if (type == FunctionType.EnvironmentModelReference)
                            scope.Note(McpScript.EntityName(commands, composite, entity) + " (EnvironmentModelReference) points at no animated model entry yet: set_animated_model {entry: 'new', skeleton: <rig>} gives it one (describe_animated_prop shows what a prop can play).");
                        else if (type == FunctionType.CAGEAnimation)
                            scope.Note(McpScript.EntityName(commands, composite, entity) + " (CAGEAnimation) has anim_length " + McpCageAnimationTools.DefaultLength + " until animate_parameters' first keys set it, and plays only when something links to its start (check_cage_animation says whether it will). " +
                                "For a camera move, create_camera_animation makes the camera, the animation and the trigger wired the retail way, and animate_camera_path keys a smooth path.");
                        break;
                    }
                case "composite":
                    entity = edit.AddInstance(composite, FindComposite(commands, Field(spec, "composite")), name);
                    break;
                case "variable":
                    {
                        JObject variable = spec["variable"] as JObject ?? throw McpError.Invalid("'variable' is an object: {\"name\": ..., \"pin\": ...}.");
                        entity = edit.AddVariable(composite, Field(variable, "name") ?? name, ParsePinType(Field(variable, "pin")), Field(variable, "enum_type"));
                        break;
                    }
                case "alias":
                    {
                        List<string> steps = ExpandRefs(ReadPath(spec["alias"]), composite, scope);
                        ShortGuid[] path = McpScript.ResolvePath(commands, composite, steps, out Composite _, out Entity target);
                        if (target is AliasEntity)
                            throw McpError.Invalid("An alias cannot point at another alias; point it at what that alias points at.");
                        if (Flag(spec, "new"))
                            entity = edit.AddAlias(composite, path, name);
                        else
                        {
                            entity = edit.FindOrAddAlias(composite, path, name, out bool created);
                            reused = !created;
                        }
                        break;
                    }
                case "proxy":
                    {
                        List<string> steps = ReadPath(spec["proxy"]);
                        ShortGuid[] path = McpScript.ResolvePath(commands, commands.EntryPoints[0], steps, out Composite _, out Entity _);
                        if (Flag(spec, "new"))
                            entity = edit.AddProxy(composite, path, name);
                        else
                        {
                            entity = edit.FindOrAddProxy(composite, path, name, out bool created);
                            reused = !created;
                        }
                        break;
                    }
                default:
                    throw McpError.Invalid("Unknown entity kind.");
            }
            if (reused && name != null && !string.Equals(McpScript.EntityName(commands, composite, entity), name.Trim(), StringComparison.Ordinal))
                scope.Note("The " + kinds[0] + " already on that path (" + McpScript.EntityName(commands, composite, entity) + ", " + McpScript.Id(entity.shortGUID) + ") was reused and keeps its name; rename_entity renames it, and 'new': true makes another.");

            JObject world = Place(edit, composite, entity, spec["position"], spec["rotation"], ReadWorld(spec["space"] ?? defaultSpace, "space"), spec["placement"] ?? defaultPlacement,
                new Aiming(spec["look_at"], spec["forward"], spec["roll"]));
            if (spec["node"] != null && spec["node"].Type != JTokenType.Null && !reused)
            {
                if (!(spec["node"] is JObject node))
                    throw McpError.Invalid("'node' is an object: {\"page\": ..., \"x\": ..., \"y\": ...}.");
                bool hasX = node["x"] != null && node["x"].Type != JTokenType.Null, hasY = node["y"] != null && node["y"].Type != JTokenType.Null;
                if (hasX != hasY)
                    throw McpError.Invalid("'node' needs both x and y, or neither.");
                System.Drawing.Point? at = hasX ? new System.Drawing.Point((int)Math.Round(McpValues.ReadDouble(node["x"], "node.x")), (int)Math.Round(McpValues.ReadDouble(node["y"], "node.y"))) : (System.Drawing.Point?)null;
                edit.PlaceNode(entity, Field(node, "page"), at);
            }
            if (spec["sequence"] != null && spec["sequence"].Type != JTokenType.Null)
                FillSequence(edit, composite, entity, spec["sequence"], scope);
            if (spec["parameters"] != null && !(spec["parameters"] is JObject))
                throw McpError.Invalid("'parameters' is an object of values by name, e.g. {\"radius\": 2}.");
            if (spec["parameters"] is JObject parameters)
                foreach (JProperty parameter in parameters.Properties())
                    Write(edit, composite, entity, parameter.Name, parameter.Value, allowCustom, scope);

            string reference = Field(spec, "ref");
            if (!string.IsNullOrWhiteSpace(reference))
            {
                reference = reference.Trim();
                if (scope.Refs.ContainsKey(reference))
                    throw McpError.Invalid("Two entities have the ref '" + reference + "'.");
                scope.Refs[reference] = (composite, entity);
            }
            edit.Select.Add(entity);
            if (!reused)
                scope.Placed.Add((composite, entity));

            JObject made = McpScript.Brief(commands, composite, entity);
            if (reference != null) made["ref"] = reference;
            if (reused) made["reused"] = true;
            if (world != null) made["world"] = world;
            //New content the build reads (or an alias given values for it) reaches the game only after a Save & Build
            bool build = !reused && NewContentNeedsBuild(commands, composite, entity, scope);
            if (!build && entity is AliasEntity && spec["parameters"] is JObject given && PlacementsOf(commands, composite, scope) != 0)
                build = given.Properties().Any(o => NeedsBuild(commands, composite, entity, McpScript.ParamId(o.Name), scope));
            if (build)
            {
                made["needs_build"] = true;
                scope.Built.Add(McpScript.EntityName(commands, composite, entity));
            }
            return made;
        }

        /// <summary>
        /// Write a position and/or rotation on an entity: relative to its composite, or (<paramref name="world"/>) in world
        /// space through one placement of it. Returns where it now is in the world when given in world space, else null.
        /// </summary>
        private static JObject Place(McpScriptEdit edit, Composite composite, Entity entity, JToken position, JToken rotation, bool world, JToken placement, Aiming aim = null)
        {
            bool hasPosition = position != null && position.Type != JTokenType.Null;
            bool aims = (aim != null && aim.Given) || rotation is JObject;
            if (!hasPosition && (rotation == null || rotation.Type == JTokenType.Null) && !aims)
                return null;
            if (entity is VariableEntity)
                throw McpError.Invalid("A variable has no position.");
            cTransform frame = null;
            JObject described = null;
            if (world)
                frame = FrameFor(edit.Commands, composite, entity, placement, out described);
            //A rotation that faces a point or a direction: from the position given, else from where the entity stands (in the same space)
            if (aims)
                rotation = Aim(rotation, aim ?? new Aiming(null, null, null), () =>
                {
                    if (hasPosition) return McpValues.ReadVector(position, "position", null);
                    Vector3 local = (edit.CurrentValue(composite, entity, ShortGuids.position) as cTransform)?.position ?? Vector3.Zero;
                    return world ? InstanceTransform.PointToWorld(frame, local) : local;
                }, "rotation");
            JObject transform = new JObject();
            if (hasPosition) transform["position"] = position;
            if (rotation != null && rotation.Type != JTokenType.Null) transform["rotation"] = rotation;
            if (!world)
            {
                edit.SetParameter(composite, entity, "position", transform, allowCustom: true);
                return null;
            }
            edit.SetParameter(composite, entity, "position", ToFrame(frame, transform, "position"), allowCustom: true);
            return WorldOf(edit, composite, entity, ShortGuids.position, frame, described);
        }

        /// <summary>The look_at / forward / roll a transform may give instead of a rotation.</summary>
        private sealed class Aiming
        {
            public readonly JToken LookAt, Forward, Roll;

            public Aiming(JToken lookAt, JToken forward, JToken roll)
            {
                LookAt = lookAt != null && lookAt.Type != JTokenType.Null ? lookAt : null;
                Forward = forward != null && forward.Type != JTokenType.Null ? forward : null;
                Roll = roll != null && roll.Type != JTokenType.Null ? roll : null;
            }

            public bool Given => LookAt != null || Forward != null || Roll != null;

            /// <summary>The aiming fields of a transform value ({position, look_at, ...}), else null.</summary>
            public static Aiming Of(JToken value) => value is JObject obj && (obj["look_at"] != null || obj["forward"] != null || obj["roll"] != null || obj["rotation"] is JObject)
                ? new Aiming(obj["look_at"], obj["forward"], obj["roll"]) : null;
        }

        /// <summary>
        /// A rotation as given ([pitch, yaw, roll]), or built to face a point (look_at, seen from <paramref name="from"/>) or along a
        /// direction (forward), with roll, as InstanceTransform.LookRotation turns +Z: the same space as the position. A rotation
        /// given as an object {look_at | forward, roll} is read the same way.
        /// </summary>
        private static JToken Aim(JToken rotation, Aiming aim, Func<Vector3> from, string where)
        {
            if (rotation is JObject inner)
            {
                if (aim.Given)
                    throw McpError.Invalid("'" + where + "': give look_at / forward / roll inside 'rotation' or beside it, not both.");
                foreach (JProperty property in inner.Properties())
                    if (property.Name != "look_at" && property.Name != "forward" && property.Name != "roll")
                        throw McpError.Invalid("'" + where + "' as an object takes look_at or forward (with roll), not '" + property.Name + "'; a rotation itself is [pitch, yaw, roll].");
                aim = new Aiming(inner["look_at"], inner["forward"], inner["roll"]);
                rotation = null;
            }
            if (aim.LookAt == null && aim.Forward == null)
            {
                if (aim.Roll != null)
                    throw McpError.Invalid("'roll' goes with look_at or forward (a rotation gives its own roll as its z).");
                return rotation;
            }
            if (rotation != null && rotation.Type != JTokenType.Null)
                throw McpError.Invalid("Give one of rotation, look_at or forward.");
            if (aim.LookAt != null && aim.Forward != null)
                throw McpError.Invalid("Give look_at or forward, not both.");
            Vector3 direction;
            if (aim.Forward != null)
                direction = McpValues.ReadVector(aim.Forward, "forward", null);
            else
            {
                Vector3 target = McpValues.ReadVector(aim.LookAt, "look_at", null);
                direction = target - from();
                if (direction.LengthSquared() < 1e-8f)
                    throw McpError.Invalid("look_at " + McpRegion.Format(target) + " is where the entity stands, so there is no direction to face.");
            }
            if (direction.LengthSquared() < 1e-12f)
                throw McpError.Invalid("'forward' is a zero vector: give the direction to face.");
            Vector3 facing = InstanceTransform.LookRotation(direction);
            float roll = aim.Roll == null ? 0f : (float)McpValues.ReadDouble(aim.Roll, "roll");
            return McpValues.Vector(new Vector3(facing.X, facing.Y, roll));
        }

        private static void FillSequence(McpScriptEdit edit, Composite composite, Entity entity, JToken token, Scope scope)
        {
            if (!(token is JObject spec))
                throw McpError.Invalid("'sequence' is an object: {\"entries\": [{\"path\": ..., \"delay\": ...}], \"methods\": [...]}.");
            SequenceOf(edit.Commands, composite, entity);
            List<(ShortGuid[], float)> entries = null;
            if (spec["entries"] != null)
            {
                entries = new List<(ShortGuid[], float)>();
                foreach (JToken item in spec["entries"] as JArray ?? new JArray(spec["entries"]))
                {
                    JObject entry = item as JObject;
                    JToken pathToken = entry != null ? entry["path"] : item;
                    if (pathToken == null)
                        throw McpError.Invalid("Each sequence entry needs 'path'.");
                    bool fromRoot = Flag(entry, "from_root");
                    entries.Add((SequencePath(edit.Commands, composite, ExpandToken(pathToken, composite, scope, fromRoot), fromRoot), entry?["delay"] != null ? (float)McpValues.ReadDouble(entry["delay"], "delay") : 0f));
                }
            }
            List<string> methods = spec["methods"] == null ? null : (spec["methods"] as JArray ?? new JArray(spec["methods"])).Select(o => McpValues.ReadString(o)).ToList();
            if (entries != null || methods != null)
                edit.SetTriggerSequence(composite, entity, entries, methods, append: false);
        }

        /// <summary>A path token with a ref as its first step put as that entity's id (not for paths from the root).</summary>
        private static JToken ExpandToken(JToken token, Composite composite, Scope scope, bool fromRoot)
        {
            if (fromRoot) return token;
            return new JArray(ExpandRefs(ReadPath(token), composite, scope));
        }

        private static readonly Dictionary<string, CompositePinType> _pinTypes = new Dictionary<string, CompositePinType>(StringComparer.OrdinalIgnoreCase)
        {
            ["method"] = CompositePinType.CompositeMethodPin,
            ["target"] = CompositePinType.CompositeTargetPin,
            ["reference"] = CompositePinType.CompositeReferencePin,
            ["input_bool"] = CompositePinType.CompositeInputBoolVariablePin,
            ["input_int"] = CompositePinType.CompositeInputIntVariablePin,
            ["input_integer"] = CompositePinType.CompositeInputIntVariablePin,
            ["input_float"] = CompositePinType.CompositeInputFloatVariablePin,
            ["input_string"] = CompositePinType.CompositeInputStringVariablePin,
            ["input_position"] = CompositePinType.CompositeInputPositionVariablePin,
            ["input_direction"] = CompositePinType.CompositeInputDirectionVariablePin,
            ["input_enum"] = CompositePinType.CompositeInputEnumVariablePin,
            ["input_enum_string"] = CompositePinType.CompositeInputEnumStringVariablePin,
            ["input_object"] = CompositePinType.CompositeInputObjectVariablePin,
            ["input_animation_info"] = CompositePinType.CompositeInputAnimationInfoVariablePin,
            ["input_zone"] = CompositePinType.CompositeInputZonePtrVariablePin,
            ["input_zone_link"] = CompositePinType.CompositeInputZoneLinkPtrVariablePin,
            ["output_bool"] = CompositePinType.CompositeOutputBoolVariablePin,
            ["output_int"] = CompositePinType.CompositeOutputIntVariablePin,
            ["output_integer"] = CompositePinType.CompositeOutputIntVariablePin,
            ["output_float"] = CompositePinType.CompositeOutputFloatVariablePin,
            ["output_string"] = CompositePinType.CompositeOutputStringVariablePin,
            ["output_position"] = CompositePinType.CompositeOutputPositionVariablePin,
            ["output_direction"] = CompositePinType.CompositeOutputDirectionVariablePin,
            ["output_enum"] = CompositePinType.CompositeOutputEnumVariablePin,
            ["output_enum_string"] = CompositePinType.CompositeOutputEnumStringVariablePin,
            ["output_object"] = CompositePinType.CompositeOutputObjectVariablePin,
            ["output_animation_info"] = CompositePinType.CompositeOutputAnimationInfoVariablePin,
            ["output_zone"] = CompositePinType.CompositeOutputZonePtrVariablePin,
            ["output_zone_link"] = CompositePinType.CompositeOutputZoneLinkPtrVariablePin,
        };

        private static CompositePinType ParsePinType(string text)
        {
            string key = (text ?? "").Trim().Replace(' ', '_').Replace(':', '_').Replace('-', '_');
            if (_pinTypes.TryGetValue(key, out CompositePinType pin))
                return pin;
            if (Enum.TryParse(key, true, out CompositePinType full) && Enum.IsDefined(typeof(CompositePinType), full))
                return full;
            throw McpError.NotFound("pin type", text, _pinTypes.Keys.Where(o => !o.EndsWith("_integer")), "Use one of: " + string.Join(", ", _pinTypes.Keys.Where(o => !o.EndsWith("_integer"))) + ".");
        }
        #endregion

        #region Parameters
        /// <summary>
        /// Set a parameter as the inspector lets a user: a value the save or build works out itself (see
        /// EntityParameterVisibility) is refused unless allowed. Notes on values the game will not recognise go to <paramref name="notes"/>.
        /// </summary>
        internal static ParameterData WriteParameter(McpScriptEdit edit, Composite composite, Entity entity, string name, JToken value, bool allowCustom, List<string> notes = null)
        {
            Scope scope = new Scope();
            ParameterData written = Write(edit, composite, entity, name, value, allowCustom, scope);
            if (notes != null)
                foreach (string note in scope.Notes) if (!notes.Contains(note)) notes.Add(note);
            return written;
        }

        /// <summary>The function type an entity is, or stands for (an alias or proxy of one); null for instances and variables.</summary>
        private static FunctionType? FunctionOf(Commands commands, Composite composite, Entity entity)
        {
            Entity resolved = entity;
            if (entity is AliasEntity || entity is ProxyEntity)
                resolved = commands.Utils.GetResolvedTarget(commands.Utils.ResolveAliasOrProxy(entity, composite)).Item2;
            if (resolved is FunctionEntity function && function.function.IsFunctionType)
                return function.function.AsFunctionType;
            return null;
        }

        private static readonly ShortGuid AnimationSet = ShortGuidUtils.Generate("AnimationSet"), Animation = ShortGuidUtils.Generate("Animation"),
            AnimSet = ShortGuidUtils.Generate("anim_set"), DataFile = ShortGuidUtils.Generate("data_file"), GoboTexture = ShortGuidUtils.Generate("gobo_texture"), Texture = ShortGuidUtils.Generate("Texture");

        private static ParameterData Write(McpScriptEdit edit, Composite composite, Entity entity, string name, JToken value, bool allowCustom, Scope scope)
        {
            Commands commands = edit.Commands;
            ShortGuid id = McpScript.ParamId(name);
            if (!allowCustom && EntityParameterVisibility.IsHiddenFromEditor(entity, id))
                throw new McpError(McpErrorCodes.Refused, "'" + McpScript.ParamName(id) + "' on " + McpScript.TypeName(commands, composite, entity) + " is worked out by OpenCAGE when the level is saved or built, so the editor does not offer it. Pass allow_custom: true to write it anyway.");
            FunctionType? type = FunctionOf(commands, composite, entity);
            string text = value != null && value.Type == JTokenType.String ? ((string)value).Trim() : null;
            //Texture paths into the build: a texture's name (as list_textures gives it) is accepted too
            if (type == FunctionType.EnvironmentMap && id == Texture && !string.IsNullOrEmpty(text) && text.Replace('/', '\\').IndexOf("content\\build\\textures\\", StringComparison.OrdinalIgnoreCase) < 0)
            {
                Textures.TEX4 texture = McpAssets.FindTexture(McpEditor.RequireLevel(forEditing: false).Level, text);
                value = McpAssets.EnvironmentMapPath(texture);
            }
            else if (type == FunctionType.LightReference && id == GoboTexture && !string.IsNullOrEmpty(text))
                value = GoboPath(text, scope);
            //Asked before the write: once written, this entity would count as one that plays the clip
            if (type == FunctionType.CameraPlayAnimation && id == DataFile && !string.IsNullOrEmpty(text))
                scope.Note(McpGlobalAssetChecks.CameraClipNote(commands, text));

            ParameterData written = edit.SetParameter(composite, entity, name, value, allowCustom);

            //Instancing reads a value parameter that has a link out of it from that link's other end, not from the value set on it
            if (!(entity is VariableEntity) && id != ShortGuids.name)
            {
                JToken source = McpScript.FedBy(commands, composite, entity)?[McpScript.ParamName(id)];
                if (source != null)
                    scope.Note(McpScript.EntityName(commands, composite, entity) + "." + McpScript.ParamName(id) + " was written, but a link feeds it, so the game reads that instead: " + (string)source + ". Set the value at that source" +
                        (type == FunctionType.LightReference ? " (set_lights changes a light's colour and intensity where they are really held)" : "") + ", or take the link away (remove_links) for this value to count.");
            }

            //What it plays: checked as a whole once the call's changes are in (a set-only change re-checks the stored clip)
            bool playsAnimation = entity is FunctionEntity && ((id == AnimationSet || id == Animation) && (type == FunctionType.CMD_PlayAnimation || type == FunctionType.CHR_PlaySecondaryAnimation || type == FunctionType.PlayEnvironmentAnimation)
                || (id == AnimSet && type == FunctionType.Character));
            if (playsAnimation)
                scope.AnimationChecks.Add((composite, entity));

            //Named values: checked against what the game has, by the parameter's own type (a loaded value is a plain string).
            //What an animation entity plays is checked with its context once the change is in, when the animations are loaded
            string stored = (written as cString)?.value;
            if (!string.IsNullOrWhiteSpace(stored) && !(playsAnimation && Singleton.Animations != null && Singleton.Animations.Loaded))
            {
                ShortGuid? enumId = (written as cEnumString)?.enumID;
                if (enumId == null)
                {
                    try { enumId = (commands.Utils.CreateDefaultParameterData(entity, composite, id) as cEnumString)?.enumID; } catch { }
                }
                if (enumId != null)
                {
                    EnumStringType enumType = (EnumStringType)enumId.Value.AsUInt32;
                    string problem = enumType == EnumStringType.ANIMATION || enumType == EnumStringType.ANIMATION_SET || enumType == EnumStringType.SOUND_EVENT
                        ? McpGlobalAssetChecks.UnknownValueWarning(enumType, stored)
                        : UnknownEnumString(enumType, stored);
                    if (problem != null)
                        scope.Note(McpScript.EntityName(commands, composite, entity) + "." + McpScript.ParamName(id) + ": " + problem);
                }
            }
            return written;
        }

        /// <summary>
        /// A LightReference gobo_texture value: the texture's name (level table first, then the global textures, as the light
        /// material resolves it) as the build path the game stores, 'N:/Content/Build/Textures/...'. A name nothing has is
        /// written as given, with a note.
        /// </summary>
        private static string GoboPath(string value, Scope scope)
        {
            string name = value.Replace('\\', '/');
            int build = name.IndexOf("content/build/textures/", StringComparison.OrdinalIgnoreCase);
            if (build >= 0) name = name.Substring(build + "content/build/textures/".Length);
            Level level = McpEditor.RequireLevel(forEditing: false).Level;
            Textures.TEX4 Find(Textures table)
            {
                if (table?.Entries == null) return null;
                Textures.TEX4 exact = table.Entries.FirstOrDefault(o => o?.Name != null && string.Equals(o.Name.Replace('\\', '/'), name, StringComparison.OrdinalIgnoreCase));
                if (exact != null) return exact;
                //By its file name, with or without the extension, when that picks out one
                string wanted = System.IO.Path.GetFileName(name);
                bool bare = !System.IO.Path.HasExtension(wanted);
                List<Textures.TEX4> leaf = table.Entries.Where(o => o?.Name != null && string.Equals(bare ? System.IO.Path.GetFileNameWithoutExtension(o.Name.Replace('\\', '/')) : System.IO.Path.GetFileName(o.Name.Replace('\\', '/')), wanted, StringComparison.OrdinalIgnoreCase)).ToList();
                return leaf.Count == 1 ? leaf[0] : null;
            }
            Textures.TEX4 texture = Find(level.Textures) ?? Find(Singleton.Global?.Textures);
            if (texture == null)
            {
                List<string> names = (level.Textures?.Entries ?? new List<Textures.TEX4>()).Concat(Singleton.Global?.Textures?.Entries ?? new List<Textures.TEX4>()).Where(o => o?.Name != null).Select(o => o.Name.Replace('\\', '/')).ToList();
                scope.Note("gobo_texture: neither the level nor the global textures have '" + name + "', so the light will draw no gobo." + McpNames.DidYouMean(names.Where(o => o.IndexOf("gobo", StringComparison.OrdinalIgnoreCase) >= 0 || names.Count < 2000), System.IO.Path.GetFileNameWithoutExtension(name)) + " (list_textures shows the level's; import_texture adds one.)");
                return build >= 0 ? value : "N:/Content/Build/Textures/" + name;
            }
            return "N:/Content/Build/Textures/" + texture.Name.Replace('\\', '/');
        }

        /// <summary>Why an enum-string value is not one the inspector's picker offers, or null (also when there is no list to check against).</summary>
        private static string UnknownEnumString(EnumStringType type, string value)
        {
            if (!Enum.IsDefined(typeof(EnumStringType), type) || type == EnumStringType.MATERIAL)
                return null;
            try
            {
                EnumStringListViewItems.PopulateGlobalEntries();
                EnumStringListViewItems.PopulateLevelSpecificEntries();
            }
            catch (Exception e)
            {
                Debug.Log("MCP", "Could not list " + type + " values to check one: " + e.Message);
                return null;
            }
            Tuple<ListViewItem[], bool> items = EnumStringListViewItems.GetItems(type);
            if (items == null || items.Item1.Length == 0 || items.Item1.Any(o => string.Equals(o.Text, value, StringComparison.OrdinalIgnoreCase)))
                return null;
            return "'" + value + "' is not a " + type + " value OpenCAGE knows (list_enum_string_values " + type + " lists them); it was written as given." + McpNames.DidYouMean(items.Item1.Select(o => o.Text), value);
        }

        /// <summary>A transform parameter, as far as world space goes: the value it holds (or would) is a transform.</summary>
        private static bool IsTransform(McpScriptEdit edit, Composite composite, Entity entity, ShortGuid id) =>
            id == ShortGuids.position || edit.CurrentValue(composite, entity, id) is cTransform;

        private static JObject StepSetParameters(McpScriptEdit edit, McpCall args, Scope scope)
        {
            Commands commands = edit.Commands;
            bool allowCustom = args.Bool("allow_custom");
            List<JObject> changes = new List<JObject>();
            if (args.Has("entity") || args.Has("path"))
            {
                if (args.Has("entity") && args.Has("path"))
                    throw McpError.Invalid("Give 'entity' or 'path', not both.");
                JObject single = new JObject() { ["parameters"] = args.Object("parameters", required: true) };
                foreach (string field in new[] { "entity", "path", "space", "placement" })
                    if (args.Has(field)) single[field] = args.Token(field);
                changes.Add(single);
            }
            else if (args.Has("parameters"))
                throw McpError.Invalid("'parameters' goes with 'entity' or 'path' (or use 'changes' for several entities).");
            foreach (JToken token in args.Array("changes"))
            {
                if (!(token is JObject change))
                    throw McpError.Invalid("Each change is an object: {\"entity\": ..., \"parameters\": {...}}.");
                if ((change["entity"] == null) == (change["path"] == null))
                    throw McpError.Invalid("Each change needs 'entity' or 'path' (one of them).");
                if (!(change["parameters"] is JObject))
                    throw McpError.Invalid("Each change needs 'parameters' (an object of values by name).");
                changes.Add(change);
            }
            if (changes.Count == 0)
                throw McpError.Invalid("Give 'entity' (or 'path') and 'parameters', or 'changes'.");

            string defaultComposite = args.Str("composite");
            JArray results = new JArray();
            foreach (JObject change in changes)
            {
                string compositeName = Field(change, "composite") ?? defaultComposite ?? throw McpError.Invalid("Say which composite: 'composite', on the call or on the change.");
                Composite composite = FindComposite(commands, compositeName);
                JObject via = null;
                Entity entity = change["path"] != null ? EntityOnPath(edit, composite, change["path"], scope, create: true, out via) : Resolve(edit, composite, Field(change, "entity"), scope);
                bool world = ReadWorld(change["space"] ?? args.Token("space"), "space");
                JToken placement = change["placement"] ?? args.Token("placement");
                cTransform frame = null;
                JObject described = null;
                JObject written = new JObject();
                JObject worldNow = null;
                List<string> notTransforms = new List<string>();
                bool needsBuild = false;
                foreach (JProperty parameter in ((JObject)change["parameters"]).Properties())
                {
                    ShortGuid id = McpScript.ParamId(parameter.Name);
                    JToken value = parameter.Value;
                    //A transform facing a point or a direction: its rotation is worked out first, in the space it is given in
                    Aiming aim = id == ShortGuids.name ? null : Aiming.Of(value);
                    if (aim != null && IsTransform(edit, composite, entity, id))
                    {
                        JObject given = (JObject)value;
                        if (world && frame == null) frame = FrameFor(commands, composite, entity, placement, out described);
                        JToken position = given["position"] != null && given["position"].Type != JTokenType.Null ? given["position"] : null;
                        ShortGuid param = id;
                        JToken rotation = Aim(given["rotation"], aim, () =>
                        {
                            if (position != null) return McpValues.ReadVector(position, parameter.Name + ".position", null);
                            Vector3 local = (edit.CurrentValue(composite, entity, param) as cTransform)?.position ?? Vector3.Zero;
                            return world ? InstanceTransform.PointToWorld(frame, local) : local;
                        }, parameter.Name);
                        JObject rebuilt = new JObject();
                        foreach (JProperty part in given.Properties())
                            if (part.Name != "look_at" && part.Name != "forward" && part.Name != "roll" && part.Name != "rotation")
                                rebuilt[part.Name] = part.Value;
                        if (rotation != null) rebuilt["rotation"] = rotation;
                        value = rebuilt;
                    }
                    if (world && id != ShortGuids.name)
                    {
                        if (IsTransform(edit, composite, entity, id))
                        {
                            if (frame == null) frame = FrameFor(commands, composite, entity, placement, out described);
                            value = ToFrame(frame, value, parameter.Name);
                        }
                        else
                            notTransforms.Add(parameter.Name);
                    }
                    ParameterData data = Write(edit, composite, entity, parameter.Name, value, allowCustom, scope);
                    written[McpScript.ParamName(id)] = McpValues.ToJson(data, commands);
                    if (world && frame != null && data is cTransform)
                        worldNow = WorldOf(edit, composite, entity, id, frame, described);
                    //A value the game will not read where something else decides it
                    List<string> reasons = edit.WhyValueIgnored(composite, entity, id);
                    if (reasons.Count != 0)
                        scope.Note(McpScript.EntityName(commands, composite, entity) + "." + McpScript.ParamName(id) + " was written, but the game may not use it: " + string.Join("; ", reasons) + ".");
                    if (!needsBuild && NeedsBuild(commands, composite, entity, id, scope))
                        needsBuild = true;
                }
                if (notTransforms.Count != 0)
                    scope.Note("space 'world' applies to transforms (position): " + string.Join(", ", notTransforms) + " written as given.");
                edit.Select.Add(entity);
                JObject result = new JObject() { ["entity"] = McpScript.Id(entity.shortGUID), ["name"] = McpScript.EntityName(commands, composite, entity) };
                if (changes.Count > 1 || Field(change, "composite") != null) result["composite"] = composite.name;
                if (via != null) result["via_alias"] = via;
                result["set"] = written;
                if (worldNow != null) result["world"] = worldNow;
                //The values written that a link feeds instead, as instancing reads them (the notes say what to do)
                JObject fedBy = entity is VariableEntity ? null : McpScript.FedBy(commands, composite, entity);
                if (fedBy != null)
                {
                    JObject fed = new JObject(fedBy.Properties().Where(o => written[o.Name] != null));
                    if (fed.Count != 0) result["fed_by"] = fed;
                }
                //Every placement of the composite takes the change (one through an alias: those the alias's composite has)
                result["placements_affected"] = PlacementsOf(commands, composite, scope);
                if (needsBuild)
                {
                    result["needs_build"] = true;
                    scope.Built.Add(McpScript.EntityName(commands, composite, entity));
                }
                results.Add(result);
            }
            return new JObject() { ["changed"] = results };
        }

        /// <summary>How many times the level places a composite (worked out once a call).</summary>
        private static int PlacementsOf(Commands commands, Composite composite, Scope scope)
        {
            if (!scope.Placements.TryGetValue(composite, out int placements))
                scope.Placements[composite] = placements = McpScriptEdit.PlacementCount(commands, composite);
            return placements;
        }

        /// <summary>Whether a composite, or anything it places, holds content the build reads.</summary>
        private static bool Carries(Commands commands, Composite composite, Scope scope)
        {
            if (!scope.Carries.TryGetValue(composite, out bool holds))
                scope.Carries[composite] = holds = McpBrowseTools.ReachableFrom(commands, composite).Any(c => c.functions.Any(f => f.function.IsFunctionType && _builtTypes.Contains(f.function.AsFunctionType)));
            return holds;
        }

        /// <summary>
        /// Whether a new entity is content the game only takes after a Save &amp; Build: one the build reads, or an instance of a
        /// composite holding such, made in a composite the level places (an unplaced one's content reaches the game through the
        /// instance that places it, which says so then).
        /// </summary>
        private static bool NewContentNeedsBuild(Commands commands, Composite composite, Entity entity, Scope scope)
        {
            if (entity is AliasEntity || entity is ProxyEntity || entity is VariableEntity || PlacementsOf(commands, composite, scope) == 0)
                return false;
            FunctionType? type = FunctionOf(commands, composite, entity);
            if (type != null)
                return _builtTypes.Contains(type.Value);
            Composite instanced = McpScript.InstancedComposite(commands, entity);
            return instanced != null && Carries(commands, instanced, scope);
        }

        //The function types the build (instancing and its bakers) reads from the script, as BuildTracker fingerprints them
        private static readonly HashSet<FunctionType> _builtTypes = BuiltTypes();
        //What an instance's placement is made of: where it is, and the flags that keep its content out of the game
        private static readonly HashSet<ShortGuid> _placementParameters = new HashSet<ShortGuid>(new[] { "position", "deleted", "delete_me", "is_template", "is_shared", "delete_standard_collision", "delete_ballistic_collision" }.Select(o => ShortGuidUtils.Generate(o)));
        private static readonly ShortGuid MappingParameter = ShortGuidUtils.Generate("mapping"), MaterialParameter = ShortGuidUtils.Generate("material");

        private static HashSet<FunctionType> BuiltTypes()
        {
            string[] names =
            {
                "ModelReference", "EnvironmentModelReference", "LightReference", "ParticleEmitterReference", "RibbonEmitterReference", "ProjectiveDecal",
                "FogBox", "FogPlane", "FogSphere", "SimpleWater", "SimpleRefraction", "SurfaceEffectBox", "SurfaceEffectSphere", "ExclusiveMaster",
                "CollisionBarrier", "PhysicsSystem", "RadiosityProxy", "RadiosityIsland", "Zone",
                "NavMeshArea", "NavMeshBarrier", "NavMeshExclusionArea", "NavMeshReachabilitySeedPoint", "NavMeshWalkablePlatform",
                "PathfindingAlienBackstageNode", "PathfindingManualNode", "PathfindingTeleportNode", "PathfindingWaitNode",
                "CoverExclusionArea", "CoverLine", "SpottingExclusionArea", "JOB_Assault", "JOB_SpottingPosition",
                "SoundBarrier", "SoundEnvironmentMarker", "SoundNetworkNode", "SoundLevelInitialiser",
            };
            HashSet<FunctionType> types = new HashSet<FunctionType>();
            foreach (string name in names)
                if (Enum.TryParse(name, out FunctionType type))
                    types.Add(type);
            foreach (FunctionType type in Enum.GetValues(typeof(FunctionType)))
                if (type.ToString().StartsWith("TRAV_", StringComparison.Ordinal))
                    types.Add(type);
            return types;
        }

        /// <summary>
        /// Whether the game only takes this parameter after a Save &amp; Build: it is on an entity the build reads (a model, light,
        /// effect, collision or nav entity - or an alias or proxy of one), it is a model's mapping or material, or it places an
        /// instance of a composite that holds such content.
        /// </summary>
        private static bool NeedsBuild(Commands commands, Composite composite, Entity entity, ShortGuid id, Scope scope)
        {
            if (id == ShortGuids.name)
                return false;
            if (id == MappingParameter || id == MaterialParameter)
                return true;
            FunctionType? type = FunctionOf(commands, composite, entity);
            if (type != null)
                return _builtTypes.Contains(type.Value);
            if (!_placementParameters.Contains(id))
                return false;
            Entity resolved = entity;
            if (entity is AliasEntity || entity is ProxyEntity)
                resolved = commands.Utils.GetResolvedTarget(commands.Utils.ResolveAliasOrProxy(entity, composite)).Item2;
            Composite instanced = resolved == null ? null : McpScript.InstancedComposite(commands, resolved);
            return instanced != null && Carries(commands, instanced, scope);
        }

        private static JObject StepRemoveParameters(McpScriptEdit edit, McpCall args, Scope scope)
        {
            Commands commands = edit.Commands;
            List<string> names = args.StrList("parameters", required: true);
            bool reset = args.Bool("reset");
            Composite composite = FindComposite(commands, args.Str("composite", required: true));
            if (args.Has("entity") == args.Has("path"))
                throw McpError.Invalid("Give 'entity' or 'path' (one of them).");
            JObject via = null;
            Entity entity = args.Has("path") ? EntityOnPath(edit, composite, args.Token("path"), scope, create: false, out via) : Resolve(edit, composite, args.Str("entity"), scope);
            if (entity == null)
                return new JObject() { ["not_present"] = new JArray(names), ["note"] = "No alias in " + composite.name + " overrides anything on that path, so there was nothing to remove." };

            List<string> removed = new List<string>(), resetTo = new List<string>(), noDefault = new List<string>();
            foreach (string name in names)
            {
                ShortGuid id = McpScript.ParamId(name);
                Parameter parameter = entity.GetParameter(id);
                if (parameter == null)
                    continue;
                //An alias's parameters are overrides: resetting one removes it, so what it points at applies (as the inspector does)
                if (!reset || entity is AliasEntity)
                {
                    if (edit.RemoveParameter(composite, entity, name)) removed.Add(name);
                    continue;
                }
                ParameterData fallback = commands.Utils.CreateDefaultParameterData(entity, composite, id);
                if (fallback == null)
                {
                    noDefault.Add(name);
                    continue;
                }
                //The same parameter kept in its place with a new value object (the undo snapshot holds the old one); removing
                //it first is what clears its "set by hand" mark as part of this step
                int index = entity.parameters.IndexOf(parameter);
                edit.RemoveParameter(composite, entity, name);
                parameter.content = fallback;
                entity.parameters.Insert(Math.Max(0, Math.Min(index, entity.parameters.Count)), parameter);
                resetTo.Add(name);
            }
            edit.Select.Add(entity);
            JObject result = new JObject() { ["entity"] = McpScript.Id(entity.shortGUID), ["name"] = McpScript.EntityName(commands, composite, entity) };
            if (via != null) result["via_alias"] = via;
            result["not_present"] = new JArray(names.Where(o => !removed.Contains(o) && !resetTo.Contains(o) && !noDefault.Contains(o)));
            if (!reset || removed.Count != 0) result["removed"] = new JArray(removed);
            if (reset)
            {
                result["reset"] = new JArray(resetTo);
                if (noDefault.Count != 0) result["no_default"] = new JArray(noDefault.Select(o => o + " (not a parameter of its type, so it has no default: remove it instead)"));
                if (resetTo.Count != 0)
                {
                    JObject values = new JObject();
                    foreach (string name in resetTo)
                        values[McpScript.ParamName(McpScript.ParamId(name))] = McpValues.ToJson(entity.GetParameter(McpScript.ParamId(name))?.content, commands);
                    result["values"] = values;
                }
            }
            return result;
        }

        private static JObject StepRename(McpScriptEdit edit, McpCall args, Scope scope)
        {
            string name = args.Str("name", required: true);
            Composite composite = FindComposite(edit.Commands, args.Str("composite", required: true));
            Entity entity = Resolve(edit, composite, args.Str("entity", required: true), scope);
            edit.Rename(composite, entity, name);
            edit.Select.Add(entity);
            return new JObject() { ["renamed"] = McpScript.Id(entity.shortGUID), ["name"] = name };
        }

        private static JObject StepRenamePin(McpScriptEdit edit, McpCall args, Scope scope)
        {
            Commands commands = edit.Commands;
            Composite composite = FindComposite(commands, args.Str("composite", required: true));
            string pin = args.Str("pin", required: true);
            Entity found;
            try { found = Resolve(edit, composite, pin, scope); }
            catch (McpError) when (composite.variables_dictionary.Count != 0)
            {
                throw McpError.NotFound("pin of " + composite.name + " called", pin, composite.variables_dictionary.Values.Select(o => McpScript.ParamName(o.name)), "Its pins: " + string.Join(", ", composite.variables_dictionary.Values.Select(o => McpScript.ParamName(o.name)).Take(40)) + ".");
            }
            if (!(found is VariableEntity variable))
                throw McpError.Invalid(McpScript.EntityName(commands, composite, found) + " is a " + McpScript.Kind(found) + ", not a pin (variable) of " + composite.name + ": rename_entity renames it.");
            string old = McpScript.ParamName(variable.name);
            McpScriptEdit.PinRename report = edit.RenamePin(composite, variable, args.Str("name", required: true));
            JObject result = new JObject()
            {
                ["composite"] = composite.name,
                ["renamed"] = old,
                ["to"] = McpScript.ParamName(variable.name),
                ["instances"] = report.Instances,
                ["links"] = report.Links,
                ["values"] = report.Values,
                ["overrides"] = report.Overrides,
                ["animation_bindings"] = report.Animations,
            };
            if (report.Composites.Count != 0) result["also_changed"] = new JArray(report.Composites.Distinct().Take(50));
            return result;
        }
        #endregion

        #region Deleting
        private static JObject StepDelete(McpScriptEdit edit, McpCall args, Scope scope)
        {
            Commands commands = edit.Commands;
            JArray items = args.Array("entities", required: true);
            if (items.Count == 0)
                throw McpError.Invalid("'entities' is empty.");
            string defaultComposite = args.Str("composite");
            List<(Composite composite, Entity entity)> targets = new List<(Composite, Entity)>();
            foreach (JToken item in items)
            {
                JObject spec = item as JObject;
                string compositeName = (spec != null ? Field(spec, "composite") : null) ?? defaultComposite ?? throw McpError.Invalid("Say which composite: 'composite', on the call or on the entity ({\"composite\": ..., \"entity\": ...}).");
                Composite composite = FindComposite(commands, compositeName);
                string reference = spec != null ? (Field(spec, "entity") ?? throw McpError.Invalid("Each {composite, entity} needs 'entity'.")) : McpValues.ReadString(item);
                Entity entity = Resolve(edit, composite, reference, scope);
                if (!targets.Contains((composite, entity))) targets.Add((composite, entity));
            }
            JArray deleted = new JArray();
            int alsoCount = 0;
            foreach ((Composite composite, Entity entity) in targets)
            {
                //Read while it is still there (an alias is named after what it points at)
                JObject item = new JObject()
                {
                    ["id"] = McpScript.Id(entity.shortGUID),
                    ["name"] = McpScript.EntityName(commands, composite, entity),
                    ["kind"] = McpScript.Kind(entity),
                    ["type"] = McpScript.TypeName(commands, composite, entity),
                    ["composite"] = composite.name,
                };
                List<string> leftDead = new List<string>();
                List<string> extra = edit.Delete(composite, entity, leftDead);
                if (extra.Count != 0) item["also"] = new JArray(extra);
                if (leftDead.Count != 0) item["left_dead"] = new JArray(leftDead);
                alsoCount += extra.Count;
                deleted.Add(item);
            }
            JObject result = new JObject() { ["deleted"] = deleted };
            if (alsoCount != 0) result["also_removed"] = alsoCount;
            return result;
        }
        #endregion

        #region Links
        /// <summary>Make one link from its spec; refs name entities made earlier in the call. Its ends may be reached through an alias or proxy found or made for it.</summary>
        private static JObject Link(McpScriptEdit edit, Composite composite, JToken token, Scope scope, bool allowCustom, bool perLinkComposite)
        {
            if (!(token is JObject spec))
                throw McpError.Invalid("Each link is an object: {\"from\", \"param\", \"to\", \"to_param\"}.");
            Commands commands = edit.Commands;
            Composite home = perLinkComposite && Field(spec, "composite") != null ? FindComposite(commands, Field(spec, "composite")) : composite;
            if (home == null)
                throw McpError.Invalid("Say which composite the link is in: 'composite', on the call or on the link.");
            JObject via = new JObject();
            Entity End(string field, string pathField, string rootField)
            {
                bool hasEntity = spec[field] != null && spec[field].Type != JTokenType.Null, hasPath = spec[pathField] != null && spec[pathField].Type != JTokenType.Null;
                if (hasEntity == hasPath)
                    throw McpError.Invalid("A link needs '" + field + "' (an entity of " + home.name + ") or '" + pathField + "' (a path to one inside a nested instance; with " + rootField + ", from the level's root) - one of them.");
                if (hasEntity)
                    return Resolve(edit, home, Field(spec, field), scope);
                bool fromRoot = Flag(spec, rootField);
                Entity reached;
                JObject made;
                if (fromRoot && home != commands.EntryPoints[0])
                {
                    List<string> steps = ReadPath(spec[pathField]);
                    ShortGuid[] path = McpScript.ResolvePath(commands, commands.EntryPoints[0], steps, out Composite targetComposite, out Entity target);
                    if (!(target is FunctionEntity))
                        throw McpError.Invalid("A proxy can only stand for a function entity or composite instance ('" + string.Join("/", steps) + "' is a " + McpScript.Kind(target) + ").");
                    ProxyEntity proxy = edit.FindOrAddProxy(home, path, null, out bool created);
                    reached = proxy;
                    made = new JObject() { ["proxy"] = McpScript.Id(proxy.shortGUID), ["name"] = McpScript.EntityName(commands, home, proxy), ["created"] = created };
                }
                else
                    reached = EntityOnPath(edit, home, spec[pathField], scope, create: true, out made);
                if (made != null) via[field] = made;
                return reached;
            }
            Entity from = End("from", "from_path", "from_root");
            Entity to = End("to", "to_path", "to_root");
            string param = Field(spec, "param") ?? throw McpError.Invalid("A link needs 'param' (the source's pin).");
            string toParam = Field(spec, "to_param") ?? throw McpError.Invalid("A link needs 'to_param' (the target's pin).");
            bool custom = allowCustom || Flag(spec, "allow_custom");
            bool added = edit.AddLink(home, from, param, to, toParam, custom);
            JObject result = new JObject()
            {
                ["link"] = McpScript.EntityName(commands, home, from) + "." + param + " -> " + McpScript.EntityName(commands, home, to) + "." + toParam,
                ["added"] = added,
            };
            if (home != composite) result["composite"] = home.name;
            if (via.Count != 0) result["via"] = via;
            return result;
        }

        private static JObject StepAddLinks(McpScriptEdit edit, McpCall args, Scope scope)
        {
            Composite composite = args.Has("composite") ? FindComposite(edit.Commands, args.Str("composite")) : null;
            JArray links = args.Array("links", required: true);
            if (links.Count == 0)
                throw McpError.Invalid("'links' is empty.");
            if (composite != null) edit.UsePage(composite, args.Str("page"));
            JArray made = new JArray();
            foreach (JToken link in links)
                made.Add(Link(edit, composite, link, scope, args.Bool("allow_custom"), perLinkComposite: true));
            JObject result = new JObject() { ["links"] = made };
            if (composite != null) result["composite"] = composite.name;
            return result;
        }

        private static JObject StepRemoveLinks(McpScriptEdit edit, McpCall args, Scope scope)
        {
            Commands commands = edit.Commands;
            string defaultComposite = args.Str("composite");
            JArray removed = new JArray();
            JArray unmatched = new JArray();
            foreach (JToken token in args.Array("links", required: true))
            {
                if (!(token is JObject link))
                    throw McpError.Invalid("Each link to remove is an object: {\"from\", \"param\", \"to\", \"to_param\"} (give from or to).");
                Composite composite = FindComposite(commands, Field(link, "composite") ?? defaultComposite ?? throw McpError.Invalid("Say which composite: 'composite', on the call or on the link."));
                string fromName = Field(link, "from"), toName = Field(link, "to");
                if (fromName == null && toName == null)
                    throw McpError.Invalid("Each link to remove needs 'from' or 'to' (or both).");
                Entity from = fromName == null ? null : Resolve(edit, composite, fromName, scope);
                Entity to = toName == null ? null : Resolve(edit, composite, toName, scope);
                List<string> gone = new List<string>();
                foreach (Entity owner in from != null ? new List<Entity>() { from } : composite.GetEntities())
                    gone.AddRange(edit.RemoveLinksListed(composite, owner, Field(link, "param"), to, Field(link, "to_param")));
                foreach (string text in gone) removed.Add(text);
                if (gone.Count == 0)
                    unmatched.Add((fromName ?? "*") + "." + (Field(link, "param") ?? "*") + " -> " + (toName ?? "*") + "." + (Field(link, "to_param") ?? "*") + " in " + composite.name);
            }
            JObject result = new JObject() { ["removed"] = removed, ["count"] = removed.Count };
            if (unmatched.Count != 0) result["matched_nothing"] = unmatched;
            return result;
        }

        private static string PagesNow(Composite composite)
        {
            return McpEditor.UI(() =>
            {
                if (FlowgraphLayoutManager.HasCompatibilityInfo(composite))
                    return FlowgraphLayoutManager.IsCompatible(composite) ? "pages" : "links (no pages)";
                //Not judged yet: say what the editor will decide when it opens it
                return new RefactorEdit.EditorPageSource().PagesCarryLinks(composite) ? "pages" : "links (no pages)";
            });
        }
        #endregion

        #region Trigger sequences
        /// <summary>One entry of a sequence while an edit rearranges them.</summary>
        private sealed class Row
        {
            public ShortGuid[] Path;
            public float Delay;
            public bool Removed;
        }

        private static List<TriggerSequence.SequenceEntry> SequenceOf(Commands commands, Composite composite, Entity entity)
        {
            if (entity is TriggerSequence trigger) return trigger.sequence;
            if (entity is ProxyEntity proxy && proxy.function == FunctionType.TriggerSequence) return proxy.sequence;
            throw McpError.Invalid(McpScript.EntityName(commands, composite, entity) + " is a " + McpScript.TypeName(commands, composite, entity) + ", not a TriggerSequence.");
        }

        /// <summary>A sequence entry's stored path: from the sequence's composite, or (from_root) from the level root, as the editor's picker writes either.</summary>
        internal static ShortGuid[] SequencePath(Commands commands, Composite composite, JToken token, bool fromRoot)
        {
            List<string> steps = ReadPath(token);
            ShortGuid[] path = McpScript.ResolvePath(commands, fromRoot ? commands.EntryPoints[0] : composite, steps, out Composite _, out Entity target);
            if (!(target is FunctionEntity))
                throw McpError.Invalid("A trigger sequence entry has to be a function entity or composite instance ('" + string.Join("/", steps) + "' is a " + McpScript.Kind(target) + ").");
            return path;
        }

        /// <summary>The entities a stored entry passes through, read as the viewport reads it (its composite first, then the root).</summary>
        internal static List<Entity> ChainOf(Commands commands, Composite composite, ShortGuid[] path)
        {
            return commands.Utils.ResolveEntityPath(path, composite).Select(o => o.Item2).ToList();
        }

        internal static bool SameChain(List<Entity> a, List<Entity> b) => a.Count != 0 && a.Count == b.Count && !a.Where((o, i) => !ReferenceEquals(o, b[i])).Any();

        private static int ReadIndex(JToken token, string what, int count)
        {
            int index = McpValues.ReadInt(token, what);
            if (index < 0 || index >= count)
                throw McpError.Invalid(what + " " + index + " is not an entry: the sequence has " + count + (count == 0 ? "." : " (0 to " + (count - 1) + ")."));
            return index;
        }

        private static JObject StepTriggerSequence(McpScriptEdit edit, McpCall args, Scope scope)
        {
            bool hasEntries = args.Has("entries");
            bool append = args.Bool("append");
            bool insert = args.Has("insert_at");
            bool dedupe = args.Bool("dedupe", true);
            JArray removals = args.Array("remove");
            JArray updates = args.Array("update");
            if (append && insert)
                throw McpError.Invalid("Give append or insert_at, not both.");
            if (hasEntries && !append && !insert && (removals.Count != 0 || updates.Count != 0))
                throw McpError.Invalid("'entries' without append or insert_at replaces the whole list, so 'remove' and 'update' would have nothing to change. Pass append: true or insert_at to keep the existing entries.");
            if (insert && !hasEntries)
                throw McpError.Invalid("'insert_at' places new 'entries': give them too.");

            Commands commands = edit.Commands;
            Composite composite = FindComposite(commands, args.Str("composite", required: true));
            Entity entity = Resolve(edit, composite, args.Str("entity", required: true), scope);
            List<TriggerSequence.SequenceEntry> current = SequenceOf(commands, composite, entity);
            List<Row> rows = current.Select(o => new Row() { Path = (ShortGuid[])(o.connectedEntity?.path ?? new ShortGuid[0]).Clone(), Delay = o.timing }).ToList();
            JArray skipped = new JArray();
            int removedCount = 0;

            //Updates and removals name entries by where they were before this call
            List<(Row row, int to)> moves = new List<(Row, int)>();
            foreach (JToken token in updates)
            {
                if (!(token is JObject update))
                    throw McpError.Invalid("Each update is an object, e.g. {\"index\": 0, \"delay\": 2}.");
                if (update["index"] == null)
                    throw McpError.Invalid("Each update needs 'index'.");
                Row row = rows[ReadIndex(update["index"], "update index", rows.Count)];
                if (update["delay"] != null) row.Delay = (float)McpValues.ReadDouble(update["delay"], "delay");
                if (update["path"] != null) row.Path = SequencePath(commands, composite, ExpandToken(update["path"], composite, scope, Flag(update, "from_root")), Flag(update, "from_root"));
                if (update["move_to"] != null) moves.Add((row, McpValues.ReadInt(update["move_to"], "move_to")));
            }
            foreach (JToken token in removals)
            {
                if (token.Type == JTokenType.Integer)
                {
                    Row row = rows[ReadIndex(token, "remove index", rows.Count)];
                    if (!row.Removed) { row.Removed = true; removedCount++; }
                    continue;
                }
                JObject spec = token as JObject;
                JToken pathToken = spec != null ? spec["path"] : token;
                if (pathToken == null)
                    throw McpError.Invalid("Each 'remove' item is an index, a path, or {\"path\": ..., \"from_root\": true}.");
                bool fromRoot = Flag(spec, "from_root");
                List<Entity> chain = ChainOf(commands, composite, SequencePath(commands, composite, ExpandToken(pathToken, composite, scope, fromRoot), fromRoot));
                int matched = 0;
                foreach (Row row in rows.Where(o => !o.Removed && SameChain(ChainOf(commands, composite, o.Path), chain)))
                {
                    row.Removed = true;
                    matched++;
                }
                removedCount += matched;
                if (matched == 0)
                    skipped.Add("remove " + string.Join("/", ReadPath(pathToken)) + ": no entry names it" + (fromRoot ? " from the root" : " (an entry written from the root needs from_root)"));
            }
            List<Row> list = rows.Where(o => !o.Removed).ToList();
            foreach ((Row row, int to) in moves)
            {
                if (row.Removed) continue;
                list.Remove(row);
                list.Insert(Math.Max(0, Math.Min(to, list.Count)), row);
            }

            if (hasEntries)
            {
                List<Row> added = new List<Row>();
                List<Row> keeping = append || insert ? list : new List<Row>();
                foreach (JToken token in args.Array("entries"))
                {
                    JObject spec = token as JObject;
                    JToken pathToken = spec != null ? spec["path"] : token;
                    if (pathToken == null)
                        throw McpError.Invalid("Each entry needs 'path'.");
                    bool fromRoot = Flag(spec, "from_root");
                    Row row = new Row()
                    {
                        Path = SequencePath(commands, composite, ExpandToken(pathToken, composite, scope, fromRoot), fromRoot),
                        Delay = spec?["delay"] != null ? (float)McpValues.ReadDouble(spec["delay"], "delay") : 0f,
                    };
                    if (dedupe)
                    {
                        List<Entity> chain = ChainOf(commands, composite, row.Path);
                        if (keeping.Concat(added).Any(o => SameChain(ChainOf(commands, composite, o.Path), chain)))
                        {
                            skipped.Add("already in the sequence: " + string.Join("/", ReadPath(pathToken)));
                            continue;
                        }
                    }
                    added.Add(row);
                }
                if (append)
                    list.AddRange(added);
                else if (insert)
                {
                    int at = args.Int("insert_at");
                    if (at < 0 || at > list.Count)
                        throw McpError.Invalid("insert_at is 0 to " + list.Count + " (the entries there are before this call's additions).");
                    list.InsertRange(at, added);
                }
                else
                    list = added;
            }

            bool changed = list.Count != current.Count
                || list.Where((o, i) => o.Delay != current[i].timing || !o.Path.SequenceEqual(current[i].connectedEntity?.path ?? new ShortGuid[0])).Any();
            if (changed)
                edit.SetTriggerSequence(composite, entity, list.Select(o => (o.Path, o.Delay)).ToList(), null, append: false);
            if (args.Has("methods"))
            {
                List<string> methods = args.StrList("methods");
                List<TriggerSequence.MethodEntry> before = (entity as TriggerSequence)?.methods ?? (entity as ProxyEntity)?.methods ?? new List<TriggerSequence.MethodEntry>();
                bool same = !append && before.Count == methods.Count && before.Select(o => o.method).SequenceEqual(methods.Select(o => ShortGuidUtils.Generate(o.Trim())));
                bool nothingNew = append && methods.All(o => before.Any(m => m.method == ShortGuidUtils.Generate(o.Trim())));
                if (!same && !nothingNew)
                    edit.SetTriggerSequence(composite, entity, null, methods, append, args.Bool("drop_links"));
            }
            edit.Select.Add(entity);

            JObject result = McpScript.Describe(commands, composite, entity, parameters: false);
            if (removedCount != 0) result["removed"] = removedCount;
            if (skipped.Count != 0) result["skipped"] = skipped;
            return result;
        }
        #endregion

        #region Batch
        private static object Batch(McpCall call)
        {
            string label = (call.Str("label", required: true) ?? "").Trim();
            JArray steps = call.Array("steps", required: true);
            if (steps.Count == 0)
                throw McpError.Invalid("'steps' is empty.");
            if (steps.Count > 200)
                throw McpError.Invalid("A batch takes at most 200 steps; split it, or wrap several batches in undo_group.");

            List<(string tool, McpCall args)> parsed = new List<(string, McpCall)>();
            for (int i = 0; i < steps.Count; i++)
            {
                if (!(steps[i] is JObject step))
                    throw McpError.Invalid("Step " + i + " is not an object: {\"tool\": ..., \"arguments\": {...}}.");
                string tool = Field(step, "tool") ?? throw McpError.Invalid("Step " + i + " needs 'tool'.");
                if (!_steps.ContainsKey(tool))
                    throw McpError.NotFound("script tool for a batch step called", tool, _steps.Keys, "A batch runs " + string.Join(", ", _steps.Keys) + ".");
                JObject arguments = step["arguments"] as JObject ?? throw McpError.Invalid("Step " + i + " (" + tool + ") needs 'arguments': an object, as " + tool + " takes them.");
                McpTool definition = McpTools.Find(tool);
                foreach (string own in new[] { "show", "dry_run" })
                    if (arguments[own] != null)
                        throw McpError.Invalid("Step " + i + " (" + tool + "): '" + own + "' goes on script_batch itself, not on a step.");
                string invalid = definition == null ? null : McpValidation.Check(definition, arguments);
                if (invalid != null)
                    throw McpError.Invalid("Step " + i + " (" + tool + "): " + invalid);
                parsed.Add((tool.ToLowerInvariant(), new McpCall(definition, arguments, call.Cancel, null)));
            }

            List<Composite> touching = McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                List<Composite> all = new List<Composite>();
                foreach ((string tool, McpCall args) in parsed)
                    foreach (Composite composite in Prepare(commands, args, strict: false))
                        if (!all.Contains(composite)) all.Add(composite);
                if (all.Count == 0)
                    all.Add(commands.EntryPoints[0]);
                return all;
            });

            Scope scope = new Scope();
            JArray results = new JArray();
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run(label, touching[0], edit =>
            {
                for (int i = 0; i < parsed.Count; i++)
                {
                    call.ThrowIfCancelled();
                    (string tool, McpCall args) = parsed[i];
                    try
                    {
                        results.Add(new JObject() { ["step"] = i, ["tool"] = tool, ["result"] = _steps[tool](edit, args, scope) });
                    }
                    catch (McpError e)
                    {
                        throw new McpError(e.Code, "Step " + i + " (" + tool + ") failed, so nothing was changed: " + e.Message) { Candidates = e.Candidates };
                    }
                }
            }, Options(call, true, touching));
            JObject result = new JObject() { ["label"] = "AI: " + label, ["steps"] = results };
            return Finish(call, scope, outcome, result);
        }
        #endregion

        #region After the change
        //Content a zone has to claim for the game to stream it in: models, lights, effects, sounds, volumes
        private static readonly HashSet<FunctionType> _zonedTypes = new HashSet<FunctionType>()
        {
            FunctionType.ModelReference, FunctionType.EnvironmentModelReference, FunctionType.LightReference, FunctionType.ParticleEmitterReference,
            FunctionType.RibbonEmitterReference, FunctionType.GPU_PFXEmitterReference, FunctionType.Sound, FunctionType.SoundObject,
            FunctionType.FogBox, FunctionType.FogSphere, FunctionType.FogPlane, FunctionType.SimpleWater, FunctionType.SimpleRefraction, FunctionType.CollisionBarrier,
        };

        /// <summary>The checks that read the change as it now stands: animations played, and new content no zone claims. UI thread.</summary>
        private static void AfterChange(Commands commands, Scope scope)
        {
            foreach ((Composite composite, Entity entity) in scope.AnimationChecks.Distinct().ToList())
            {
                if (composite.GetEntityByID(entity.shortGUID) != entity) continue;
                foreach (string problem in McpGlobalAssetChecks.AnimationProblems(commands, composite, entity))
                    scope.Note(McpScript.EntityName(commands, composite, entity) + ": " + problem);
            }
            try { NoteUnzoned(commands, scope); }
            catch (Exception e) when (!(e is McpError)) { Debug.Log("MCP", "Could not check zone membership: " + e.Message); }
        }

        /// <summary>Whether content of this entity is drawn, lit, heard or collided with: a type zones stream, or an instance of a composite holding one.</summary>
        private static bool NeedsZone(Commands commands, Entity entity, Dictionary<Composite, bool> known)
        {
            if (!(entity is FunctionEntity function)) return false;
            if (function.function.IsFunctionType) return _zonedTypes.Contains(function.function.AsFunctionType);
            Composite instanced = commands.GetComposite(function.function);
            if (instanced == null) return false;
            if (known.TryGetValue(instanced, out bool holds)) return holds;
            holds = McpBrowseTools.ReachableFrom(commands, instanced).Any(c => c.functions_dictionary.Values.Any(f => f.function.IsFunctionType && _zonedTypes.Contains(f.function.AsFunctionType)));
            known[instanced] = holds;
            return holds;
        }

        /// <summary>
        /// New content placed where no zone claims it streams with no room around it, popping in or out: say so, naming the
        /// composite holding the level's zones. Levels with no zones at all are not checked.
        /// </summary>
        private static void NoteUnzoned(Commands commands, Scope scope)
        {
            Dictionary<Composite, bool> known = new Dictionary<Composite, bool>();
            List<(Composite Composite, Entity Entity)> content = scope.Placed.Where(o => o.Composite.GetEntityByID(o.Entity.shortGUID) == o.Entity && NeedsZone(commands, o.Entity, known)).Distinct().ToList();
            if (content.Count == 0)
                return;
            List<Composite> holders = commands.Entries.Where(c => c != null && c.functions_dictionary.Values.Any(f => f.function == FunctionType.Zone)).OrderByDescending(c => c.functions_dictionary.Values.Count(f => f.function == FunctionType.Zone)).ToList();
            if (holders.Count == 0)
                return;
            List<SyncedZone> zones = McpRegion.Zones(commands);
            if (zones.Count == 0)
                return;
            Composite root = commands.EntryPoints[0];
            List<string> unzoned = new List<string>(), partly = new List<string>();
            foreach (IGrouping<Composite, (Composite Composite, Entity Entity)> group in content.GroupBy(o => o.Composite))
            {
                List<List<uint>> chains = new List<List<uint>>();
                if (group.Key == root)
                    chains.Add(new List<uint>());
                else
                {
                    List<McpPlacements.Placement> found = new List<McpPlacements.Placement>();
                    McpRegion.Walker(commands).PlacementsOf(root, group.Key, null, found, 64, default(System.Threading.CancellationToken), realOnly: true);
                    chains.AddRange(found.Select(o => o.Chain.Select(e => e.shortGUID.AsUInt32).ToList()));
                }
                if (chains.Count == 0)
                    continue;
                foreach ((Composite composite, Entity entity) in group)
                {
                    int claimed = 0;
                    foreach (List<uint> chain in chains)
                    {
                        List<uint> path = chain.Concat(new[] { entity.shortGUID.AsUInt32 }).ToList();
                        if (zones.Any(z => z.roots.Any(r => r != null && r.Count != 0 && r.Count <= path.Count && !r.Where((id, i) => id != path[i]).Any())))
                            claimed++;
                    }
                    string name = McpScript.EntityName(commands, composite, entity) + (composite == root ? "" : " in " + composite.name);
                    if (claimed == 0) unzoned.Add(name);
                    else if (claimed < chains.Count) partly.Add(name + " (" + claimed + " of " + chains.Count + " placements)");
                }
            }
            string advice = "Content no zone claims is streamed with no room around it, so it can pop in or vanish: make it inside the composite holding the level's zones (" + holders[0].name + ") or a room composite a zone already lists, or add it to a zone with set_zone_contents.";
            if (unzoned.Count != 0)
                scope.Note("In no zone: " + string.Join(", ", unzoned.Take(8)) + (unzoned.Count > 8 ? " and " + (unzoned.Count - 8) + " more" : "") + ". " + advice);
            if (partly.Count != 0)
                scope.Note("In a zone in only some placements: " + string.Join(", ", partly.Take(8)) + ". " + advice);
        }
        #endregion

        #region Refactors
        private static object Deinstance(McpCall call)
        {
            bool dryRun = call.Bool("dry_run");
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: !dryRun);
                if (!dryRun) McpEditor.RequireUndoIdle();
                Composite parent = FindComposite(commands, call.Str("composite", required: true));

                //The refactor reads the parent's pages, which are live while it is on screen. Opening it first also runs
                //its first-open purge, which can remove entities - so the instance is looked up after. A dry run plans
                //against the composite as it stands, without taking the editor there.
                if (dryRun)
                    McpBrowseTools.CompileIfShown(parent);
                else
                {
                    CompositeDisplay display = Singleton.Editor.CompositeBrowser.LoadComposite(parent);
                    display?.SaveAllFlowgraphs();
                }
                Entity found = McpScript.FindEntity(commands, parent, call.Str("instance", required: true));
                if (!(found is FunctionEntity instance) || McpScript.InstancedComposite(commands, found) == null)
                    throw McpError.Invalid(McpScript.EntityName(commands, parent, found) + " is not a composite instance.");
                string name = McpScript.EntityName(commands, parent, instance);
                DeinstancePlan plan = DeinstancePlan.Plan(commands, parent, instance);
                List<string> notes = plan.Issues.Where(o => !o.Blocking).Select(o => o.Message).ToList();
                if (dryRun)
                {
                    return new JObject()
                    {
                        ["dry_run"] = true,
                        ["instance"] = name,
                        ["composite"] = plan.Content?.name,
                        ["can_apply"] = plan.CanApply,
                        ["entities_to_pull_in"] = plan.Content?.GetEntities().Count,
                        ["blocking"] = new JArray(plan.Issues.Where(o => o.Blocking).Select(o => o.Message)),
                        ["notes"] = new JArray(notes),
                    };
                }
                if (!plan.CanApply)
                    throw new McpError(McpErrorCodes.Refused, "Cannot de-instance " + name + ":\n- " + string.Join("\n- ", plan.Issues.Where(o => o.Blocking).Select(o => o.Message)));
                if (notes.Count != 0 && !call.Bool("allow_notes", true))
                    throw new McpError(McpErrorCodes.Refused, "De-instancing " + name + " would not carry everything over exactly (pass allow_notes: true to go ahead):\n- " + string.Join("\n- ", notes));

                RefactorEdit edit = CompositeRefactoring.DeinstanceEdit(plan, "AI: De-instance " + name);
                UndoStack.Current.Apply(edit);
                RefactorResult result = edit.Result;
                return new JObject()
                {
                    ["deinstanced"] = name,
                    ["pulled_in"] = result.PulledEntities.Count,
                    ["entities"] = new JArray(result.PulledEntities.Take(100).Select(o => McpScript.Brief(commands, parent, o))),
                    ["notes"] = new JArray(notes.Concat(result.Issues.Select(o => o.Message)).Distinct()),
                };
            });
        }

        private static object GroupIntoComposite(McpCall call)
        {
            bool dryRun = call.Bool("dry_run");
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: !dryRun);
                if (!dryRun) McpEditor.RequireUndoIdle();
                Composite parent = FindComposite(commands, call.Str("composite", required: true));

                //Opened first: its first-open purge can remove entities, so the selection is looked up after it. A dry run plans
                //against the composite as it stands, without taking the editor there
                if (dryRun)
                    McpBrowseTools.CompileIfShown(parent);
                else
                {
                    CompositeDisplay display = Singleton.Editor.CompositeBrowser.LoadComposite(parent);
                    display?.SaveAllFlowgraphs();
                }
                List<Entity> selection = call.StrList("entities", required: true).Select(o => McpScript.FindEntity(commands, parent, o)).Distinct().ToList();
                string name = call.Str("name") ?? DefaultGroupName(commands, parent);
                CreateCompositePlan plan = CreateCompositePlan.Plan(commands, parent, selection, McpScript.NormalisePath(name));
                if (dryRun)
                {
                    return new JObject()
                    {
                        ["dry_run"] = true,
                        ["name"] = plan.Name,
                        ["entities"] = plan.Selection.Count,
                        ["can_apply"] = plan.CanApply,
                        ["blocking"] = new JArray(plan.Issues.Where(o => o.Blocking).Select(o => o.Message)),
                        ["notes"] = new JArray(plan.Issues.Where(o => !o.Blocking).Select(o => o.Message)),
                    };
                }
                if (!plan.CanApply)
                    throw new McpError(McpErrorCodes.Refused, "Cannot group those entities:\n- " + string.Join("\n- ", plan.Issues.Where(o => o.Blocking).Select(o => o.Message)));

                RefactorEdit edit = CompositeRefactoring.CreateCompositeEdit(plan, "AI: Create composite " + McpScript.NormalisePath(name));
                UndoStack.Current.Apply(edit);
                RefactorResult result = edit.Result;
                return new JObject()
                {
                    ["composite"] = McpScript.CompositeSummary(commands, result.CreatedComposite),
                    ["instance"] = result.CreatedInstance == null ? null : McpScript.Brief(commands, parent, result.CreatedInstance),
                    ["notes"] = new JArray(plan.Issues.Concat(result.Issues).Where(o => !o.Blocking).Select(o => o.Message).Distinct()),
                };
            });
        }

        private static object DuplicateComposite(McpCall call)
        {
            bool dryRun = call.Bool("dry_run");
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: !dryRun);
                if (!dryRun) McpEditor.RequireUndoIdle();
                Composite source = FindComposite(commands, call.Str("composite", required: true));

                List<(Composite, FunctionEntity)> switching = new List<(Composite, FunctionEntity)>();
                foreach (JToken token in call.Array("use_for"))
                {
                    if (!(token is JObject spec))
                        throw McpError.Invalid("Each 'use_for' entry is an object: {\"composite\": <the composite it is in>, \"instance\": <the instance>}.");
                    string holderName = Field(spec, "composite") ?? throw McpError.Invalid("A 'use_for' entry needs 'composite': the composite the instance is in.");
                    string instanceName = Field(spec, "instance") ?? throw McpError.Invalid("A 'use_for' entry needs 'instance': the instance entity (id or name).");
                    Composite holder = FindComposite(commands, holderName);
                    Entity found = McpScript.FindEntity(commands, holder, instanceName);
                    if (!(found is FunctionEntity instance) || instance.function != source.shortGUID)
                        throw McpError.Invalid("'" + McpScript.EntityName(commands, holder, found) + "' in " + holder.name + " is not an instance of " + source.name + ".");
                    switching.Add((holder, instance));
                }

                string name = McpScript.NormalisePath(call.Str("path") ?? DuplicateCompositePlan.DefaultName(commands, source));
                if (!dryRun)
                    CompositeRefactoring.PrepareToDuplicate(commands, source);
                DuplicateCompositePlan plan = DuplicateCompositePlan.Plan(commands, source, name, switching);
                if (dryRun)
                {
                    return new JObject()
                    {
                        ["dry_run"] = true,
                        ["name"] = plan.Name,
                        ["entities"] = source.GetEntities().Count,
                        ["pages"] = FlowgraphLayoutManager.GetLayouts(source).Count,
                        ["use_for"] = switching.Count,
                        ["can_apply"] = plan.CanApply,
                        ["blocking"] = new JArray(plan.Issues.Where(o => o.Blocking).Select(o => o.Message)),
                        ["notes"] = new JArray(plan.Issues.Where(o => !o.Blocking).Select(o => o.Message)),
                    };
                }
                if (!plan.CanApply)
                    throw new McpError(McpErrorCodes.Refused, "Cannot duplicate " + source.name + ":\n- " + string.Join("\n- ", plan.Issues.Where(o => o.Blocking).Select(o => o.Message)));

                RefactorEdit edit = CompositeRefactoring.DuplicateEdit(plan, "AI: " + (switching.Count != 0 ? "Create variant of " : "Duplicate ") + McpScript.CompositeLeaf(source));
                UndoStack.Current.Apply(edit);
                Composite copy = edit.Result.CreatedComposite;
                JObject result = new JObject()
                {
                    ["composite"] = McpScript.CompositeSummary(commands, copy),
                    ["copied_from"] = source.name,
                    ["pages"] = new JArray(FlowgraphLayoutManager.GetLayouts(copy).Select(o => o.Name)),
                    ["script_view"] = FlowgraphLayoutManager.IsCompatible(copy) ? "pages" : "links",
                };
                if (switching.Count != 0)
                    result["switched"] = new JArray(switching.Select(o => new JObject() { ["composite"] = o.Item1.name, ["instance"] = McpScript.Brief(commands, o.Item1, o.Item2) }));
                JArray notes = new JArray(plan.Issues.Where(o => !o.Blocking).Select(o => o.Message).Distinct());
                if (notes.Count != 0)
                    result["notes"] = notes;
                return result;
            });
        }

        private static string DefaultGroupName(Commands commands, Composite parent)
        {
            string stem = McpScript.NormalisePath(parent.name);
            for (int i = 1; i < 1000; i++)
            {
                string candidate = stem + "_Group" + (i == 1 ? "" : "_" + i);
                if (CreateCompositePlan.CheckName(commands, candidate) == null) return candidate;
            }
            return "Composite_Group_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }
        #endregion
    }
}
