using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.Scripting.Refactor;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.DockPanels;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.MCP
{
    /// <summary>Changing the open level's script: composites, entities, parameters, links, and the two refactors.</summary>
    internal static class McpScriptTools
    {
        #region Schemas
        private static readonly JObject EntitySpec = new JObject()
        {
            ["type"] = "object",
            ["description"] = "One entity to make. Give exactly one of function, composite, variable, alias or proxy.",
            ["properties"] = new JObject()
            {
                ["ref"] = new JObject() { ["type"] = "string", ["description"] = "A handle for this entity, for links in the same call to refer to it." },
                ["function"] = new JObject() { ["type"] = "string", ["description"] = "A built-in type (FunctionType), e.g. 'ThinkOnce', 'Character', 'TriggerBox', 'ModelReference'." },
                ["composite"] = new JObject() { ["type"] = "string", ["description"] = "Place an instance of this composite (path or id)." },
                ["variable"] = new JObject()
                {
                    ["type"] = "object",
                    ["description"] = "A variable: a pin of this composite, which its instances expose as a parameter and link point.",
                    ["properties"] = new JObject()
                    {
                        ["name"] = new JObject() { ["type"] = "string" },
                        ["pin"] = new JObject() { ["type"] = "string", ["description"] = "method, target, reference, or input_/output_ with bool, int, float, string, position, direction, enum, enum_string, object, animation_info, zone, zone_link (e.g. 'input_float')." },
                        ["enum_type"] = new JObject() { ["type"] = "string", ["description"] = "For enum pins: the enum's name; for enum_string pins: the enum string type." },
                    },
                    ["required"] = new JArray("name", "pin"),
                },
                ["alias"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" }, ["description"] = "An alias (an override reaching into a nested instance): the path of entity ids/names from this composite, through instances, to the target." },
                ["proxy"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" }, ["description"] = "A proxy: the path of entity ids/names from the level's root composite, through instances, to a function entity." },
                ["name"] = new JObject() { ["type"] = "string", ["description"] = "Its name (default: the type, numbered)." },
                ["position"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["description"] = "[x, y, z] metres, relative to the composite." },
                ["rotation"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["description"] = "[x, y, z] degrees." },
                ["node"] = new JObject()
                {
                    ["type"] = "object",
                    ["description"] = "Its flowgraph node: {page?, x?, y?}. By default a linked entity is drawn beside what it joins and an unlinked one in a column beside the page's nodes.",
                    ["properties"] = new JObject() { ["page"] = new JObject() { ["type"] = "string" }, ["x"] = new JObject() { ["type"] = "number" }, ["y"] = new JObject() { ["type"] = "number" } },
                },
                ["parameters"] = new JObject() { ["type"] = "object", ["additionalProperties"] = true, ["description"] = "Parameter values by name." },
            },
        };

        private static readonly JObject LinkSpec = new JObject()
        {
            ["type"] = "object",
            ["description"] = "A link: from (source entity).param to (target entity).to_param. Events go from a relay/target pin (e.g. ThinkOnce 'on_think', a method's relay like 'triggered') to a method pin (e.g. 'trigger', 'spawn'); data goes from a parameter to a reference or variable pin.",
            ["properties"] = new JObject()
            {
                ["from"] = new JObject() { ["type"] = "string", ["description"] = "Source entity: a ref from this call, an id, or a name." },
                ["param"] = new JObject() { ["type"] = "string", ["description"] = "The source's pin." },
                ["to"] = new JObject() { ["type"] = "string", ["description"] = "Target entity: a ref, an id, or a name." },
                ["to_param"] = new JObject() { ["type"] = "string", ["description"] = "The target's pin." },
                ["allow_custom"] = new JObject() { ["type"] = "boolean", ["description"] = "Allow pins the entities do not normally have." },
            },
            ["required"] = new JArray("from", "param", "to", "to_param"),
        };

        private static readonly JObject EntryPath = new JObject()
        {
            ["description"] = "Entity ids/names through instances to the entity, as an array (or one string split on '/').",
            ["anyOf"] = new JArray(new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" } }, new JObject() { ["type"] = "string" }),
        };
        #endregion

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "create_entities",
                Title = "Create entities",
                Description = "Add entities to a composite - built-in functions, instances of composites, variables (pins), aliases, proxies - with names, positions and parameters, and optionally link them (to each other via 'ref', or to existing entities). One undo step; the flowgraph pages are updated to draw the new links. Returns the ids made.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "Where to add them (path or id; 'root' for the level's root).", required: true),
                    McpSchema.Array("entities", "What to make.", EntitySpec, required: true),
                    McpSchema.Array("links", "Links to make once they exist.", LinkSpec),
                    McpSchema.String("page", "The flowgraph page new links are drawn on when they cannot go beside what they join (default: the page last shown)."),
                    McpSchema.Boolean("allow_custom", "Allow parameters the entities do not normally have, and values the editor derives itself.")),
                Run = CreateEntities,
            };

            yield return new McpTool()
            {
                Name = "create_composite",
                Title = "Create composite",
                Description = "Make a new composite (a reusable script/prefab), optionally filling it (same entities/links format as create_entities) and placing an instance of it in another composite. Give pins with 'variable' entities so instances can be configured and linked. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("path", "Its path, with backslash folders, e.g. 'MCP\\Corridor_Lights'.", required: true),
                    McpSchema.Array("entities", "Entities to put in it.", EntitySpec),
                    McpSchema.Array("links", "Links between them.", LinkSpec),
                    McpSchema.String("place_in", "Also place an instance of it in this composite (e.g. 'root')."),
                    McpSchema.Vector("position", "Where to place that instance (needs place_in)."),
                    McpSchema.Vector("rotation", "Its rotation in degrees (needs place_in)."),
                    McpSchema.String("instance_name", "The instance's name (needs place_in).")),
                Run = CreateComposite,
            };

            yield return new McpTool()
            {
                Name = "set_parameters",
                Title = "Set parameters",
                Description = "Set parameter values on entities. Values are JSON: true/false, numbers, strings, [x,y,z] vectors, {\"position\":[x,y,z],\"rotation\":[x,y,z]} transforms (degrees), enum entry names (e.g. \"TELEPORT\"), a material mapping's name for 'mapping', enum-string values as list_enum_string_values lists them (e.g. a LEVEL_NAME for a SwitchLevel's level_name). Setting 'name' renames. One undo step for all of them.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id).", required: true),
                    McpSchema.String("entity", "One entity (id or name) - or use 'changes' for several."),
                    McpSchema.Map("parameters", "Parameter values by name, for 'entity'."),
                    McpSchema.Array("changes", "Several entities at once.", new JObject()
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject()
                        {
                            ["entity"] = new JObject() { ["type"] = "string" },
                            ["parameters"] = new JObject() { ["type"] = "object", ["additionalProperties"] = true },
                        },
                        ["required"] = new JArray("entity", "parameters"),
                    }),
                    McpSchema.Boolean("allow_custom", "Allow parameters the entity does not normally have, and values the editor derives itself (e.g. delete_me).")),
                Run = SetParameters,
            };

            yield return new McpTool()
            {
                Name = "remove_parameters",
                Title = "Remove parameters",
                Description = "Take parameters off an entity (it then uses the default; on an alias, the override is removed) - or, with reset, put them back to their default value as the inspector's Reset does (an alias's override is removed either way). One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id).", required: true),
                    McpSchema.String("entity", "The entity (id or name).", required: true),
                    McpSchema.Strings("parameters", "Parameter names.", required: true),
                    McpSchema.Boolean("reset", "Keep the parameters, at their default values, and clear their 'set by hand' mark.")),
                Destructive = true,
                Run = RemoveParameters,
            };

            yield return new McpTool()
            {
                Name = "rename_entity",
                Title = "Rename entity",
                Description = "Give an entity a new name. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id).", required: true),
                    McpSchema.String("entity", "The entity (id or name).", required: true),
                    McpSchema.String("name", "The new name.", required: true)),
                Run = call =>
                {
                    string name = call.Str("name", required: true);
                    Composite composite = null;
                    Entity entity = null;
                    McpScriptEdit.Run("Rename to " + name, Lookup(call, out composite, out entity), edit =>
                    {
                        entity = McpScript.FindEntity(edit.Commands, composite, call.Str("entity", required: true));
                        edit.Rename(composite, entity, name);
                    });
                    return new JObject() { ["renamed"] = McpScript.Id(entity.shortGUID), ["name"] = name };
                },
            };

            yield return new McpTool()
            {
                Name = "delete_entities",
                Title = "Delete entities",
                Description = "Delete entities from a composite, with their links (as the editor's Delete does). One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id).", required: true),
                    McpSchema.Strings("entities", "Entity ids or names.", required: true)),
                Destructive = true,
                Run = call =>
                {
                    List<string> wanted = call.StrList("entities", required: true);
                    Composite composite = McpEditor.UI(() => McpScript.FindComposite(McpEditor.RequireCommands(), call.Str("composite", required: true)));
                    JArray deleted = new JArray();
                    McpScriptEdit.Run("Delete " + wanted.Count + " entit" + (wanted.Count == 1 ? "y" : "ies"), composite, edit =>
                    {
                        List<Entity> entities = wanted.Select(o => McpScript.FindEntity(edit.Commands, composite, o)).Distinct().ToList();
                        foreach (Entity entity in entities)
                        {
                            string name = McpScript.EntityName(edit.Commands, composite, entity);
                            List<string> also = edit.Delete(composite, entity);
                            deleted.Add(name + " (" + McpScript.Id(entity.shortGUID) + ")");
                            foreach (string extra in also) deleted.Add(extra);
                        }
                    }, show: false);
                    return new JObject() { ["deleted"] = deleted };
                },
            };

            yield return new McpTool()
            {
                Name = "add_links",
                Title = "Add links",
                Description = "Link entities in a composite. Each link runs from a source entity's pin to a target entity's pin (describe_entity lists pins). One undo step; pages are updated to draw them.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id).", required: true),
                    McpSchema.Array("links", "The links.", LinkSpec, required: true),
                    McpSchema.String("page", "The flowgraph page for links that cannot go beside what they join."),
                    McpSchema.Boolean("allow_custom", "Allow pins the entities do not normally have, for every link.")),
                Run = call =>
                {
                    Composite composite = McpEditor.UI(() => McpScript.FindComposite(McpEditor.RequireCommands(), call.Str("composite", required: true)));
                    JArray made = new JArray();
                    McpScriptEdit.Outcome outcome = McpScriptEdit.Run("Add links", composite, edit =>
                    {
                        edit.UsePage(composite, call.Str("page"));
                        foreach (JToken link in call.Array("links", required: true))
                            made.Add(Link(edit, composite, link, null, call.Bool("allow_custom")));
                    }, show: false);
                    return new JObject() { ["links"] = made, ["script_view"] = outcome.Changed ? PagesNow(composite) : null };
                },
            };

            yield return new McpTool()
            {
                Name = "remove_links",
                Title = "Remove links",
                Description = "Remove links from a composite. Leave out 'to' / 'to_param' / 'param' to remove every link from the source that matches the rest. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id).", required: true),
                    McpSchema.Array("links", "The links to remove.", new JObject()
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject()
                        {
                            ["from"] = new JObject() { ["type"] = "string" },
                            ["param"] = new JObject() { ["type"] = "string" },
                            ["to"] = new JObject() { ["type"] = "string" },
                            ["to_param"] = new JObject() { ["type"] = "string" },
                        },
                        ["required"] = new JArray("from"),
                    }, required: true)),
                Destructive = true,
                Run = call =>
                {
                    Composite composite = McpEditor.UI(() => McpScript.FindComposite(McpEditor.RequireCommands(), call.Str("composite", required: true)));
                    int removed = 0;
                    McpScriptEdit.Run("Remove links", composite, edit =>
                    {
                        foreach (JToken token in call.Array("links", required: true))
                        {
                            if (!(token is JObject link))
                                throw new McpError("Each link to remove is an object: {\"from\", \"param\", \"to\", \"to_param\"} (only 'from' is required).");
                            Entity from = McpScript.FindEntity(edit.Commands, composite, Field(link, "from") ?? throw new McpError("Each link to remove needs 'from'."));
                            Entity to = Field(link, "to") == null ? null : McpScript.FindEntity(edit.Commands, composite, Field(link, "to"));
                            removed += edit.RemoveLinks(composite, from, Field(link, "param"), to, Field(link, "to_param"));
                        }
                    }, show: false);
                    return new JObject() { ["removed"] = removed };
                },
            };

            yield return new McpTool()
            {
                Name = "set_trigger_sequence",
                Title = "Set trigger sequence",
                Description = "Edit what a TriggerSequence fires, in order, each after a delay. 'entries' (paths through instances from its composite, or from the level root with from_root) replace the list, or join it with append / insert_at; 'remove' and 'update' change existing entries. 'methods' (e.g. 'trigger') become method pins with _relay/_finished pins. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the sequence is in.", required: true),
                    McpSchema.String("entity", "The TriggerSequence (or a proxy of one), by id or name.", required: true),
                    McpSchema.Array("entries", "Entities to trigger, in order.", new JObject()
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject()
                        {
                            ["path"] = EntryPath,
                            ["delay"] = new JObject() { ["type"] = "number", ["description"] = "Seconds to wait before triggering it." },
                            ["from_root"] = new JObject() { ["type"] = "boolean", ["description"] = "The path starts at the level's root composite, not the sequence's composite." },
                        },
                        ["required"] = new JArray("path"),
                    }),
                    McpSchema.Integer("insert_at", "Put the new entries at this position (0 = first), keeping the rest."),
                    McpSchema.Boolean("append", "Add the new entries (and methods) at the end instead of replacing the lists."),
                    McpSchema.Boolean("dedupe", "Skip new entries naming an entity already in the sequence (default true)."),
                    McpSchema.Array("remove", "Entries to take out: an index, or a path (every entry naming that entity goes); {\"path\":..., \"from_root\": true} for a path from the root.", new JObject()),
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
                    McpSchema.Strings("methods", "The method names it supports (replaces them unless append).")),
                Run = SetTriggerSequence,
            };

            yield return new McpTool()
            {
                Name = "deinstance",
                Title = "De-instance",
                Description = "Pull a composite instance apart: its composite's entities are copied into the composite it is placed in (positions, parameters, aliases and links carried over, script pages imported) and the instance goes. Other instances are unaffected. dry_run reports what it would carry over inexactly, or why it cannot, without changing anything. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the instance is in.", required: true),
                    McpSchema.String("instance", "The instance entity (id or name).", required: true),
                    McpSchema.Boolean("allow_notes", "Go ahead even if it reports things it cannot carry over exactly (default true)."),
                    McpSchema.Boolean("dry_run", "Only report the plan's blocking problems and notes.")),
                Run = Deinstance,
            };

            yield return new McpTool()
            {
                Name = "group_into_composite",
                Title = "Group into new composite",
                Description = "Create Composite from entities: moves them (with their parameters and script pages) into a new composite and places an instance of it where they were; links that crossed the boundary are kept through new pins and aliases. dry_run reports the plan's problems and notes without changing anything. One undo step.",
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
                Description = "Copy a composite under a new path: the same entities with the same ids, links, parameters, pins and flowgraph pages, its models, collision and physics shared with the original as two placements share them. Because the ids match, an instance switched to the copy keeps every alias, proxy, trigger sequence and animation that reached into it - so use_for (instances of the original to switch to the copy) gives one placement a version of its own to change, like the editor's Make Unique. dry_run reports the name it would get and any problems without changing anything. One undo step.",
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
            throw new McpError("'" + name + "' must be text, not " + token.ToString(Newtonsoft.Json.Formatting.None) + ".");
        }

        /// <summary>A path of entity ids/names: an array, or one string split on '/', '\' or '>'.</summary>
        internal static List<string> ReadPath(JToken token)
        {
            if (token is JArray array && array.Count != 0)
            {
                if (array.Any(o => o.Type != JTokenType.String && o.Type != JTokenType.Integer))
                    throw new McpError("A path is a list of entity ids or names (text), e.g. [\"Door_1\", \"Keypad\"].");
                return array.Select(o => McpValues.ReadString(o).Trim()).ToList();
            }
            if (token?.Type == JTokenType.String && ((string)token).Trim().Length != 0)
                return ((string)token).Split(new[] { '/', '\\', '>' }, StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim()).ToList();
            throw new McpError("A path is a list of entity ids or names, e.g. [\"Door_1\", \"Keypad\"].");
        }
        #endregion

        #region Creating
        private static object CreateEntities(McpCall call)
        {
            JArray specs = call.Array("entities", required: true);
            if (specs.Count == 0)
                throw new McpError("'entities' is empty.");
            Composite composite = McpEditor.UI(() => McpScript.FindComposite(McpEditor.RequireCommands(), call.Str("composite", required: true)));
            JArray made = new JArray();
            JArray links = new JArray();
            List<string> notes = new List<string>();
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run(specs.Count == 1 ? "Add " + Label(specs[0]) : "Add " + specs.Count + " entities", composite, edit =>
            {
                edit.UsePage(composite, call.Str("page"));
                Dictionary<string, Entity> refs = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
                foreach (JToken spec in specs)
                    made.Add(Make(edit, composite, spec, refs, call.Bool("allow_custom"), notes));
                foreach (JToken link in call.Array("links"))
                    links.Add(Link(edit, composite, link, refs, call.Bool("allow_custom")));
            });
            foreach (string note in notes) call.Note(note);
            JObject result = new JObject() { ["composite"] = composite.name, ["created"] = made };
            if (links.Count != 0) result["links"] = links;
            result["script_view"] = PagesNow(composite);
            return result;
        }

        private static object CreateComposite(McpCall call)
        {
            string path = call.Str("path", required: true);
            JArray specs = call.Array("entities");
            string placeIn = call.Str("place_in");
            if (placeIn == null && (call.Has("position") || call.Has("rotation") || call.Has("instance_name")))
                throw new McpError("'position', 'rotation' and 'instance_name' are for placing an instance of the new composite: give 'place_in' too (e.g. 'root').");
            Composite host = McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                return placeIn != null ? McpScript.FindComposite(commands, placeIn) : (Singleton.Editor.CompositeDisplay?.Composite ?? commands.EntryPoints[0]);
            });

            Composite created = null;
            JArray made = new JArray();
            JArray links = new JArray();
            JObject instance = null;
            McpScriptEdit.Run("Create composite " + McpScript.NormalisePath(path), host, edit =>
            {
                created = edit.AddComposite(path);
                Dictionary<string, Entity> refs = new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase);
                foreach (JToken spec in specs)
                    made.Add(Make(edit, created, spec, refs, false));
                foreach (JToken link in call.Array("links"))
                    links.Add(Link(edit, created, link, refs, false));
                if (placeIn != null)
                {
                    FunctionEntity placed = edit.AddInstance(host, created, call.Str("instance_name"));
                    Place(edit, host, placed, call.Token("position"), call.Token("rotation"));
                    edit.Select.Add(placed);
                    instance = new JObject() { ["composite"] = host.name, ["id"] = McpScript.Id(placed.shortGUID), ["name"] = McpScript.EntityName(edit.Commands, host, placed) };
                }
            }, show: placeIn != null);

            JObject result = McpEditor.UI(() => McpScript.CompositeSummary(McpEditor.RequireCommands(forEditing: false), created));
            result["created"] = made;
            if (links.Count != 0) result["links"] = links;
            if (instance != null) result["instance"] = instance;
            return result;
        }

        /// <summary>What to call a one-entity step: its name, else its type (never empty).</summary>
        private static string Label(JToken token)
        {
            if (!(token is JObject spec))
                return "entity";
            string Text(string field) => spec[field]?.Type == JTokenType.String && ((string)spec[field]).Trim().Length != 0 ? ((string)spec[field]).Trim() : null;
            string composite = Text("composite") == null ? null : McpScript.NormalisePath(Text("composite")).Split('\\').LastOrDefault();
            return Text("name") ?? Text("function") ?? (string.IsNullOrEmpty(composite) ? null : composite)
                ?? (spec["variable"] != null ? "variable" : spec["alias"] != null ? "alias" : spec["proxy"] != null ? "proxy" : "entity");
        }

        /// <summary>Make one entity from its spec, in <paramref name="composite"/>.</summary>
        private static JObject Make(McpScriptEdit edit, Composite composite, JToken token, Dictionary<string, Entity> refs, bool allowCustom, List<string> notes = null)
        {
            if (!(token is JObject spec))
                throw new McpError("Each entity is an object, e.g. {\"function\": \"ThinkOnce\"}.");
            Commands commands = edit.Commands;
            string name = Field(spec, "name");
            string[] kinds = new[] { "function", "composite", "variable", "alias", "proxy" }.Where(o => spec[o] != null).ToArray();
            if (kinds.Length != 1)
                throw new McpError("Give each entity exactly one of: function, composite, variable, alias, proxy" + (kinds.Length > 1 ? " (this one has " + string.Join(" and ", kinds) + ")." : "."));

            Entity entity;
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
                            throw new McpError("There is no function type '" + typeName + "'." + (named != null ? " There is a composite '" + named.name + "': use \"composite\" to place an instance of it." : " list_function_types lists them."));
                        }
                        entity = edit.AddFunction(composite, type, name);
                        break;
                    }
                case "composite":
                    entity = edit.AddInstance(composite, McpScript.FindComposite(commands, Field(spec, "composite")), name);
                    break;
                case "variable":
                    {
                        JObject variable = spec["variable"] as JObject ?? throw new McpError("'variable' is an object: {\"name\": ..., \"pin\": ...}.");
                        entity = edit.AddVariable(composite, Field(variable, "name") ?? name, ParsePinType(Field(variable, "pin")), Field(variable, "enum_type"));
                        break;
                    }
                case "alias":
                    {
                        List<string> steps = ReadPath(spec["alias"]);
                        ShortGuid[] path = McpScript.ResolvePath(commands, composite, steps, out Composite _, out Entity target);
                        if (target is AliasEntity)
                            throw new McpError("An alias cannot point at another alias; point it at what that alias points at.");
                        entity = edit.AddAlias(composite, path, name);
                        break;
                    }
                case "proxy":
                    {
                        List<string> steps = ReadPath(spec["proxy"]);
                        ShortGuid[] path = McpScript.ResolvePath(commands, commands.EntryPoints[0], steps, out Composite _, out Entity _);
                        entity = edit.AddProxy(composite, path, name);
                        break;
                    }
                default:
                    throw new McpError("Unknown entity kind.");
            }

            Place(edit, composite, entity, spec["position"], spec["rotation"]);
            if (spec["node"] != null && spec["node"].Type != JTokenType.Null)
            {
                if (!(spec["node"] is JObject node))
                    throw new McpError("'node' is an object: {\"page\": ..., \"x\": ..., \"y\": ...}.");
                bool hasX = node["x"] != null && node["x"].Type != JTokenType.Null, hasY = node["y"] != null && node["y"].Type != JTokenType.Null;
                if (hasX != hasY)
                    throw new McpError("'node' needs both x and y, or neither.");
                System.Drawing.Point? at = hasX ? new System.Drawing.Point((int)Math.Round(McpValues.ReadDouble(node["x"], "node.x")), (int)Math.Round(McpValues.ReadDouble(node["y"], "node.y"))) : (System.Drawing.Point?)null;
                edit.PlaceNode(entity, Field(node, "page"), at);
            }
            if (spec["parameters"] != null && !(spec["parameters"] is JObject))
                throw new McpError("'parameters' is an object of values by name, e.g. {\"radius\": 2}.");
            if (spec["parameters"] is JObject parameters)
                foreach (JProperty parameter in parameters.Properties())
                    WriteParameter(edit, composite, entity, parameter.Name, parameter.Value, allowCustom, notes);

            string reference = Field(spec, "ref");
            if (!string.IsNullOrWhiteSpace(reference))
            {
                if (refs.ContainsKey(reference))
                    throw new McpError("Two entities have the ref '" + reference + "'.");
                refs[reference] = entity;
            }

            JObject made = McpScript.Brief(commands, composite, entity);
            if (reference != null) made["ref"] = reference;
            return made;
        }

        private static void Place(McpScriptEdit edit, Composite composite, Entity entity, JToken position, JToken rotation)
        {
            if (position == null && rotation == null)
                return;
            if (entity is VariableEntity)
                throw new McpError("A variable has no position.");
            JObject transform = new JObject();
            if (position != null) transform["position"] = position;
            if (rotation != null) transform["rotation"] = rotation;
            edit.SetParameter(composite, entity, "position", transform, allowCustom: true);
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
            throw new McpError("'" + text + "' is not a pin type. Use one of: " + string.Join(", ", _pinTypes.Keys.Where(o => !o.EndsWith("_integer"))) + ".");
        }
        #endregion

        #region Parameters and links
        private static Composite Lookup(McpCall call, out Composite composite, out Entity entity)
        {
            Composite c = null;
            Entity e = null;
            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                c = McpScript.FindComposite(commands, call.Str("composite", required: true));
                e = McpScript.FindEntity(commands, c, call.Str("entity", required: true));
            });
            composite = c;
            entity = e;
            return c;
        }

        /// <summary>
        /// Set a parameter as the inspector lets a user: a value the save or build works out itself (see
        /// EntityParameterVisibility) is refused unless allowed.
        /// </summary>
        internal static ParameterData WriteParameter(McpScriptEdit edit, Composite composite, Entity entity, string name, JToken value, bool allowCustom, List<string> notes = null)
        {
            ShortGuid id = McpScript.ParamId(name);
            if (!allowCustom && EntityParameterVisibility.IsHiddenFromEditor(entity, id))
                throw new McpError("'" + McpScript.ParamName(id) + "' on " + McpScript.TypeName(edit.Commands, composite, entity) + " is worked out by OpenCAGE when the level is saved or built, so the editor does not offer it. Pass allow_custom: true to write it anyway.");
            //An EnvironmentMap's Texture is a path into the build's textures: a texture's name (as list_textures gives it) is accepted too
            if (entity is FunctionEntity host && host.function == FunctionType.EnvironmentMap && string.Equals(McpScript.ParamName(id), "Texture", StringComparison.OrdinalIgnoreCase)
                && value != null && value.Type == JTokenType.String && ((string)value).Trim().Length != 0
                && ((string)value).Replace('/', '\\').IndexOf("content\\build\\textures\\", StringComparison.OrdinalIgnoreCase) < 0)
            {
                Textures.TEX4 texture = McpAssets.FindTexture(McpEditor.RequireLevel(forEditing: false).Level, (string)value);
                value = McpAssets.EnvironmentMapPath(texture);
            }
            ParameterData written = edit.SetParameter(composite, entity, name, value, allowCustom);
            if (notes != null && written is cEnumString enumString && !string.IsNullOrWhiteSpace(enumString.value))
            {
                string problem = UnknownEnumString(enumString);
                if (problem != null) notes.Add(McpScript.EntityName(edit.Commands, composite, entity) + "." + McpScript.ParamName(id) + ": " + problem);
            }
            return written;
        }

        /// <summary>Why an enum-string value is not one the inspector's picker offers, or null (also when there is no list to check against).</summary>
        private static string UnknownEnumString(cEnumString value)
        {
            EnumStringType type = (EnumStringType)value.enumID.AsUInt32;
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
            if (items == null || items.Item1.Length == 0 || items.Item1.Any(o => string.Equals(o.Text, value.value, StringComparison.OrdinalIgnoreCase)))
                return null;
            return "'" + value.value + "' is not a " + type + " value OpenCAGE knows (list_enum_string_values " + type + " lists them); it was written as given.";
        }

        private static object SetParameters(McpCall call)
        {
            Composite composite = McpEditor.UI(() => McpScript.FindComposite(McpEditor.RequireCommands(), call.Str("composite", required: true)));
            List<(string entity, JObject parameters)> changes = new List<(string, JObject)>();
            if (call.Has("entity"))
                changes.Add((call.Str("entity"), call.Object("parameters", required: true)));
            else if (call.Has("parameters"))
                throw new McpError("'parameters' goes with 'entity' (or use 'changes' for several entities).");
            foreach (JToken token in call.Array("changes"))
            {
                if (!(token is JObject change))
                    throw new McpError("Each change is an object: {\"entity\": ..., \"parameters\": {...}}.");
                string entity = Field(change, "entity") ?? throw new McpError("Each change needs 'entity'.");
                changes.Add((entity, change["parameters"] as JObject ?? throw new McpError("Each change needs 'parameters' (an object of values by name).")));
            }
            if (changes.Count == 0)
                throw new McpError("Give 'entity' and 'parameters', or 'changes'.");

            JArray results = new JArray();
            List<string> notes = new List<string>();
            McpScriptEdit.Run(changes.Count == 1 ? "Set parameters on " + changes[0].entity : "Set parameters on " + changes.Count + " entities", composite, edit =>
            {
                foreach ((string reference, JObject parameters) in changes)
                {
                    Entity entity = McpScript.FindEntity(edit.Commands, composite, reference);
                    JObject written = new JObject();
                    foreach (JProperty parameter in parameters.Properties())
                    {
                        ParameterData value = WriteParameter(edit, composite, entity, parameter.Name, parameter.Value, call.Bool("allow_custom"), notes);
                        written[McpScript.ParamName(McpScript.ParamId(parameter.Name))] = McpValues.ToJson(value, edit.Commands);
                    }
                    edit.Select.Add(entity);
                    results.Add(new JObject() { ["entity"] = McpScript.Id(entity.shortGUID), ["name"] = McpScript.EntityName(edit.Commands, composite, entity), ["set"] = written });
                }
            });
            foreach (string note in notes) call.Note(note);
            return new JObject() { ["changed"] = results };
        }

        private static object RemoveParameters(McpCall call)
        {
            List<string> names = call.StrList("parameters", required: true);
            bool reset = call.Bool("reset");
            List<string> removed = new List<string>();
            List<string> resetTo = new List<string>();
            List<string> noDefault = new List<string>();
            Composite composite = null;
            Entity entity = null;
            McpScriptEdit.Run((reset ? "Reset" : "Remove") + " parameters", Lookup(call, out composite, out entity), edit =>
            {
                entity = McpScript.FindEntity(edit.Commands, composite, call.Str("entity", required: true));
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
                    ParameterData fallback = edit.Commands.Utils.CreateDefaultParameterData(entity, composite, id);
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
            });
            JObject result = new JObject() { ["not_present"] = new JArray(names.Where(o => !removed.Contains(o) && !resetTo.Contains(o) && !noDefault.Contains(o))) };
            if (!reset || removed.Count != 0) result["removed"] = new JArray(removed);
            if (reset)
            {
                result["reset"] = new JArray(resetTo);
                if (noDefault.Count != 0) result["no_default"] = new JArray(noDefault.Select(o => o + " (not a parameter of its type, so it has no default: remove it instead)"));
                if (resetTo.Count != 0)
                    result["values"] = McpEditor.UI(() =>
                    {
                        Commands commands = McpEditor.RequireCommands(forEditing: false);
                        JObject values = new JObject();
                        foreach (string name in resetTo)
                            values[McpScript.ParamName(McpScript.ParamId(name))] = McpValues.ToJson(entity.GetParameter(McpScript.ParamId(name))?.content, commands);
                        return values;
                    });
            }
            return result;
        }

        /// <summary>Make one link from its spec; refs name entities made in the same call.</summary>
        private static JObject Link(McpScriptEdit edit, Composite composite, JToken token, Dictionary<string, Entity> refs, bool allowCustom)
        {
            if (!(token is JObject spec))
                throw new McpError("Each link is an object: {\"from\", \"param\", \"to\", \"to_param\"}.");
            Entity Resolve(string field)
            {
                string reference = Field(spec, field) ?? throw new McpError("A link needs '" + field + "'.");
                if (refs != null && refs.TryGetValue(reference, out Entity made)) return made;
                return McpScript.FindEntity(edit.Commands, composite, reference);
            }
            Entity from = Resolve("from");
            Entity to = Resolve("to");
            string param = Field(spec, "param") ?? throw new McpError("A link needs 'param' (the source's pin).");
            string toParam = Field(spec, "to_param") ?? throw new McpError("A link needs 'to_param' (the target's pin).");
            bool custom = allowCustom || (spec["allow_custom"] != null && McpValues.ReadBool(spec["allow_custom"], "allow_custom"));
            bool added = edit.AddLink(composite, from, param, to, toParam, custom);
            return new JObject()
            {
                ["link"] = McpScript.EntityName(edit.Commands, composite, from) + "." + param + " -> " + McpScript.EntityName(edit.Commands, composite, to) + "." + toParam,
                ["added"] = added,
            };
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
            throw new McpError(McpScript.EntityName(commands, composite, entity) + " is a " + McpScript.TypeName(commands, composite, entity) + ", not a TriggerSequence.");
        }

        /// <summary>A sequence entry's stored path: from the sequence's composite, or (from_root) from the level root, as the editor's picker writes either.</summary>
        internal static ShortGuid[] SequencePath(Commands commands, Composite composite, JToken token, bool fromRoot)
        {
            List<string> steps = ReadPath(token);
            ShortGuid[] path = McpScript.ResolvePath(commands, fromRoot ? commands.EntryPoints[0] : composite, steps, out Composite _, out Entity target);
            if (!(target is FunctionEntity))
                throw new McpError("A trigger sequence entry has to be a function entity or composite instance ('" + string.Join("/", steps) + "' is a " + McpScript.Kind(target) + ").");
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
                throw new McpError(what + " " + index + " is not an entry: the sequence has " + count + (count == 0 ? "." : " (0 to " + (count - 1) + ")."));
            return index;
        }

        private static object SetTriggerSequence(McpCall call)
        {
            bool hasEntries = call.Has("entries");
            bool append = call.Bool("append");
            bool insert = call.Has("insert_at");
            bool dedupe = call.Bool("dedupe", true);
            JArray removals = call.Array("remove");
            JArray updates = call.Array("update");
            if (append && insert)
                throw new McpError("Give append or insert_at, not both.");
            if (hasEntries && !append && !insert && (removals.Count != 0 || updates.Count != 0))
                throw new McpError("'entries' without append or insert_at replaces the whole list, so 'remove' and 'update' would have nothing to change. Pass append: true or insert_at to keep the existing entries.");
            if (insert && !hasEntries)
                throw new McpError("'insert_at' places new 'entries': give them too.");

            Composite composite = null;
            Entity entity = null;
            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                entity = McpScript.FindEntity(commands, composite, call.Str("entity", required: true));
                SequenceOf(commands, composite, entity);
                //An editor open on the sequence holds the lists this replaces: closed first, it records its own changes as a step
                foreach (TriggerSequenceEditor editor in Application.OpenForms.OfType<TriggerSequenceEditor>().ToList())
                    if (editor.Entity == entity) editor.Close();
            });

            JArray skipped = new JArray();
            int removedCount = 0;
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run("Set trigger sequence", composite, edit =>
            {
                Commands commands = edit.Commands;
                entity = McpScript.FindEntity(commands, composite, call.Str("entity", required: true));
                List<TriggerSequence.SequenceEntry> current = SequenceOf(commands, composite, entity);
                List<Row> rows = current.Select(o => new Row() { Path = (ShortGuid[])(o.connectedEntity?.path ?? new ShortGuid[0]).Clone(), Delay = o.timing }).ToList();

                //Updates and removals name entries by where they were before this call
                List<(Row row, int to)> moves = new List<(Row, int)>();
                foreach (JToken token in updates)
                {
                    if (!(token is JObject update))
                        throw new McpError("Each update is an object, e.g. {\"index\": 0, \"delay\": 2}.");
                    if (update["index"] == null)
                        throw new McpError("Each update needs 'index'.");
                    Row row = rows[ReadIndex(update["index"], "update index", rows.Count)];
                    if (update["delay"] != null) row.Delay = (float)McpValues.ReadDouble(update["delay"], "delay");
                    if (update["path"] != null) row.Path = SequencePath(commands, composite, update["path"], update["from_root"] != null && McpValues.ReadBool(update["from_root"], "from_root"));
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
                        throw new McpError("Each 'remove' item is an index, a path, or {\"path\": ..., \"from_root\": true}.");
                    bool fromRoot = spec?["from_root"] != null && McpValues.ReadBool(spec["from_root"], "from_root");
                    List<Entity> chain = ChainOf(commands, composite, SequencePath(commands, composite, pathToken, fromRoot));
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
                    foreach (JToken token in call.Array("entries"))
                    {
                        JObject spec = token as JObject;
                        JToken pathToken = spec != null ? spec["path"] : token;
                        if (pathToken == null)
                            throw new McpError("Each entry needs 'path'.");
                        bool fromRoot = spec?["from_root"] != null && McpValues.ReadBool(spec["from_root"], "from_root");
                        Row row = new Row()
                        {
                            Path = SequencePath(commands, composite, pathToken, fromRoot),
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
                        int at = call.Int("insert_at");
                        if (at < 0 || at > list.Count)
                            throw new McpError("insert_at is 0 to " + list.Count + " (the entries there are before this call's additions).");
                        list.InsertRange(at, added);
                    }
                    else
                        list = added;
                }

                bool changed = list.Count != current.Count
                    || list.Where((o, i) => o.Delay != current[i].timing || !o.Path.SequenceEqual(current[i].connectedEntity?.path ?? new ShortGuid[0])).Any();
                if (changed)
                    edit.SetTriggerSequence(composite, entity, list.Select(o => (o.Path, o.Delay)).ToList(), null, append: false);
                if (call.Has("methods"))
                    edit.SetTriggerSequence(composite, entity, null, call.StrList("methods"), append);
                edit.Select.Add(entity);
            });

            JObject result = McpEditor.UI(() => McpScript.Describe(McpEditor.RequireCommands(forEditing: false), composite, entity, parameters: false));
            if (!outcome.Changed) result["unchanged"] = "The sequence already had exactly that.";
            if (removedCount != 0) result["removed"] = removedCount;
            if (skipped.Count != 0) result["skipped"] = skipped;
            return result;
        }
        #endregion

        #region Refactors
        private static object Deinstance(McpCall call)
        {
            bool dryRun = call.Bool("dry_run");
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                if (!dryRun) McpEditor.RequireUndoIdle();
                Composite parent = McpScript.FindComposite(commands, call.Str("composite", required: true));

                //The refactor reads the parent's pages, which are live while it is on screen. Opening it first also runs
                //its first-open purge, which can remove entities - so the instance is looked up after.
                CompositeDisplay display = Singleton.Editor.CompositeBrowser.LoadComposite(parent);
                display?.SaveAllFlowgraphs();
                Entity found = McpScript.FindEntity(commands, parent, call.Str("instance", required: true));
                if (!(found is FunctionEntity instance) || McpScript.InstancedComposite(commands, found) == null)
                    throw new McpError(McpScript.EntityName(commands, parent, found) + " is not a composite instance.");
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
                    throw new McpError("Cannot de-instance " + name + ":\n- " + string.Join("\n- ", plan.Issues.Where(o => o.Blocking).Select(o => o.Message)));
                if (notes.Count != 0 && !call.Bool("allow_notes", true))
                    throw new McpError("De-instancing " + name + " would not carry everything over exactly (pass allow_notes: true to go ahead):\n- " + string.Join("\n- ", notes));

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
                Commands commands = McpEditor.RequireCommands();
                if (!dryRun) McpEditor.RequireUndoIdle();
                Composite parent = McpScript.FindComposite(commands, call.Str("composite", required: true));

                //Opened first: its first-open purge can remove entities, so the selection is looked up after it
                CompositeDisplay display = Singleton.Editor.CompositeBrowser.LoadComposite(parent);
                display?.SaveAllFlowgraphs();
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
                    throw new McpError("Cannot group those entities:\n- " + string.Join("\n- ", plan.Issues.Where(o => o.Blocking).Select(o => o.Message)));

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
                Commands commands = McpEditor.RequireCommands();
                if (!dryRun) McpEditor.RequireUndoIdle();
                Composite source = McpScript.FindComposite(commands, call.Str("composite", required: true));

                List<(Composite, FunctionEntity)> switching = new List<(Composite, FunctionEntity)>();
                foreach (JToken token in call.Array("use_for"))
                {
                    if (!(token is JObject spec))
                        throw new McpError("Each 'use_for' entry is an object: {\"composite\": <the composite it is in>, \"instance\": <the instance>}.");
                    string holderName = Field(spec, "composite") ?? throw new McpError("A 'use_for' entry needs 'composite': the composite the instance is in.");
                    string instanceName = Field(spec, "instance") ?? throw new McpError("A 'use_for' entry needs 'instance': the instance entity (id or name).");
                    Composite holder = McpScript.FindComposite(commands, holderName);
                    Entity found = McpScript.FindEntity(commands, holder, instanceName);
                    if (!(found is FunctionEntity instance) || instance.function != source.shortGUID)
                        throw new McpError("'" + McpScript.EntityName(commands, holder, found) + "' in " + holder.name + " is not an instance of " + source.name + ".");
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
                    throw new McpError("Cannot duplicate " + source.name + ":\n- " + string.Join("\n- ", plan.Issues.Where(o => o.Blocking).Select(o => o.Message)));

                RefactorEdit edit = CompositeRefactoring.DuplicateEdit(plan, "AI: " + (switching.Count != 0 ? "Make unique " : "Duplicate ") + McpScript.CompositeLeaf(source));
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
