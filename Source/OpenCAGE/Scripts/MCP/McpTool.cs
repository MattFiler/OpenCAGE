using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace OpenCAGE.MCP
{
    /// <summary>One tool an MCP client can call: its name, what it does, the arguments it takes, and the code that runs it.</summary>
    /// <remarks>
    /// The annotations follow one rule, so a client can decide what to ask the user about. ReadOnly: changes no level,
    /// file or game state (moving the editor's view or selection does not count). Destructive: may delete things, or
    /// overwrite work that one undo step cannot bring back (files written straight away, a save, a cleared history).
    /// A script edit the undo history takes back counts only when it deletes. Idempotent: calling it twice with the
    /// same arguments leaves things as calling it once does.
    /// </remarks>
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

        /// <summary>
        /// Answered while another tool runs (it does not queue behind the one-at-a-time gate): only for quick
        /// read-only tools that are safe to run in the middle of another call's work, such as status reads.
        /// </summary>
        public bool Concurrent;

        /// <summary>Which toolset it is offered in when a client asks for only some (see <see cref="McpToolsets"/>); set from its file's family when null.</summary>
        public string Toolset;

        /// <summary>Runs on a background thread; returns anything Newtonsoft can serialise.</summary>
        public Func<McpCall, object> Run;

        /// <summary>
        /// Past this a result is trimmed: lists are cut short (saying so in 'trimmed') rather than the text being cut,
        /// so it stays valid JSON. Clients cap a tool result at about 100k characters and pay for every one.
        /// </summary>
        public const int MaxResultCharacters = 80000;

        public JObject Describe()
        {
            //The spec's defaults (readOnly false, idempotent false) are left out to keep the list small;
            //destructiveHint only means something for a tool that is not read-only
            JObject annotations = new JObject();
            if (ReadOnly) annotations["readOnlyHint"] = true;
            else annotations["destructiveHint"] = Destructive;
            if (Idempotent) annotations["idempotentHint"] = true;
            annotations["openWorldHint"] = false;
            JObject described = new JObject()
            {
                ["name"] = Name,
                ["title"] = Title,
                ["description"] = Description,
                ["inputSchema"] = McpSchema.ForClients(InputSchema ?? McpSchema.Object()),
                ["annotations"] = annotations,
            };
            if (Toolset != null)
                described["_meta"] = new JObject() { [McpToolsets.MetaKey] = Toolset };
            return described;
        }

        public JObject Invoke(McpCall call)
        {
            //A misspelt argument would otherwise be ignored without a word, and the tool do something other than was asked.
            //Nested objects too: a misspelt key in a link or entity spec would quietly do something else
            string invalid = McpValidation.Check(this, call.Args);
            if (invalid != null)
                return ErrorResult(invalid, call, McpErrorCodes.InvalidArgument);

            object result;
            try
            {
                McpEditor.BeginCall(call);
                //A step made while the editor is part-way through an edit of its own would be refused: give it a moment first
                if (!ReadOnly)
                    McpEditor.WaitUndoIdle(TimeSpan.FromSeconds(2));
                using (Concurrent ? null : McpDialogs.Watch(call))
                    result = Run(call);
            }
            catch (McpError e)
            {
                return ErrorResult(e.Message, call, e.Code, e.Candidates);
            }
            catch (OperationCanceledException)
            {
                return ErrorResult("Cancelled.", call, McpErrorCodes.Cancelled);
            }
            catch (Exception e)
            {
                Exception inner = e;
                while ((inner is System.Reflection.TargetInvocationException || inner is McpUiException) && inner.InnerException != null)
                    inner = inner.InnerException;
                if (inner is McpError error)
                    return ErrorResult(error.Message, call, error.Code, error.Candidates);
                Debug.Log("MCP", Name + " failed: " + inner);
                return ErrorResult(Name + " failed: " + inner.GetType().Name + ": " + inner.Message, call, McpErrorCodes.Failed);
            }

            if (result is McpImage image)
            {
                string caption = image.Caption ?? "";
                if (call.Notes.Count != 0) caption += "\nNotes: " + string.Join(" ", call.Notes);
                if (call.Dialogs.Count != 0) caption += "\nOpenCAGE showed (and these were dismissed): " + string.Join(" | ", call.Dialogs);
                if (call.Level != null) caption += "\nOpen level: " + call.Level;
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
                body = McpResults.Frame(obj, call);
            string text = body.Type == JTokenType.String ? (string)body : McpResults.Fit(body, call, MaxResultCharacters);
            return new JObject()
            {
                ["content"] = new JArray(new JObject() { ["type"] = "text", ["text"] = text }),
                ["isError"] = false,
            };
        }

        /// <summary>
        /// A failed call's result: "[code] message", where the code (<see cref="McpErrorCodes"/>) says what kind of failure
        /// it is (inferred from the message when not given), followed by any candidates and the messages OpenCAGE showed.
        /// </summary>
        public static JObject ErrorResult(string message, McpCall call = null, string code = null, JArray candidates = null)
        {
            string text = "[" + (code ?? McpErrorCodes.Infer(message)) + "] " + message;
            //Names are in the message already; objects (ids, paths) are not
            if (candidates != null && candidates.Any(o => o is JObject))
                text += "\ncandidates: " + candidates.ToString(Formatting.None);
            if (call != null && call.Dialogs.Count != 0)
                text += "\n\nOpenCAGE showed these messages, which were dismissed:\n- " + string.Join("\n- ", call.Dialogs);
            return new JObject()
            {
                ["content"] = new JArray(new JObject() { ["type"] = "text", ["text"] = text }),
                ["isError"] = true,
            };
        }
    }

    /// <summary>
    /// What kind of failure a tool reports, at the start of its error text ("[not_found] There is no level ...") so a
    /// caller can tell a typo from a busy editor without parsing prose.
    /// </summary>
    internal static class McpErrorCodes
    {
        /// <summary>Nothing has that name or id (the message suggests near names).</summary>
        public const string NotFound = "not_found";
        /// <summary>The name fits several things: pick one by id (the candidates are listed).</summary>
        public const string Ambiguous = "ambiguous";
        /// <summary>OpenCAGE is saving, loading or mid-edit: the same call should work in a moment.</summary>
        public const string Busy = "busy";
        /// <summary>An argument is missing, misspelt, of the wrong kind or out of range.</summary>
        public const string InvalidArgument = "invalid_argument";
        /// <summary>No level is open: load_level or create_level first.</summary>
        public const string LevelNotLoaded = "level_not_loaded";
        /// <summary>The user opened another level since this client's last call; nothing was done.</summary>
        public const string LevelChanged = "level_changed";
        /// <summary>The state is not what the call expected (someone else changed it), or it would clash with what is there.</summary>
        public const string Conflict = "conflict";
        /// <summary>The tool declined, for the reason given.</summary>
        public const string Refused = "refused";
        /// <summary>The client cancelled it.</summary>
        public const string Cancelled = "cancelled";
        /// <summary>Something went wrong inside OpenCAGE (a bug, or a broken file): the message has the details.</summary>
        public const string Failed = "failed";

        /// <summary>The code an <see cref="McpError"/> made without one most likely means, from its message.</summary>
        public static string Infer(string message)
        {
            string text = message ?? "";
            if (Has(text, "No level is open")) return LevelNotLoaded;
            if (Has(text, "Try again") || Has(text, "is saving") || Has(text, "still loading") || Has(text, "is loading") || Has(text, "in the middle of") || Has(text, "backup is running") || Has(text, "is busy"))
                return Busy;
            if (Has(text, "ambiguous") || Has(text, "more than one") || Has(text, "Which one") || Has(text, "Which of"))
                return Ambiguous;
            if (Has(text, "Unknown argument") || Has(text, "Unknown field") || Has(text, "unknown key") || Has(text, " is required") || Has(text, " must be ") || Has(text, " has to be ") || Has(text, "cannot be combined") || Has(text, "Unknown action"))
                return InvalidArgument;
            if (text.StartsWith("There is no ") || text.StartsWith("No ") || Has(text, "not found") || Has(text, "does not exist") || Has(text, "is not in the level") || Has(text, "no longer in the level") || Has(text, " has no "))
                return NotFound;
            if (text.StartsWith("Cancelled")) return Cancelled;
            if (Has(text, "already ")) return Conflict;
            return Refused;
        }

        private static bool Has(string text, string part) => text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>A tool that could not do what it was asked, for a reason the caller can act on.</summary>
    internal sealed class McpError : Exception
    {
        /// <summary>What kind of failure (<see cref="McpErrorCodes"/>).</summary>
        public string Code { get; }

        /// <summary>For not_found / ambiguous: what the caller could have meant ({id, name, type, path} objects, or names).</summary>
        public JArray Candidates { get; set; }

        public McpError(string message) : base(message) { Code = McpErrorCodes.Infer(message); }

        public McpError(string code, string message) : base(message) { Code = code ?? McpErrorCodes.Infer(message); }

        /// <summary>
        /// "There is no &lt;what&gt; '&lt;query&gt;'." with the nearest names offered (<see cref="McpNames"/>): typos,
        /// other spellings, a word of the name, or its last path segment.
        /// </summary>
        /// <param name="hint">What to do instead, e.g. "find_composites lists them."</param>
        public static McpError NotFound(string what, string query, IEnumerable<string> names, string hint = null)
        {
            List<string> near = names == null ? new List<string>() : McpNames.Similar(names, query, 6);
            string message = "There is no " + what + " '" + query + "'." + (near.Count == 0 ? "" : " Did you mean " + McpNames.Quote(near) + "?") + (hint == null ? "" : " " + hint);
            return new McpError(McpErrorCodes.NotFound, message) { Candidates = near.Count == 0 ? null : new JArray(near) };
        }

        /// <summary>
        /// "'&lt;query&gt;' matches N &lt;what&gt;: ..." listing each candidate the same way (id, name, type, path), so the
        /// caller can pick one and call again with its id.
        /// </summary>
        /// <param name="candidates">Objects with any of id, name, type, path (others are kept).</param>
        public static McpError Ambiguous(string what, string query, IEnumerable<JObject> candidates, string hint = "Call again with one of their ids.")
        {
            List<JObject> all = candidates.ToList();
            const int shown = 12;
            string list = string.Join("; ", all.Take(shown).Select(Describe));
            string message = "'" + query + "' matches " + all.Count + " " + what + ": " + list + (all.Count > shown ? "; and " + (all.Count - shown) + " more" : "") + "." + (hint == null ? "" : " " + hint);
            return new McpError(McpErrorCodes.Ambiguous, message);
        }

        private static string Describe(JObject candidate)
        {
            string id = (string)candidate["id"], name = (string)candidate["name"], type = (string)candidate["type"], path = (string)candidate["path"];
            string text = (id != null ? "[" + id + "] " : "") + (name ?? "");
            List<string> extra = new List<string>();
            if (type != null) extra.Add(type);
            if (path != null) extra.Add(path);
            return text + (extra.Count == 0 ? "" : " (" + string.Join(", ", extra) + ")");
        }

        /// <summary>OpenCAGE cannot do it right now, but will shortly (saving, loading, mid-edit).</summary>
        public static McpError Busy(string message) => new McpError(McpErrorCodes.Busy, message);

        /// <summary>An argument is wrong: say which, and what it takes.</summary>
        public static McpError Invalid(string message) => new McpError(McpErrorCodes.InvalidArgument, message);
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

        /// <summary>The level the call worked in (set when it asked for the open level), added to its result as 'open_level'.</summary>
        public string Level { get; internal set; }

        /// <summary>The call opened (or re-opened) a level itself: its client knows the level it leaves open (see McpServer's level check).</summary>
        public bool OpenedLevel { get; internal set; }

        /// <summary>The last progress message, for get_activity (kept whether or not the client asked for progress).</summary>
        public string LastProgress { get; private set; }

        public DateTime Started { get; } = DateTime.Now;

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
            LastProgress = text;
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
    /// <remarks>
    /// Every object schema that lists 'properties' is closed: a call with a key it does not list is refused, at any
    /// depth (<see cref="McpValidation"/>), so list every key a handler reads. A free-form object (parameter names to
    /// values, say) is a <see cref="Map"/>, or sets additionalProperties to true.
    /// </remarks>
    internal static class McpSchema
    {
        public sealed class Prop
        {
            public string Name;
            public JObject Schema;
            public bool Required;
        }

        /* The conventions every tool shares. Put them in argument and result descriptions (or use the Prop builders below),
           so a caller never has to guess an axis, a unit or a scale. */

        /// <summary>Positions and offsets.</summary>
        public const string PositionText = "[x, y, z] metres, Y up";
        /// <summary>Euler rotations, as InstanceTransform and the inspector use them (System.Numerics CreateFromYawPitchRoll).</summary>
        public const string RotationText = "[x, y, z] degrees: x = pitch (positive looks down), y = yaw (about +Y, up), z = roll, applied yaw, then pitch, then roll. An entity faces its local +Z: to face direction d use [-asin(d.y/|d|), atan2(d.x, d.z), 0]";
        /// <summary>Colour parameters on entities (LightReference colour, emissive tints...). A few run 0-1 (vertex_colour_scale): the parameter says so.</summary>
        public const string ColourText = "[r, g, b], 0-255 per channel ([255, 255, 255] is white; [1, 0, 0] would be near-black)";
        /// <summary>Which space a position or rotation is in: say it on every spatial result ("space": "world" or "composite").</summary>
        public const string SpaceText = "'world' is the level's space (the root composite's); 'composite' is relative to the composite the entity sits in (one placement of it, when it is placed more than once)";
        /// <summary>The standard phrases for what 'composite' means when it is left out.</summary>
        public const string CompositeDefaultRoot = "Default 'root' (the level's root composite).";
        public const string CompositeDefaultShown = "Default: the composite on screen in the editor.";
        public const string CompositeRequired = "Required: a composite path (e.g. 'AYZ\\Doors\\Door_SML') or id; 'root' is the level's root composite.";

        /// <summary>A position argument: "[x, y, z] metres, Y up" is added to the description.</summary>
        public static Prop Position(string name, string description, bool required = false) => Vector(name, Convention(description, PositionText), required);

        /// <summary>A rotation argument: the axis order, units and facing rule are added to the description.</summary>
        public static Prop Rotation(string name, string description, bool required = false) => Vector(name, Convention(description, RotationText), required);

        /// <summary>A colour argument: the 0-255 scale is added to the description.</summary>
        public static Prop Colour(string name, string description, bool required = false) => Vector(name, Convention(description, ColourText), required);

        private static string Convention(string description, string convention) =>
            string.IsNullOrWhiteSpace(description) ? convention + "." : description.TrimEnd().TrimEnd('.') + ": " + convention + ".";

        /// <summary>The 'limit' argument of a list (clamped by <see cref="McpPaging.Limit"/>).</summary>
        public static Prop Limit(int defaultLimit, string what = "items", int max = McpPaging.MaxLimit) =>
            Integer("limit", "Most " + what + " to return (default " + defaultLimit + ", at most " + max + "); the result's next_offset continues.");

        /// <summary>The 'offset' argument of a list (<see cref="McpPaging"/>).</summary>
        public static Prop Offset(string what = "items") =>
            Integer("offset", "Skip this many " + what + " first (default 0): pass the previous result's next_offset for the next page.");

        /// <summary>An argument still accepted (an older name, say) but no longer listed to clients.</summary>
        public static Prop Deprecated(Prop prop)
        {
            prop.Schema["deprecated"] = true;
            return prop;
        }

        /// <summary>The schema as clients are given it: arguments marked deprecated are left out (they are still accepted).</summary>
        public static JObject ForClients(JObject schema)
        {
            if (!HasDeprecated(schema))
                return schema;
            JObject copy = (JObject)schema.DeepClone();
            StripDeprecated(copy);
            return copy;
        }

        private static bool HasDeprecated(JToken token)
        {
            if (token is JObject obj)
            {
                if (obj["properties"] is JObject properties && properties.Properties().Any(o => o.Value is JObject p && p["deprecated"]?.Type == JTokenType.Boolean && (bool)p["deprecated"]))
                    return true;
                return obj.Properties().Any(o => HasDeprecated(o.Value));
            }
            if (token is JArray array)
                return array.Any(HasDeprecated);
            return false;
        }

        private static void StripDeprecated(JToken token)
        {
            if (token is JObject obj)
            {
                if (obj["properties"] is JObject properties)
                {
                    foreach (JProperty property in properties.Properties().ToList())
                    {
                        if (property.Value is JObject p && p["deprecated"]?.Type == JTokenType.Boolean && (bool)p["deprecated"])
                        {
                            property.Remove();
                            if (obj["required"] is JArray required)
                                foreach (JToken entry in required.Where(o => (string)o == property.Name).ToList())
                                    entry.Remove();
                        }
                    }
                }
                foreach (JProperty property in obj.Properties().ToList())
                    StripDeprecated(property.Value);
            }
            else if (token is JArray array)
            {
                foreach (JToken item in array)
                    StripDeprecated(item);
            }
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

        /// <summary>The same JSON with every object's keys in order: two calls with the same arguments give the same text.</summary>
        public static string Canonical(JToken token)
        {
            return Sorted(token).ToString(Formatting.None);
        }

        private static JToken Sorted(JToken token)
        {
            if (token is JObject obj)
            {
                JObject sorted = new JObject();
                foreach (JProperty property in obj.Properties().OrderBy(o => o.Name, StringComparer.Ordinal))
                    sorted[property.Name] = Sorted(property.Value);
                return sorted;
            }
            if (token is JArray array)
                return new JArray(array.Select(Sorted));
            return token?.DeepClone();
        }
    }

    /// <summary>
    /// Checks a call's arguments against its tool's input schema before it runs. An object schema that lists
    /// 'properties' is closed at any depth (unless additionalProperties is true): a key it does not list is refused,
    /// naming where it was, the keys allowed there, and the likeliest one meant.
    /// </summary>
    internal static class McpValidation
    {
        //Names assistants reach for that mean another argument: suggested when one of these is allowed there
        private static readonly Dictionary<string, string[]> Synonyms = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["params"] = new[] { "parameters" },
            ["parameter_values"] = new[] { "parameters" },
            ["values"] = new[] { "parameters", "value" },
            ["pos"] = new[] { "position" },
            ["location"] = new[] { "position" },
            ["translation"] = new[] { "position" },
            ["rot"] = new[] { "rotation" },
            ["orientation"] = new[] { "rotation" },
            ["pin"] = new[] { "param", "parameter" },
            ["from_pin"] = new[] { "param" },
            ["from_param"] = new[] { "param" },
            ["source_param"] = new[] { "param" },
            ["to_pin"] = new[] { "to_param" },
            ["target_param"] = new[] { "to_param" },
            ["target_pin"] = new[] { "to_param" },
            ["target"] = new[] { "to", "entity" },
            ["source"] = new[] { "from" },
            ["type"] = new[] { "function", "kind" },
            ["function_type"] = new[] { "function", "type" },
            ["id"] = new[] { "ref", "entity" },
            ["entity_id"] = new[] { "entity" },
            ["query"] = new[] { "filter", "name", "text" },
            ["search"] = new[] { "filter", "name" },
            ["level_name"] = new[] { "level" },
            ["composite_path"] = new[] { "composite", "path" },
            ["count"] = new[] { "limit", "steps" },
            ["max"] = new[] { "limit" },
            ["skip"] = new[] { "offset" },
            ["colour"] = new[] { "color" },
            ["color"] = new[] { "colour" },
            ["delay_s"] = new[] { "delay" },
            ["seconds"] = new[] { "delay", "time", "duration" },
        };

        /// <summary>Null when the arguments fit the schema; otherwise what is wrong, for the caller.</summary>
        public static string Check(McpTool tool, JObject args)
        {
            JObject schema = tool.InputSchema ?? McpSchema.Object();
            JObject properties = schema["properties"] as JObject ?? new JObject();
            List<string> listed = Listed(properties);
            List<string> unknown = args.Properties().Select(o => o.Name).Where(o => properties[o] == null).ToList();
            if (unknown.Count != 0)
                return "Unknown argument" + (unknown.Count > 1 ? "s " : " ") + string.Join(", ", unknown.Select(o => "'" + o + "'" + Suggest(o, listed))) + ". " +
                    (listed.Count == 0 ? tool.Name + " takes none." : tool.Name + " takes: " + string.Join(", ", listed) + ".");

            List<string> problems = new List<string>();
            foreach (JProperty argument in args.Properties())
                Walk(argument.Value, properties[argument.Name] as JObject, argument.Name, problems);
            if (problems.Count == 0)
                return null;
            return string.Join(" ", problems.Take(4)) + (problems.Count > 4 ? " (And " + (problems.Count - 4) + " more like these.)" : "");
        }

        private static void Walk(JToken value, JObject schema, string path, List<string> problems)
        {
            if (schema == null || value == null || value.Type == JTokenType.Null || problems.Count > 8)
                return;
            List<JObject> branches = new List<JObject>();
            Flatten(schema, branches);

            if (value is JObject obj)
            {
                Dictionary<string, JObject> known = new Dictionary<string, JObject>();
                List<string> listed = new List<string>();
                bool closed = false, open = false;
                JObject valueSchema = null;
                foreach (JObject branch in branches)
                {
                    string type = branch["type"]?.Type == JTokenType.String ? (string)branch["type"] : null;
                    JToken more = branch["additionalProperties"];
                    if (more is JObject moreSchema)
                        valueSchema = moreSchema;
                    else if (more != null && more.Type == JTokenType.Boolean && (bool)more)
                        open = true;
                    if (!(branch["properties"] is JObject properties))
                    {
                        //A union's own node says nothing of the keys (Flatten has added its options as branches): not an open object
                        if (type == null && (branch["anyOf"] != null || branch["oneOf"] != null || branch["allOf"] != null))
                            continue;
                        //A branch that takes any object (or any value at all) leaves the keys free
                        if (more == null && (type == null || type == "object") && branch["items"] == null && branch["enum"] == null)
                            open = true;
                        continue;
                    }
                    closed = true;
                    foreach (JProperty property in properties.Properties())
                    {
                        if (known.ContainsKey(property.Name)) continue;
                        known[property.Name] = property.Value as JObject;
                        if (!IsDeprecated(property.Value)) listed.Add(property.Name);
                    }
                }
                List<string> unknown = new List<string>();
                foreach (JProperty member in obj.Properties())
                {
                    string at = path + "." + member.Name;
                    if (known.TryGetValue(member.Name, out JObject sub))
                        Walk(member.Value, sub, at, problems);
                    else if (valueSchema != null)
                        Walk(member.Value, valueSchema, at, problems);
                    else if (closed && !open)
                        unknown.Add("'" + member.Name + "'" + Suggest(member.Name, listed));
                }
                if (unknown.Count != 0)
                    problems.Add(path + ": unknown key" + (unknown.Count > 1 ? "s " : " ") + string.Join(", ", unknown) + ". It takes: " + string.Join(", ", listed) + ".");
            }
            else if (value is JArray array)
            {
                JObject items = branches.Select(o => o["items"] as JObject).FirstOrDefault(o => o != null);
                if (items == null)
                    return;
                for (int i = 0; i < array.Count; i++)
                    Walk(array[i], items, path + "[" + i + "]", problems);
            }
        }

        private static void Flatten(JObject schema, List<JObject> into)
        {
            into.Add(schema);
            foreach (string combiner in new[] { "anyOf", "oneOf", "allOf" })
                if (schema[combiner] is JArray options)
                    foreach (JObject option in options.OfType<JObject>())
                        Flatten(option, into);
        }

        private static bool IsDeprecated(JToken schema) => schema is JObject obj && obj["deprecated"]?.Type == JTokenType.Boolean && (bool)obj["deprecated"];

        private static List<string> Listed(JObject properties) => properties.Properties().Where(o => !IsDeprecated(o.Value)).Select(o => o.Name).ToList();

        /// <summary>" (did you mean 'x'?)" when one of the allowed names is the likely one, else empty.</summary>
        private static string Suggest(string given, List<string> allowed)
        {
            if (Synonyms.TryGetValue(given, out string[] meant))
            {
                string synonym = meant.FirstOrDefault(o => allowed.Contains(o));
                if (synonym != null) return " (did you mean '" + synonym + "'?)";
            }
            string near = McpNames.Closest(given, allowed);
            return near == null ? "" : " (did you mean '" + near + "'?)";
        }
    }

    /// <summary>
    /// Matching what a user or assistant typed to the names things really have: ignoring case, spaces, '_', '-' and path
    /// separators; whole names, their last path segment, prefixes, words in any order, and small typos.
    /// Use <see cref="Rank"/> for "did you mean" lists and fuzzy searches, and <see cref="McpError.NotFound"/> for the error.
    /// </summary>
    internal static class McpNames
    {
        /// <summary>Lower case letters and digits only: "BSP_Torrens" and "bsp torrens" are the same.</summary>
        public static string Squash(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            StringBuilder squashed = new StringBuilder(text.Length);
            foreach (char c in text)
                if (char.IsLetterOrDigit(c))
                    squashed.Append(char.ToLowerInvariant(c));
            return squashed.ToString();
        }

        /// <summary>The words of a name: split at separators, lower-to-upper case changes and letter/digit changes ("TriggerBox_02" is trigger, box, 02).</summary>
        public static List<string> Words(string text)
        {
            List<string> words = new List<string>();
            if (string.IsNullOrEmpty(text)) return words;
            StringBuilder word = new StringBuilder();
            char previous = '\0';
            foreach (char c in text)
            {
                bool split = !char.IsLetterOrDigit(c)
                    || (word.Length != 0 && char.IsUpper(c) && char.IsLower(previous))
                    || (word.Length != 0 && char.IsDigit(c) != char.IsDigit(previous));
                if (split && word.Length != 0)
                {
                    words.Add(word.ToString());
                    word.Clear();
                }
                if (char.IsLetterOrDigit(c))
                    word.Append(char.ToLowerInvariant(c));
                previous = c;
            }
            if (word.Length != 0) words.Add(word.ToString());
            return words;
        }

        /// <summary>The last segment of a path ('AYZ\Doors\Door_SML' is Door_SML).</summary>
        public static string Leaf(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int cut = Math.Max(text.LastIndexOf('\\'), text.LastIndexOf('/'));
            return cut < 0 ? text : text.Substring(cut + 1);
        }

        /// <summary>Edit distance (insertions, deletions, substitutions, swaps of neighbours); anything over <paramref name="max"/> is max + 1.</summary>
        public static int Distance(string a, string b, int max)
        {
            if (Math.Abs(a.Length - b.Length) > max) return max + 1;
            int[,] d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                int best = int.MaxValue;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    int value = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                        value = Math.Min(value, d[i - 2, j - 2] + 1);
                    d[i, j] = value;
                    best = Math.Min(best, value);
                }
                if (best > max) return max + 1;
            }
            return Math.Min(d[a.Length, b.Length], max + 1);
        }

        //How many typos a word of this length may have
        private static int Tolerance(int length) => length <= 3 ? 0 : length <= 5 ? 1 : length <= 9 ? 2 : 3;

        /// <summary>
        /// How well a name fits what was typed, 0 (not at all) to 1 (the same, ignoring case and separators). Above
        /// about 0.5 it is a likely meaning; 0.3-0.5 a loose one worth listing.
        /// </summary>
        public static double Score(string candidate, string query)
        {
            string q = Squash(query), c = Squash(candidate);
            if (q.Length == 0 || c.Length == 0) return 0;
            if (c == q) return 1;
            string leaf = Squash(Leaf(candidate));
            if (leaf == q) return 0.95;
            if (q.Length >= 2 && leaf.StartsWith(q)) return 0.8 + 0.1 * q.Length / leaf.Length;
            if (q.Length >= 2 && c.StartsWith(q)) return 0.75 + 0.1 * q.Length / c.Length;
            double score = 0;
            if (q.Length >= 3 && c.Contains(q)) score = 0.65 + 0.1 * q.Length / c.Length;

            //Words in any order, each matching a word of the name exactly, as a prefix, or with a typo or two
            List<string> qWords = Words(query), cWords = Words(candidate);
            if (qWords.Count != 0 && cWords.Count != 0)
            {
                int matched = 0;
                double quality = 0;
                foreach (string word in qWords)
                {
                    double best = 0;
                    foreach (string other in cWords)
                    {
                        if (other == word) best = Math.Max(best, 1);
                        else if (word.Length >= 2 && other.StartsWith(word)) best = Math.Max(best, 0.85);
                        else if (other.Length >= 3 && word.StartsWith(other)) best = Math.Max(best, 0.6);
                        else
                        {
                            int tolerance = Tolerance(Math.Min(word.Length, other.Length));
                            if (tolerance > 0)
                            {
                                int distance = Distance(word, other, tolerance);
                                if (distance <= tolerance) best = Math.Max(best, 0.8 - 0.15 * distance);
                            }
                        }
                    }
                    if (best > 0) matched++;
                    quality += best;
                }
                if (matched == qWords.Count)
                    score = Math.Max(score, 0.45 + 0.25 * quality / qWords.Count + 0.05 * Math.Min(1.0, (double)matched / cWords.Count));
                else if (matched * 2 >= qWords.Count)
                    score = Math.Max(score, 0.25 + 0.15 * quality / qWords.Count);
            }

            //The whole name (or its last segment) misspelt
            int allowed = Tolerance(q.Length);
            if (allowed > 0)
            {
                int distance = Math.Min(Distance(leaf, q, allowed), Distance(c, q, allowed));
                if (distance <= allowed) score = Math.Max(score, 0.75 - 0.08 * distance);
            }
            return Math.Min(score, 0.99);
        }

        /// <summary>The items whose names fit the query best, best first (then shortest name), at least <paramref name="minimum"/>.</summary>
        public static List<T> Rank<T>(IEnumerable<T> items, Func<T, string> name, string query, int max = 8, double minimum = 0.3)
        {
            return items
                .Select(o => (item: o, name: name(o) ?? "", score: Score(name(o) ?? "", query)))
                .Where(o => o.score >= minimum)
                .OrderByDescending(o => o.score)
                .ThenBy(o => o.name.Length)
                .ThenBy(o => o.name, StringComparer.OrdinalIgnoreCase)
                .Take(max)
                .Select(o => o.item)
                .ToList();
        }

        /// <summary>
        /// The names nearest the query, best first, for a "did you mean" (distinct, ignoring case): only those nearly as
        /// good as the best, so a clear match is not buried among loose ones.
        /// </summary>
        public static List<string> Similar(IEnumerable<string> names, string query, int max = 5, double minimum = 0.3)
        {
            List<(string name, double score)> scored = names.Where(o => !string.IsNullOrEmpty(o)).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(o => (name: o, score: Score(o, query))).Where(o => o.score >= minimum).ToList();
            if (scored.Count == 0)
                return new List<string>();
            double best = scored.Max(o => o.score);
            return scored.Where(o => o.score >= best - 0.12)
                .OrderByDescending(o => o.score).ThenBy(o => o.name.Length).ThenBy(o => o.name, StringComparer.OrdinalIgnoreCase)
                .Take(max).Select(o => o.name).ToList();
        }

        /// <summary>The one name the query most likely meant (a strong match only), or null.</summary>
        public static string Closest(string query, IEnumerable<string> names)
        {
            return Similar(names, query, 1, 0.5).FirstOrDefault();
        }

        /// <summary>" Did you mean 'a', 'b' or 'c'?" (with a leading space), or empty when nothing is near.</summary>
        public static string DidYouMean(IEnumerable<string> names, string query, int max = 5)
        {
            List<string> near = Similar(names, query, max);
            return near.Count == 0 ? "" : " Did you mean " + Quote(near) + "?";
        }

        /// <summary>'a', 'b' or 'c'.</summary>
        public static string Quote(IList<string> names)
        {
            if (names.Count == 0) return "";
            if (names.Count == 1) return "'" + names[0] + "'";
            return string.Join(", ", names.Take(names.Count - 1).Select(o => "'" + o + "'")) + " or '" + names[names.Count - 1] + "'";
        }
    }

    /// <summary>
    /// Paging for list and search tools, one way everywhere: 'limit' and 'offset' arguments (<see cref="McpSchema.Limit"/>,
    /// <see cref="McpSchema.Offset"/>), and 'total', 'offset' and 'next_offset' (when there is more) in the result.
    /// Give it the items in a stable order (by path, then id) so offsets mean the same thing from call to call.
    /// </summary>
    internal static class McpPaging
    {
        /// <summary>No list returns more than this many items in one call.</summary>
        public const int MaxLimit = 500;

        /// <summary>The 'limit' argument, clamped to 1..max (with a note when it was over).</summary>
        public static int Limit(McpCall call, int defaultLimit, int max = MaxLimit, string name = "limit")
        {
            int limit = call.Int(name, defaultLimit);
            if (limit < 1) limit = 1;
            if (limit > max)
            {
                call.Note("'" + name + "' is at most " + max + ": " + max + " were returned; use offset/next_offset for the rest.");
                limit = max;
            }
            return limit;
        }

        /// <summary>The 'offset' argument (0 when not given, never below 0).</summary>
        public static int Offset(McpCall call, string name = "offset") => Math.Max(0, call.Int(name, 0));

        /// <summary>
        /// One page of <paramref name="ordered"/>, written into <paramref name="result"/> as <paramref name="field"/>
        /// (each item through <paramref name="select"/>), with total, offset and next_offset beside it. Returns the page.
        /// </summary>
        public static List<T> Page<T>(McpCall call, IEnumerable<T> ordered, JObject result, string field, Func<T, JToken> select, int defaultLimit, int max = MaxLimit)
        {
            int offset = Offset(call), limit = Limit(call, defaultLimit, max);
            List<T> all = ordered as List<T> ?? ordered.ToList();
            List<T> page = all.Skip(offset).Take(limit).ToList();
            result[field] = new JArray(page.Select(select));
            Describe(result, all.Count, offset, page.Count);
            if (offset != 0 && offset >= all.Count && all.Count != 0)
                call.Note("offset " + offset + " is past the end: there are " + all.Count + ".");
            return page;
        }

        /// <summary>Writes total, offset (when not 0) and next_offset (when more are left) into a result.</summary>
        public static void Describe(JObject result, int total, int offset, int shown)
        {
            result["total"] = total;
            if (offset != 0) result["offset"] = offset;
            if (offset + shown < total) result["next_offset"] = offset + shown;
            else result.Remove("next_offset");
        }
    }

    /// <summary>How a tool's JSON result is put together for the client: notes first, compact, and never cut mid-JSON.</summary>
    internal static class McpResults
    {
        /// <summary>Notes and dismissed messages go first (a trimmed result never loses them), the open level last.</summary>
        public static JObject Frame(JObject result, McpCall call)
        {
            List<JProperty> properties = result.Properties().ToList();
            //A tool's own notes and the call's become one list
            List<string> notes = new List<string>();
            JToken own = result["notes"];
            if (own is JArray ownNotes)
                notes.AddRange(ownNotes.Select(o => o.Type == JTokenType.String ? (string)o : o.ToString(Formatting.None)));
            else if (own != null && own.Type == JTokenType.String)
                notes.Add((string)own);
            else if (own != null && own.Type != JTokenType.Null)
                notes.Add(own.ToString(Formatting.None));
            notes.AddRange(call.Notes);

            result.RemoveAll();
            if (notes.Count != 0) result["notes"] = new JArray(notes.Distinct());
            if (call.Dialogs.Count != 0) result["dialogs_dismissed"] = new JArray(call.Dialogs);
            foreach (JProperty property in properties)
            {
                if (property.Name == "notes" || result.ContainsKey(property.Name)) continue;
                result.Add(property);
            }
            if (call.Level != null && result["open_level"] == null && !string.Equals((string)(result["level"] as JObject)?["name"], call.Level, StringComparison.OrdinalIgnoreCase))
                result["open_level"] = call.Level;
            return result;
        }

        /// <summary>
        /// The result as compact JSON of at most <paramref name="max"/> characters: when it is longer, the biggest lists
        /// are cut short (each listed in 'trimmed' with how many it had, and next_offset where the tool pages), then the
        /// longest strings, so it always parses.
        /// </summary>
        public static string Fit(JToken body, McpCall call, int max)
        {
            string text = body.ToString(Formatting.None);
            if (text.Length <= max)
                return text;

            JArray trimmed = new JArray();
            bool pages = call?.Tool?.InputSchema?["properties"]?["offset"] != null;
            int offset = call == null ? 0 : Math.Max(0, SafeInt(call, "offset"));
            for (int round = 0; round < 12 && text.Length > max; round++)
            {
                //The biggest list (not the notes), and how much of it has to go
                (JArray array, string path, int size) = Biggest(body);
                if (array == null) break;
                int excess = text.Length - max + 400;
                int keep = array.Count, cut = 0;
                while (keep > 0 && cut < excess)
                {
                    keep--;
                    cut += array[keep].ToString(Formatting.None).Length + 1;
                }
                if (keep == array.Count) break;
                JObject entry = trimmed.OfType<JObject>().FirstOrDefault(o => (string)o["field"] == path);
                int total = entry != null ? (int)entry["total"] : array.Count;
                while (array.Count > keep) array.RemoveAt(array.Count - 1);
                if (entry == null)
                {
                    entry = new JObject() { ["field"] = path, ["shown"] = keep, ["total"] = total };
                    trimmed.Add(entry);
                }
                else entry["shown"] = keep;
                //A tool that pages its top-level list can carry on from here
                if (pages && body is JObject top && top[path] == array)
                {
                    int start = offset;
                    entry["next_offset"] = start + keep;
                    top["next_offset"] = start + keep;
                }
                if (body is JObject withTrim) withTrim["trimmed"] = trimmed;
                text = body.ToString(Formatting.None);
            }

            //Still too long: one huge string (a log, a file's text)
            for (int round = 0; round < 8 && text.Length > max; round++)
            {
                JValue longest = Longest(body);
                if (longest == null) break;
                string value = (string)longest.Value;
                int keep = Math.Max(200, value.Length - (text.Length - max) - 200);
                if (keep >= value.Length) break;
                longest.Value = value.Substring(0, keep) + " ... (cut: " + (value.Length - keep) + " more characters)";
                text = body.ToString(Formatting.None);
            }

            if (body is JObject framed && trimmed.Count != 0)
            {
                JArray notes = framed["notes"] as JArray ?? new JArray();
                string note = "The result was over " + max + " characters, so lists were cut short (see 'trimmed'): ask for less with a filter, a smaller limit" + (pages ? ", or the next page with offset" : "") + ".";
                if (!notes.Any(o => (string)o == note)) notes.Add(note);
                if (framed["notes"] == null)
                {
                    //Notes first, as everywhere
                    List<JProperty> rest = framed.Properties().ToList();
                    framed.RemoveAll();
                    framed["notes"] = notes;
                    foreach (JProperty property in rest) framed.Add(property);
                }
                text = body.ToString(Formatting.None);
            }

            if (text.Length > max)
            {
                //Nothing left to trim cleanly: say so, with the start of it
                text = new JObject()
                {
                    ["notes"] = new JArray("The result was too long to return (" + text.Length + " characters) and could not be trimmed: ask for less."),
                    ["preview"] = text.Substring(0, Math.Max(0, max - 1000)),
                }.ToString(Formatting.None);
            }
            return text;
        }

        private static int SafeInt(McpCall call, string name)
        {
            try { return call.Int(name, 0); }
            catch { return 0; }
        }

        //The list of two or more items with the most text in it, anywhere in the result (the notes excepted). A list of
        //one item is looked into instead: cutting it would leave nothing
        private static (JArray array, string path, int size) Biggest(JToken token)
        {
            (JArray array, string path, int size) found = (null, null, 0);
            Visit(token, "", ref found);
            return found;
        }

        private static int Visit(JToken token, string path, ref (JArray array, string path, int size) found)
        {
            if (token is JObject obj)
            {
                int size = 2;
                foreach (JProperty property in obj.Properties())
                {
                    if (path.Length == 0 && (property.Name == "notes" || property.Name == "trimmed" || property.Name == "dialogs_dismissed"))
                    {
                        size += property.Value.ToString(Formatting.None).Length + property.Name.Length + 4;
                        continue;
                    }
                    size += Visit(property.Value, path.Length == 0 ? property.Name : path + "." + property.Name, ref found) + property.Name.Length + 4;
                }
                return size;
            }
            if (token is JArray array)
            {
                int size = 2;
                for (int i = 0; i < array.Count; i++)
                    size += Visit(array[i], path + "[" + i + "]", ref found) + 1;
                if (array.Count >= 2 && size > found.size)
                    found = (array, path, size);
                return size;
            }
            return token.ToString(Formatting.None).Length;
        }

        private static JValue Longest(JToken token)
        {
            IEnumerable<JValue> values = token is JContainer container ? container.Descendants().OfType<JValue>() : token is JValue single ? new[] { single } : Enumerable.Empty<JValue>();
            JValue longest = null;
            foreach (JValue value in values)
                if (value.Type == JTokenType.String && (longest == null || ((string)value.Value).Length > ((string)longest.Value).Length))
                    longest = value;
            return longest;
        }
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
                        //Each family's tools are offered in its toolset unless a tool names its own (McpToolsets)
                        _tools = new List<McpTool>();
                        _tools.AddRange(McpToolsets.Tag("core", McpEditorTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("core", McpBrowseTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("script", McpScriptTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("flowgraph", McpFlowgraphTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("porting", McpPortingTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("assets", McpAssetTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("viewport", McpViewportTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("assets", McpImportTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("script", McpEditingTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("zones", McpZoneTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("flowgraph", McpPageTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("animation", McpCageAnimationTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("assets", McpResourceTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("assets", McpMaterialTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("animation", McpAnimationAssetTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("gamedata", McpGameDataTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("game", McpLevelTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("world", McpSpatialTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("world", McpNavTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("core", McpPlatformTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("sound", McpSoundTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("cameras", McpCameraTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("characters", McpCharacterTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("lights", McpLightTools.Tools()));
                        _tools.AddRange(McpToolsets.Tag("script", McpUsageTools.Tools()));
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
                        ApplyPolicy(_byName);
                        McpPlatformTools.Decorate(_byName);
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

        /* Corrections to tools defined elsewhere, kept here so the whole list follows one rule (see McpTool's remarks).
           Once a tool's own definition is fixed, its entry here can go. */

        //Destructive: deletes, or writes over something one undo step cannot bring back
        private static readonly string[] AlsoDestructive =
        {
            //'overwrite' replaces files already there, as export_collision_mesh, export_composite_package and export_region_collision (marked) do
            "export_model", "export_textures", "export_animations", "export_sound",
            //'remove' deletes a zone's sequence entries, as set_trigger_sequence (marked) can
            "set_zone_contents",
            //'replace_door_link' takes a door's link away from the ZoneLink it drove, as remove_links (marked) does
            "create_zone_link",
        };

        //Not read-only after all: they change game or level state
        private static readonly string[] NotReadOnly = { };

        //Answered while another call runs (quick status reads, no queue)
        private static readonly string[] AlsoConcurrent = { };

        //Older argument names still accepted, no longer listed to clients
        private static readonly string[] DeprecatedArguments =
        {
            "link_zones.exclusions",                //open_pairs
        };

        private static void ApplyPolicy(Dictionary<string, McpTool> byName)
        {
            foreach (string name in AlsoDestructive)
                if (byName.TryGetValue(name, out McpTool tool) && !tool.ReadOnly)
                    tool.Destructive = true;
            foreach (string name in NotReadOnly)
                if (byName.TryGetValue(name, out McpTool tool))
                    tool.ReadOnly = false;
            foreach (string name in AlsoConcurrent)
                if (byName.TryGetValue(name, out McpTool tool) && tool.ReadOnly)
                    tool.Concurrent = true;
            foreach (string entry in DeprecatedArguments)
            {
                string[] parts = entry.Split('.');
                if (byName.TryGetValue(parts[0], out McpTool tool) && tool.InputSchema?["properties"]?[parts[1]] is JObject argument)
                    argument["deprecated"] = true;
            }
        }

        /* The server's instructions, sent to every client at the handshake. This is the one copy: the bridge (OpenCAGE.MCP)
           serves it from the tool cache McpServer writes, or reads this const out of OpenCAGE.exe, and carries none of its own.
           One const per section, each kept to what a caller cannot learn from the tools' descriptions: a compact map of the
           tools by task plus the conventions they share. Keep the whole well under 12,000 characters (clients load it into
           every conversation); detail belongs in the tool descriptions. */

        public const string Instructions =
            InstructionsIntro + InstructionsConventions + InstructionsScript + InstructionsSaving + InstructionsTasks +
            InstructionsTaskScript + InstructionsPlaces + InstructionsViewport + InstructionsCinematics + InstructionsCharacters + InstructionsLights +
            InstructionsSound + InstructionsZones + InstructionsAssets + InstructionsLevels + InstructionsSafety;

        private const string InstructionsIntro =
            "OpenCAGE is the level editor for Alien: Isolation. These tools drive the OpenCAGE window the user has open (it is started on first use), so every " +
            "change shows there as you make it. Script changes are steps on the open level's undo history and reach the game only when saved; a tool's description " +
            "says when it writes files straight away instead. Call get_editor_state first.\n\n";

        private const string InstructionsConventions =
            "Conventions: positions are [x, y, z] metres, Y up. Rotations are [x, y, z] degrees: x = pitch (positive looks down), y = yaw, z = roll, applied yaw, " +
            "then pitch, then roll; an entity faces its local +Z, so to face direction d use [-asin(d.y/|d|), atan2(d.x, d.z), 0]. Colours are 0-255 per channel " +
            "unless the parameter says 0-1. A position you write is relative to the composite holding the entity unless the tool takes space: 'world' (add " +
            "placement: n, or a path, when that composite is placed more than once); world-space results say \"space\": \"world\", and transform_points converts. " +
            "Composites are named by path ('AYZ\\Doors\\Door_SML', either slash, or a unique last part; 'root' is the level's root composite) or id; entities by id " +
            "or name (case, spaces and '_' ignored). A path through instances is an array of ids/names, a string split on '/', or any {path, ids} a result gives: " +
            "pass those back as they are. A 'region' is {name: 'canteen'} (a room, zone, composite or model, as find_places ranks them), {zone}, {composite, " +
            "placement}, {path}, {box_min, box_max}, {near, radius} or 'level'. Errors start with a code such as [not_found] (near names suggested), " +
            "[ambiguous] (candidates with ids), [invalid_argument], [busy] (try again shortly) or [level_changed]. Lists take limit/offset and give total and " +
            "next_offset; a result too big to return is trimmed ('trimmed' says what was left out).\n\n";

        private const string InstructionsScript =
            "The script: a level is a tree of composites. The root composite places instances of composites; a composite holds functions (built-in types such as " +
            "ModelReference, Character, PlayerTriggerBox, LogicGate: list_function_types, describe_function_type), instances, variables (its own pins, seen as " +
            "parameters and link points on its instances), aliases (overrides reaching into a nested instance) and proxies (an entity elsewhere in the level). A " +
            "link is held by its source and joins entities of one composite. Events: relay -> method (ThinkOnce.on_think -> Sound.start); a number on a method pin " +
            "is its delay in seconds. Data: the entity that uses a value links from its own pin to what provides it (Checkpoint.player_spawn_position -> " +
            "PositionMarker.reference), so a value parameter with a link out of it takes its value from the other end and setting it changes nothing in game " +
            "(describe_entity lists parameters_fed_by: set the source, or the composite pin on the instance). from_path/to_path link into a nested instance (an " +
            "alias is found or made), from_root/to_root to anything in the level (a proxy). A composite placed more than once changes in every placement; for one " +
            "placement, set_parameters with 'path' writes an alias override. get_usage_patterns shows how the game's own scripts use a type or composite: wire " +
            "things the way retail does. trace_links and get_effective_parameters explain what drives an entity and the values it really gets.\n\n";

        private const string InstructionsSaving =
            "Saving and playing: a plain save_level writes the script and assets, enough for logic (links, triggers, logic parameters, camera CAGEAnimations). " +
            "Placing, moving or deleting models, lights, collision, physics, zones, navigation, cover or sound entities (or composites holding them) reaches the " +
            "game only with save_level build=true (Save & Build); get_editor_state's level.build says whether one is needed and why, and build: 'auto' builds only " +
            "then. Saving closes a running game. launch_game refuses a level needing a build (save: 'auto' does it first; skip_frontend starts it playing); " +
            "runtime_utils works on the running game over Live Link (push script edits live, call_method, activity, screenshot). Ask before saving a level the " +
            "user has not asked you to save.\n\n";

        //The map: one line per family of tools, each saying what its descriptions cannot (how the family's tools chain, game rules)
        private const string InstructionsTasks = "Tools by task:\n";

        private const string InstructionsTaskScript =
            "- Script: find_composites, find_entities, get_composite, describe_entity; create_entities places and wires in one call; set_parameters, add_links, " +
            "delete_entities (also removes what reaches what it deletes), rename_pin; script_batch makes several edits one all-or-nothing step. Flowgraph pages " +
            "stay drawn by themselves.\n";

        //Spatial questions: finding a room by name, its size and collision, where things are in the world
        private const string InstructionsPlaces =
            "- Rooms and the world: for 'the room called X' use find_places, then pass a candidate's region to get_bounds (size, floor, ceiling), sample_space " +
            "(a camera loop round the room with keys ready for animate_parameters, a free centre, standing points) or raycast (the floor under points, walls, line " +
            "of sight). They read the collision as it is in the editor, unsaved edits included, with no viewport. drop_to_floor puts entities on the floor; " +
            "get_placements lists where a composite is placed. A box volume (PlayerTriggerBox, CollisionBarrier) stands on its position: +-half_dimensions in x " +
            "and z, 0 to 2*half_dimensions.y up, in its own rotation. query_navmesh reads the navmesh of the last Save & Build.\n";

        private const string InstructionsViewport =
            "- Viewport (when on): capture_viewport takes a picture ('camera' places the camera first); pick_in_viewport says what a point of it shows; get_viewport_state 'camera' says where the " +
            "user is looking (looking_at: the surface in the middle and the instances it is placed through, i.e. the room); set_viewport_camera moves it or looks " +
            "through an entity. Its positions are world space while the root composite is on screen. It draws no lighting and does not show streaming.\n";

        //Scripted cameras and CAGEAnimations
        private const string InstructionsCinematics =
            "- Cameras: a scripted camera move is a CameraResource whose position a CAGEAnimation keys, switched to through activate_camera and back with " +
            "deactivate_camera. create_camera_animation builds the whole move as one undo step (camera, animation, start trigger, end: return, loop or hold) " +
            "round a room's region or through points, keeps it off collision and reports path_check and will_it_play; animate_camera_path re-keys a path; " +
            "check_cage_animation says whether an animation will play; preview_cage_animation {view: 'camera', times} shows what the player sees. " +
            "animate_parameters keys any entity (its retime shifts, scales or loops a whole animation). Retail animates positions and VariableFloat " +
            "initial_value: other float parameters may not change in game. ANIMATION.PAK clips (list_animations) are skeletal animations; set_animation_events' " +
            "camera binding is only for CameraPlayAnimation's baked clips.\n";

        //NPCs
        private const string InstructionsCharacters =
            "- NPCs: an NPC is an instance of an archetype under Archetypes\\NPCs (Android\\Android_NPC, Alien\\Xenomorph_NPC...) wrapping a Character: " +
            "class, alliance, behaviour tree, look. spawn_npc places one on the floor (the error names a level to " +
            "port the archetype from); describe_npc and set_npc read and change what the game uses for one NPC (alliance_group PLAYER_ALLY is friendly). Commands " +
            "(CMD_GoTo, CMD_Idle, CMD_PlayAnimation) act on the character of the trigger that starts them: retail binds one with a TriggerBindCharacter and " +
            "chains commands on their own events, as create_npc_route and play_character_animation do. Whether a level loads their sound banks is unverified.\n";

        //Lights
        private const string InstructionsLights =
            "- Lights: a LightReference usually sits in a fixture composite whose pins feed it by links, so its own parameters often do nothing. list_lights (a " +
            "region) gives each light's real colour, intensity, range, cone and behaviour and what controls it; set_lights changes them where the game reads " +
            "them (those placements, or the fixture everywhere with scope 'shared'), flicker included; add_light adds one at a world point. Lighting reaches the " +
            "game only at Save & Build.\n";

        //Skeletal animation and sound
        private const string InstructionsSound =
            "- Animation and sound: CMD_PlayAnimation {AnimationSet, Animation} plays a clip on a character (get_character_animation_profile gives an NPC's set; " +
            "preview_animation and check_animations help); a prop plays its environment set through PlayEnvironmentAnimation (describe_animated_prop); an " +
            "unrigged prop moves with a CAGEAnimation. import_animation takes FBX/glTF (Mixamo and Unreal mannequin retargeted). Sound events are named values " +
            "(list_enum_string_values); describe_sound_event says whether its bank loads in this level; place_sound adds one.\n";

        private const string InstructionsZones =
            "- Zones decide what streams in. They live in the composite holding the level's geometry (a retail level's ENVIRONMENT_... composite, the root of a " +
            "level made from scratch) and claim only what is placed in it (move_into_composite moves root content in), through Zone.composites -> " +
            "TriggerSequence.reference. A ZoneLink (ZoneA/ZoneB -> each Zone's reference pin) joins two zones, driven by the door between them (door.zonelink -> " +
            "ZoneLink.reference) or open on reset for an archway or a door with a window; a ZoneExclusionLink is always open. Unzoned content or a missing or " +
            "closed link makes rooms pop in. analyse_zones shows what is missing; auto_zone and link_zones (dry run first) zone a whole level; create_zone, " +
            "set_zone_contents, create_zone_link (door 'auto'), merge_zones and delete_zone change one; check_zones (near: 'viewport' for where the user " +
            "looks) and get_zone_links diagnose, which pictures cannot. Zones reach the game only at Save & Build.\n";

        private const string InstructionsAssets =
            "- Assets: get_entity_resources and set_renderable / set_collision / set_physics_system change what an entity draws and collides with; place_model " +
            "places a model with the collision its submeshes name; find_asset_users follows a texture, material or model to its users. An edit inside a " +
            "composite changes every placement (placements_affected); for one " +
            "placement, set a material mapping on the instance placing the models. Texture, material, mapping and model material edits are undo steps; geometry " +
            "changes and Havok imports are not.\n";

        //Other levels, whole levels and backups
        private const string InstructionsLevels =
            "- Other levels: search_level and describe_level_composite read a level without opening it; find_in_levels says which levels have a composite or " +
            "type; port_composites brings composites with their models, materials, textures and collision. duplicate_level copies a level, lighting and navmesh " +
            "included; delete_level removes a custom level (backed up first); diff_level shows what changed. create_backup before risky file work; " +
            "restore_backup can restore single files.\n\n";

        private const string InstructionsSafety =
            "Working safely: undo and redo refuse a step the user made unless force=true, and 'expect' names the step you mean; get_undo_history lists the steps, " +
            "who made them, and your changes that left none. undo_group begin ... end makes a task of several calls one undo step. One tool runs at a time; " +
            "get_activity answers at once with what is running and recently done. A call that timed out on your side may still have finished: check " +
            "get_activity, and repeating the very same call returns its result rather than doing it twice. If the user opens another level between your " +
            "calls, your next change is refused once with [level_changed]. Files every level shares (configuration records, text strings, ANIMATION.PAK, sound " +
            "banks, UI.PAK) change at once and cannot be undone: dry_run first.";
    }

    /// <summary>
    /// Toolsets: groups of tools a client can be offered alone, keeping the tool list it loads small (the bridge's
    /// --toolsets core,script,...; it offers the rest through a load_toolsets tool). Every tool stays callable by
    /// name whatever was offered. A tool's toolset comes from its file's family (<see cref="McpTools"/>), the
    /// exceptions below, or its own <see cref="McpTool.Toolset"/>.
    /// </summary>
    internal static class McpToolsets
    {
        /// <summary>Where a tool's toolset is given in its description (the bridge filters on it, and strips it).</summary>
        public const string MetaKey = "opencage/toolset";

        public static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>()
        {
            ["core"] = "editor state, levels, saving, undo and activity; finding and describing composites, entities and function types",
            ["script"] = "creating, wiring, changing and deleting entities and composites",
            ["flowgraph"] = "flowgraph pages and nodes",
            ["zones"] = "streaming zones, their links and doors",
            ["viewport"] = "the 3D viewport: pictures, view settings, camera",
            ["assets"] = "models, textures, materials, collision, physics and entity resources, and importing them",
            ["porting"] = "bringing composites in from other levels and packages",
            ["animation"] = "CAGEAnimations, ANIMATION.PAK clips, anim trees and blend sets",
            ["gamedata"] = "files every level shares: configuration records, text strings, behaviour trees, UI.PAK",
            ["game"] = "launching the game, Live Link, backups, game and editor options",
            ["world"] = "world-space questions: collision, bounds, navigation",
            ["cameras"] = "scripted cameras",
            ["characters"] = "NPCs and their appearance",
            ["lights"] = "lights",
            ["sound"] = "sound events and banks",
        };

        //Tools whose file's family is not their best fit
        private static readonly Dictionary<string, string> Exceptions = new Dictionary<string, string>()
        {
            ["launch_game"] = "game",
            ["list_backups"] = "game",
            ["create_backup"] = "game",
            ["restore_backup"] = "game",
            ["delete_backups"] = "game",
            ["get_placements"] = "core",
            ["list_enum_string_values"] = "core",
            ["get_zones"] = "zones",
            ["export_composite_package"] = "porting",
            ["inspect_composite_package"] = "porting",
            ["import_composite_package"] = "porting",
            ["list_config_files"] = "gamedata",
            ["read_config"] = "gamedata",
            ["write_config"] = "gamedata",
            ["list_ui_files"] = "gamedata",
            ["edit_ui_pak"] = "gamedata",
            ["get_character_appearance"] = "characters",
            ["set_character_appearance"] = "characters",
            ["check_character_appearances"] = "characters",
            ["get_behaviour_tree"] = "gamedata",
            ["edit_behaviour_tree"] = "gamedata",
            ["list_sound_banks"] = "sound",
            ["describe_sound_event"] = "sound",
            ["export_sound"] = "sound",
            ["replace_sound"] = "sound",
        };

        /// <summary>Gives each tool without a toolset of its own <paramref name="toolset"/> (or its exception).</summary>
        public static IEnumerable<McpTool> Tag(string toolset, IEnumerable<McpTool> tools)
        {
            foreach (McpTool tool in tools)
            {
                if (tool.Toolset == null)
                    tool.Toolset = Exceptions.TryGetValue(tool.Name, out string other) ? other : toolset;
                yield return tool;
            }
        }

        /// <summary>Every toolset: what it covers and its tools (for the bridge's load_toolsets).</summary>
        public static JObject Describe()
        {
            JObject sets = new JObject();
            foreach (IGrouping<string, McpTool> group in McpTools.All.GroupBy(o => o.Toolset ?? "core"))
            {
                sets[group.Key] = new JObject()
                {
                    ["description"] = Descriptions.TryGetValue(group.Key, out string text) ? text : group.Key,
                    ["tools"] = new JArray(group.Select(o => o.Name)),
                };
            }
            return sets;
        }
    }
}
