using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OpenCAGE.MCP
{
    /// <summary>One tool an MCP client can call: its name, what it does, the arguments it takes, and the code that runs it.</summary>
    internal sealed class McpTool
    {
        public string Name;
        public string Title;
        public string Description;
        public JObject InputSchema;

        /// <summary>Changes nothing (a client may call it without asking).</summary>
        public bool ReadOnly;
        /// <summary>Can remove or overwrite work (a client should confirm first).</summary>
        public bool Destructive;
        /// <summary>Calling it twice is the same as calling it once.</summary>
        public bool Idempotent;

        /// <summary>Runs on a background thread; returns anything Newtonsoft can serialise.</summary>
        public Func<McpCall, object> Run;

        //Past this a result is cut short: a client pays for every character of it
        private const int MaxResultCharacters = 150000;

        public JObject Describe()
        {
            return new JObject()
            {
                ["name"] = Name,
                ["title"] = Title,
                ["description"] = Description,
                ["inputSchema"] = InputSchema ?? McpSchema.Object(),
                ["annotations"] = new JObject()
                {
                    ["title"] = Title,
                    ["readOnlyHint"] = ReadOnly,
                    ["destructiveHint"] = Destructive,
                    ["idempotentHint"] = Idempotent,
                    ["openWorldHint"] = false,
                },
            };
        }

        public JObject Invoke(McpCall call)
        {
            //A misspelt argument would otherwise be ignored without a word, and the tool do something other than was asked
            JObject properties = (InputSchema ?? McpSchema.Object())["properties"] as JObject ?? new JObject();
            List<string> unknown = call.Args.Properties().Select(o => o.Name).Where(o => properties[o] == null).ToList();
            if (unknown.Count != 0)
                return ErrorResult("Unknown argument" + (unknown.Count > 1 ? "s " : " ") + string.Join(", ", unknown.Select(o => "'" + o + "'")) + ". " +
                    (properties.Count == 0 ? Name + " takes none." : Name + " takes: " + string.Join(", ", properties.Properties().Select(o => o.Name)) + "."));

            object result;
            try
            {
                McpEditor.BeginCall();
                using (McpDialogs.Watch(call))
                    result = Run(call);
            }
            catch (McpError e)
            {
                return ErrorResult(e.Message, call);
            }
            catch (OperationCanceledException)
            {
                return ErrorResult("Cancelled.", call);
            }
            catch (Exception e)
            {
                Exception inner = e;
                while ((inner is System.Reflection.TargetInvocationException || inner is McpUiException) && inner.InnerException != null)
                    inner = inner.InnerException;
                Debug.Log("MCP", Name + " failed: " + inner);
                return ErrorResult(Name + " failed: " + inner.GetType().Name + ": " + inner.Message, call);
            }

            if (result is McpImage image)
            {
                string caption = image.Caption ?? "";
                if (call.Notes.Count != 0) caption += "\nNotes: " + string.Join(" ", call.Notes);
                if (call.Dialogs.Count != 0) caption += "\nOpenCAGE showed (and these were dismissed): " + string.Join(" | ", call.Dialogs);
                return new JObject()
                {
                    ["content"] = new JArray(
                        new JObject() { ["type"] = "image", ["data"] = Convert.ToBase64String(image.Data), ["mimeType"] = image.MimeType },
                        new JObject() { ["type"] = "text", ["text"] = caption }),
                    ["isError"] = false,
                };
            }

            JToken body = result as JToken ?? (result == null ? new JObject() : JToken.FromObject(result, McpJson.Serializer));
            if (body is JObject obj)
            {
                if (call.Notes.Count != 0) obj["notes"] = new JArray(call.Notes);
                if (call.Dialogs.Count != 0) obj["dialogs_dismissed"] = new JArray(call.Dialogs);
            }
            string text = body.Type == JTokenType.String ? (string)body : body.ToString(Formatting.Indented);
            if (text.Length > MaxResultCharacters)
                text = text.Substring(0, MaxResultCharacters) + "\n... (cut short at " + MaxResultCharacters + " characters: ask for less, e.g. with a filter or a limit)";
            return new JObject()
            {
                ["content"] = new JArray(new JObject() { ["type"] = "text", ["text"] = text }),
                ["isError"] = false,
            };
        }

        public static JObject ErrorResult(string message, McpCall call = null)
        {
            string text = message;
            if (call != null && call.Dialogs.Count != 0)
                text += "\n\nOpenCAGE showed these messages, which were dismissed:\n- " + string.Join("\n- ", call.Dialogs);
            return new JObject()
            {
                ["content"] = new JArray(new JObject() { ["type"] = "text", ["text"] = text }),
                ["isError"] = true,
            };
        }
    }

    /// <summary>A tool that could not do what it was asked, for a reason the caller can act on.</summary>
    internal sealed class McpError : Exception
    {
        public McpError(string message) : base(message) { }
    }

    /// <summary>One call of a tool: its arguments, and what it reports alongside its result.</summary>
    internal sealed class McpCall
    {
        public McpTool Tool { get; }
        public JObject Args { get; }
        public CancellationToken Cancel { get; }

        /// <summary>Things the caller should know that are not the result itself.</summary>
        public List<string> Notes { get; } = new List<string>();

        /// <summary>Message boxes OpenCAGE showed while the tool ran (they are dismissed, and reported).</summary>
        public List<string> Dialogs { get; } = new List<string>();

        private readonly Action<string, double?, double?> _progress;

        public McpCall(McpTool tool, JObject args, CancellationToken cancel, Action<string, double?, double?> progress)
        {
            Tool = tool;
            Args = args ?? new JObject();
            Cancel = cancel;
            _progress = progress;
        }

        public void Note(string text)
        {
            if (!string.IsNullOrEmpty(text) && !Notes.Contains(text))
                Notes.Add(text);
        }

        /// <summary>Tell the client how far along a long tool is (it may show it, and it keeps the request alive).</summary>
        public void Progress(string text, double? done = null, double? total = null)
        {
            try { _progress?.Invoke(text, done, total); } catch { }
        }

        public void ThrowIfCancelled() => Cancel.ThrowIfCancellationRequested();

        public bool Has(string name) => Args[name] != null && Args[name].Type != JTokenType.Null;

        public JToken Token(string name) => Has(name) ? Args[name] : null;

        public string Str(string name, bool required = false, string fallback = null)
        {
            JToken token = Token(name);
            if (token == null)
            {
                if (required) throw new McpError("'" + name + "' is required.");
                return fallback;
            }
            string value = token.Type == JTokenType.String ? (string)token : token.ToString(Formatting.None);
            if (required && string.IsNullOrWhiteSpace(value))
                throw new McpError("'" + name + "' is required.");
            return value;
        }

        public bool Bool(string name, bool fallback = false)
        {
            JToken token = Token(name);
            if (token == null) return fallback;
            if (token.Type == JTokenType.Boolean) return (bool)token;
            if (token.Type == JTokenType.String && bool.TryParse((string)token, out bool parsed)) return parsed;
            if (token.Type == JTokenType.Integer) return (long)token != 0;
            throw new McpError("'" + name + "' must be true or false.");
        }

        public int Int(string name, int fallback = 0)
        {
            JToken token = Token(name);
            if (token == null) return fallback;
            try { return token.Value<int>(); }
            catch { throw new McpError("'" + name + "' must be a whole number."); }
        }

        public double Num(string name, double fallback = 0)
        {
            JToken token = Token(name);
            if (token == null) return fallback;
            try { return token.Value<double>(); }
            catch { throw new McpError("'" + name + "' must be a number."); }
        }

        /// <summary>A list of strings, given as an array or as one string.</summary>
        public List<string> StrList(string name, bool required = false)
        {
            JToken token = Token(name);
            if (token == null)
            {
                if (required) throw new McpError("'" + name + "' is required.");
                return new List<string>();
            }
            if (token is JArray array)
                return array.Select(o => o.Type == JTokenType.String ? (string)o : o.ToString(Formatting.None)).ToList();
            return new List<string>() { token.Type == JTokenType.String ? (string)token : token.ToString(Formatting.None) };
        }

        public JArray Array(string name, bool required = false)
        {
            JToken token = Token(name);
            if (token == null)
            {
                if (required) throw new McpError("'" + name + "' is required.");
                return new JArray();
            }
            if (token is JArray array) return array;
            return new JArray(token);
        }

        public JObject Object(string name, bool required = false)
        {
            JToken token = Token(name);
            if (token == null)
            {
                if (required) throw new McpError("'" + name + "' is required.");
                return null;
            }
            if (token is JObject obj) return obj;
            throw new McpError("'" + name + "' must be an object.");
        }
    }

    /// <summary>Small builders for tools' JSON input schemas.</summary>
    internal static class McpSchema
    {
        public sealed class Prop
        {
            public string Name;
            public JObject Schema;
            public bool Required;
        }

        public static JObject Object(params Prop[] props)
        {
            JObject properties = new JObject();
            JArray required = new JArray();
            foreach (Prop prop in props)
            {
                properties[prop.Name] = prop.Schema;
                if (prop.Required) required.Add(prop.Name);
            }
            JObject schema = new JObject() { ["type"] = "object", ["properties"] = properties };
            if (required.Count != 0) schema["required"] = required;
            return schema;
        }

        public static Prop String(string name, string description, bool required = false, IEnumerable<string> options = null)
        {
            JObject schema = new JObject() { ["type"] = "string", ["description"] = description };
            if (options != null) schema["enum"] = new JArray(options);
            return new Prop() { Name = name, Schema = schema, Required = required };
        }

        public static Prop Boolean(string name, string description, bool required = false) =>
            new Prop() { Name = name, Schema = new JObject() { ["type"] = "boolean", ["description"] = description }, Required = required };

        public static Prop Integer(string name, string description, bool required = false) =>
            new Prop() { Name = name, Schema = new JObject() { ["type"] = "integer", ["description"] = description }, Required = required };

        public static Prop Number(string name, string description, bool required = false) =>
            new Prop() { Name = name, Schema = new JObject() { ["type"] = "number", ["description"] = description }, Required = required };

        public static Prop Strings(string name, string description, bool required = false) =>
            new Prop() { Name = name, Schema = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" }, ["description"] = description }, Required = required };

        public static Prop Vector(string name, string description, bool required = false) =>
            new Prop() { Name = name, Schema = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["minItems"] = 3, ["maxItems"] = 3, ["description"] = description }, Required = required };

        public static Prop Array(string name, string description, JObject items, bool required = false) =>
            new Prop() { Name = name, Schema = new JObject() { ["type"] = "array", ["items"] = items, ["description"] = description }, Required = required };

        /// <summary>An object whose keys are free (parameter names to values, say).</summary>
        public static Prop Map(string name, string description, bool required = false) =>
            new Prop() { Name = name, Schema = new JObject() { ["type"] = "object", ["additionalProperties"] = true, ["description"] = description }, Required = required };

        public static Prop Nested(string name, string description, JObject schema, bool required = false)
        {
            JObject copy = (JObject)schema.DeepClone();
            copy["description"] = description;
            return new Prop() { Name = name, Schema = copy, Required = required };
        }

        /// <summary>Any JSON value.</summary>
        public static Prop Any(string name, string description, bool required = false) =>
            new Prop() { Name = name, Schema = new JObject() { ["description"] = description }, Required = required };
    }

    internal static class McpJson
    {
        public static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        });
    }

    /// <summary>Every tool OpenCAGE offers.</summary>
    internal static class McpTools
    {
        private static List<McpTool> _tools;
        private static Dictionary<string, McpTool> _byName;
        private static readonly object _lock = new object();

        public static IReadOnlyList<McpTool> All
        {
            get
            {
                lock (_lock)
                {
                    if (_tools == null)
                    {
                        _tools = new List<McpTool>();
                        _tools.AddRange(McpEditorTools.Tools());
                        _tools.AddRange(McpBrowseTools.Tools());
                        _tools.AddRange(McpScriptTools.Tools());
                        _tools.AddRange(McpFlowgraphTools.Tools());
                        _tools.AddRange(McpPortingTools.Tools());
                        _tools.AddRange(McpAssetTools.Tools());
                        _tools.AddRange(McpViewportTools.Tools());
                        _tools.AddRange(McpImportTools.Tools());
                        _tools.AddRange(McpEditingTools.Tools());
                        _tools.AddRange(McpZoneTools.Tools());
                        _tools.AddRange(McpPageTools.Tools());
                        _tools.AddRange(McpCageAnimationTools.Tools());
                        _tools.AddRange(McpResourceTools.Tools());
                        _tools.AddRange(McpMaterialTools.Tools());
                        _tools.AddRange(McpAnimationAssetTools.Tools());
                        _tools.AddRange(McpGameDataTools.Tools());
                        _tools.AddRange(McpLevelTools.Tools());
                        //A name given twice would make the whole list fail: the first one stays, and the clash is logged
                        _byName = new Dictionary<string, McpTool>(StringComparer.OrdinalIgnoreCase);
                        foreach (McpTool tool in _tools.ToList())
                        {
                            if (_byName.ContainsKey(tool.Name))
                            {
                                Debug.Log("MCP", "Two tools are called '" + tool.Name + "'; only the first is offered.");
                                _tools.Remove(tool);
                                continue;
                            }
                            _byName[tool.Name] = tool;
                        }
                    }
                    return _tools;
                }
            }
        }

        public static McpTool Find(string name)
        {
            if (name == null) return null;
            _ = All;
            return _byName.TryGetValue(name, out McpTool tool) ? tool : null;
        }

        public static JArray Describe() => new JArray(All.Select(o => o.Describe()));

        public const string Instructions =
            "OpenCAGE is the level editor for Alien: Isolation. These tools drive the OpenCAGE window the user has open (it is started if it is not), " +
            "so every change appears there as you make it. Changes to the open level's script are steps on its undo history (the undo tool, or Ctrl+Z) and reach the game only when you save; " +
            "each tool's description says when a change is instead written straight away (assets, shared game files).\n\n" +
            "How a level is built: a level's script is a tree of composites. The level's root composite places instances of other composites; composites hold " +
            "entities: functions (built-in types such as ModelReference, Character, TriggerBox, LogicGate - see list_function_types and describe_function_type), " +
            "instances of other composites, variables (a composite's own pins, which appear as parameters and link points on its instances), aliases (overrides " +
            "that reach into a nested instance) and proxies. Entities are wired with links (source entity.parameter -> target entity.parameter: events from a relay " +
            "or target pin to a method pin, data from a parameter to a reference or variable pin) and configured with parameters. Positions are relative to the composite.\n\n" +
            "Typical work: get_editor_state first. Use load_level or create_level to get a level open. Find content with find_composites / find_entities, or " +
            "search_level for another level; port_composites brings composites (with their models, materials, textures and collision) in from another level. " +
            "create_entities places instances and functions and can wire them in the same call; set_parameters and add_links configure and wire existing ones " +
            "(list_enum_string_values gives valid values for sound, animation and other named parameters). Scripts stay drawn in the flowgraph editor automatically; " +
            "get_flowgraph, edit_flowgraph_pages and edit_flowgraph_nodes arrange pages. save_level writes the level; save_level with build=true (Save & Build) also " +
            "rebuilds lighting, navigation and the rest of the runtime data the game needs to run the level - do that before playing it (launch_game).\n\n" +
            "Beyond the script: get_entity_resources and set_renderable / set_collision / set_physics_system change an entity's model, materials, collision and physics; " +
            "the model, material, texture and collision/physics import tools add assets; get_cage_animation, animate_parameters and set_animation_events edit CAGEAnimations; " +
            "capture_viewport and set_viewport_view let you look at the result.\n\n" +
            "Zones decide what the game streams and draws, keeping memory down by loading parts of a level only when they can be seen or entered. They live in the composite " +
            "holding the level's geometry (a vanilla level's ENVIRONMENT_... composite, or the root of a level made from scratch), since links only join zones in their own composite. " +
            "A Zone claims content - models, collision, lights, triggers - through its 'composites' pin, usually via one TriggerSequence listing instances and functions " +
            "(Zone.composites -> TriggerSequence.reference). Zones are joined by a ZoneLink - a gate that opens and closes (ZoneLink.ZoneA / ZoneB -> each Zone's reference pin), driven by a door, " +
            "by script, or open on reset for an archway - or by a ZoneExclusionLink, which is always open (vanilla uses those between zones further apart; zones whose content touches with no door between get a ZoneLink open on reset). " +
            "Doors: a placed door VARIANT instance (e.g. AYZ\\Doors\\Door_SML, which wraps the base Door_Package logic holding the Door entity) bridges two rooms - it is listed by both zones, " +
            "and its zonelink pin drives the ZoneLink joining them (door.zonelink -> ZoneLink.reference); each door has its own link. A door with a window drives none: its rooms see each other " +
            "with it shut, so they are joined by a ZoneLink open on reset instead; a dead-end door (nothing behind it) is listed by its one zone and drives none. Zones whose load methods script " +
            "calls (animation zones in props) are not rooms. Content in no zone, or a missing, closed or mis-wired link, makes rooms pop in as the player crosses. " +
            "Tools: analyse_zones shows how a level's zones, doors and links fit and what is missing; auto_zone splits unzoned content into zones (dry run first, then merge/drop/name by number) " +
            "and links them; link_zones makes every missing link (dry run first); create_zone (contents may be a room's name; link:true links it too) / set_zone_contents / " +
            "create_zone_link (door 'auto' finds the doors between two zones) / delete_zone for single changes; get_zone_links and check_zones to read back and diagnose - use them rather than " +
            "judging from viewport captures, which do not show streaming. Pointer pins like ZoneA link out to the target's reference pin, never into it. Zone membership reaches the game only at " +
            "Save & Build (save_level with build=true).\n\n" +
            "Tools that change files every level shares (configuration records, text strings, " +
            "ANIMATION.PAK, sound banks, UI.PAK) take effect at once and cannot be undone: run them with dry_run first. " +
            "Refer to composites by their path (e.g. 'Archetypes\\Script\\Mission\\SpawnPositionSelect'; 'root' is the level's root composite) and to entities by the id the tools return, or by name.";
    }
}
