using CATHODE;
using CATHODE.Enums;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// How function types and archetypes are really used: the patterns the game's own scripts wire them in (counted over a
    /// level's whole script, the open one live or others read from disk and kept), and tracing what drives an entity, or what
    /// it drives, across composite boundaries.
    /// </summary>
    internal static class McpUsageTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "get_usage_patterns",
                Title = "Get usage patterns",
                Description = "How the game's own scripts use a function type or a composite (an archetype such as a door, a light fixture or a save terminal): how many there are, the parameters most often set with their typical values, " +
                    "the commonest links in ('SrcType.pin -> pin': events that trigger it, or entities that read a value from it) and out ('pin -> DstType.pin': events it fires, or where a value parameter of it reads from), each with an example, and a few whole examples with their parameter values and links. " +
                    "In patterns a composite instance is 'Instance:<name>', a composite's own pin is 'Pin', and an alias or proxy is 'Alias:<type>' / 'Proxy:<type>' (counted with the type it stands for). " +
                    "level: 'open' (default: the open level as it is now, unsaved edits included), a level as list_levels names it (its saved script, read without opening it), a list of levels, 'retail' (the game's own levels) or 'all' (every level on disk); the first run reads each level's script (half a minute for them all) and is kept after. " +
                    "An example is {composite, id, name} (+ level): describe_entity reads it (with that 'level' when it is another level's), and find_references shows what refers to it. " +
                    "Look before building something the game already does - a fire-once trigger, a checkpoint, a scripted camera, a lift - and wire it the same way.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("type", "A function type (e.g. 'PlayerTriggerBox', 'CameraResource') or a composite path or name (e.g. 'Save_Terminal').", required: true),
                    McpSchema.Any("level", "'open' (default), a level (e.g. 'PRODUCTION/SCI_HOSPITALUPPER'), a list of levels, 'retail' or 'all'."),
                    McpSchema.String("pin", "Only the links through this pin (at either end), and only this parameter."),
                    McpSchema.Integer("examples", "Whole examples to show, with their parameters and links (default 2, at most 8; 0 for none)."),
                    McpSchema.Integer("limit", "Most patterns per list, and parameters (default 12, at most 50).")),
                ReadOnly = true,
                Idempotent = true,
                Run = GetUsagePatterns,
            };

            yield return new McpTool()
            {
                Name = "trace_links",
                Title = "Trace links",
                Description = "What drives an entity, or what it drives, across composite boundaries: 'up' (default) follows links into it back to their sources - through its composite's pins to the instances outside, " +
                    "through aliases and proxies pointing at it, TriggerSequences listing it and CAGEAnimations animating it; 'down' follows its events forward - into instances' pins, out through its composite's pins, through aliases to what they stand for, into a TriggerSequence's entries. " +
                    "Each hop has its composite and the ids at both ends (describe_entity takes them); 'chains' gives one line per root cause (up) or end (down). Give 'path' (a placement from the root) to cross only into that placement's parents; otherwise every placement's are followed. " +
                    "Data links are not events: a link out of a value parameter is its owner reading a value, so 'up' does not follow it (with 'pin', the entities reading that pin are listed as 'read_by' hops); a parameter fed by a link is shown by describe_entity's parameters_fed_by.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the entity is in (path or id).", required: true),
                    McpSchema.String("entity", "The entity (id or name).", required: true),
                    McpSchema.String("pin", "Only links through this pin of it (e.g. 'start' up, 'on_entered' down)."),
                    McpSchema.String("direction", "'up' (default): what drives it; 'down': what it drives.", options: new[] { "up", "down" }),
                    McpSchema.Integer("depth", "How many hops to follow (default 6, at most 20)."),
                    McpSchema.Any("path", "One placement of the entity, as entity ids/names from the root composite: crossing out of a composite then goes only to this placement's parents."),
                    McpSchema.Integer("limit", "Most hops to report (default 100, at most 500).")),
                ReadOnly = true,
                Idempotent = true,
                Toolset = "core",
                Run = TraceLinks,
            };

            yield return new McpTool()
            {
                Name = "find_links",
                Title = "Find links",
                Description = "Search the level's links: everything calling a method ('to_pin': 'light_switch_off'), everything a type fires ('from_type': 'PlayerTriggerBox'), links between two types, or by entity name. " +
                    "Types match like find_entities' (a function type, part of one, or text in an instance's composite path); 'Pin' matches a composite's own pins. Each link gives its composite and both ends with ids.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("from_type", "The source's type (e.g. 'PlayerTriggerBox', 'LogicGate', 'Pin')."),
                    McpSchema.String("from_pin", "The source pin (e.g. 'on_entered')."),
                    McpSchema.String("from_name", "Text the source's name contains."),
                    McpSchema.String("to_type", "The target's type."),
                    McpSchema.String("to_pin", "The target pin (e.g. 'start', 'light_switch_off')."),
                    McpSchema.String("to_name", "Text the target's name contains."),
                    McpSchema.String("composite", "Only this composite (path or id)."),
                    McpSchema.String("within", "Only this composite and those placed under it ('root' for what the level places)."),
                    McpSchema.Limit(100, "links"),
                    McpSchema.Offset("links")),
                ReadOnly = true,
                Idempotent = true,
                Toolset = "core",
                Run = FindLinks,
            };
        }

        #region Usage index
        private const int MaxValues = 30, MaxNumbers = 400, MaxSamples = 6, MaxPatternExamples = 2;

        /// <summary>How one level's script uses each function type and composite: counted once over every entity and link.</summary>
        internal sealed class UsageIndex
        {
            public const int FormatVersion = 1;
            public int Format = FormatVersion;
            public string Level;
            /// <summary>The script file's write time (UTC ticks) and size, for a level read from disk; 0 for the open level.</summary>
            public long Written, Length;
            /// <summary>By function type name, or "composite:" + a composite's path.</summary>
            public Dictionary<string, TypeUsage> Types = new Dictionary<string, TypeUsage>(StringComparer.OrdinalIgnoreCase);

            public TypeUsage Get(string key)
            {
                if (!Types.TryGetValue(key, out TypeUsage usage))
                    Types[key] = usage = new TypeUsage();
                return usage;
            }
        }

        internal sealed class TypeUsage
        {
            public int Count;
            public Dictionary<string, ParameterUsage> Parameters = new Dictionary<string, ParameterUsage>();
            /// <summary>Parameters aliases of it override, by how many aliases do.</summary>
            public Dictionary<string, int> AliasOverrides = new Dictionary<string, int>();
            public Dictionary<string, PatternUsage> In = new Dictionary<string, PatternUsage>();
            public Dictionary<string, PatternUsage> Out = new Dictionary<string, PatternUsage>();
            public List<Ref> Samples = new List<Ref>();
        }

        internal sealed class ParameterUsage
        {
            public int Count;
            /// <summary>Values as compact JSON, by how often each is set; past MaxValues distinct ones are counted in Others.</summary>
            public Dictionary<string, int> Values = new Dictionary<string, int>();
            public int Others;
            public List<double> Numbers = new List<double>();
        }

        internal sealed class PatternUsage
        {
            public int Count;
            public List<Ref> Examples = new List<Ref>();
        }

        /// <summary>An entity of a level's script, by its composite and id.</summary>
        internal sealed class Ref
        {
            public string Level, Composite, CompositeId, Id, Name;

            public JObject Json(bool withLevel)
            {
                JObject json = new JObject();
                if (withLevel) json["level"] = Level;
                json["composite"] = Composite;
                json["id"] = Id;
                json["name"] = Name;
                return json;
            }
        }

        private static readonly object _lock = new object();
        private static bool _hooked;
        private static UsageIndex _open;
        //The open level's script the index was counted from: dropped when the level closes, so it never keeps a closed level alive
        private static Commands _openFor;
        private static int _openVersion = -1;
        private static readonly Dictionary<string, UsageIndex> _levels = new Dictionary<string, UsageIndex>(StringComparer.OrdinalIgnoreCase);
        //The last other level's script, kept for drawing whole examples from it
        private static string _loadedLevel;
        private static long _loadedWritten;
        private static Commands _loaded;

        /// <summary>
        /// The usage key and the pattern label of an entity: a function type by name; an instance as "composite:path" /
        /// "Instance:leaf"; a composite pin as (none) / "Pin"; an alias or proxy as what it stands for, labelled "Alias:..." / "Proxy:...".
        /// </summary>
        private sealed class Classifier
        {
            private readonly Commands _commands;
            private readonly Dictionary<Entity, (string key, string label)> _known = new Dictionary<Entity, (string, string)>();

            public Classifier(Commands commands) { _commands = commands; }

            public (string key, string label) Of(Composite composite, Entity entity, int depth = 0)
            {
                if (_known.TryGetValue(entity, out (string, string) known)) return known;
                (string key, string label) result;
                switch (entity)
                {
                    case FunctionEntity function when function.function.IsFunctionType:
                        result = (function.function.AsFunctionType.ToString(), function.function.AsFunctionType.ToString());
                        break;
                    case FunctionEntity instance:
                        {
                            Composite child = _commands.GetComposite(instance.function);
                            result = child == null ? ((string)null, "Instance:?") : (CompositeKey(child), "Instance:" + McpScript.CompositeLeaf(child));
                        }
                        break;
                    case VariableEntity _:
                        result = (null, "Pin");
                        break;
                    case AliasEntity alias:
                        result = Pointer(alias, "Alias:", () => _commands.Utils.ResolveAlias(alias, composite), depth);
                        break;
                    case ProxyEntity proxy:
                        result = Pointer(proxy, "Proxy:", () => _commands.Utils.ResolveProxy(proxy), depth);
                        break;
                    default:
                        result = (null, entity.variant.ToString());
                        break;
                }
                _known[entity] = result;
                return result;
            }

            private (string, string) Pointer(Entity pointer, string prefix, Func<List<Tuple<Composite, Entity>>> resolve, int depth)
            {
                (Composite composite, Entity entity) target = (null, null);
                try { target = _commands.Utils.GetResolvedTarget(resolve()); } catch { }
                if (target.entity == null || depth > 8 || target.entity == pointer) return (null, prefix + "?");
                (string key, string label) inner = Of(target.composite, target.entity, depth + 1);
                return (inner.key, prefix + inner.label);
            }
        }

        public static string CompositeKey(Composite composite) => "composite:" + McpScript.NormalisePath(composite.name);

        private static void Increment(Dictionary<string, int> counts, string key) => counts[key] = (counts.TryGetValue(key, out int n) ? n : 0) + 1;

        private static Ref RefOf(string level, Commands commands, Composite composite, Entity entity) => new Ref()
        {
            Level = level,
            Composite = composite.name,
            CompositeId = McpScript.Id(composite.shortGUID),
            Id = McpScript.Id(entity.shortGUID),
            Name = McpScript.EntityName(commands, composite, entity),
        };

        private static void AddPattern(Dictionary<string, PatternUsage> patterns, string pattern, string level, Commands commands, Composite composite, Entity entity)
        {
            if (!patterns.TryGetValue(pattern, out PatternUsage usage))
                patterns[pattern] = usage = new PatternUsage();
            usage.Count++;
            if (usage.Examples.Count < MaxPatternExamples && !usage.Examples.Any(o => o.CompositeId == McpScript.Id(composite.shortGUID)))
                usage.Examples.Add(RefOf(level, commands, composite, entity));
        }

        private static void CountParameter(TypeUsage usage, Parameter parameter, Commands commands)
        {
            if (parameter.name == ShortGuids.name || parameter.name == ShortGuids.position) return;
            string name = McpScript.ParamName(parameter.name);
            if (!usage.Parameters.TryGetValue(name, out ParameterUsage counted))
                usage.Parameters[name] = counted = new ParameterUsage();
            counted.Count++;
            switch (parameter.content)
            {
                case cResource _:
                case cTransform _:
                case cSpline _:
                case null:
                    return;
                case cFloat f:
                    if (counted.Numbers.Count < MaxNumbers) counted.Numbers.Add(f.value);
                    break;
                case cInteger i:
                    if (counted.Numbers.Count < MaxNumbers) counted.Numbers.Add(i.value);
                    break;
            }
            string value = McpValues.ToJson(parameter.content, commands).ToString(Formatting.None);
            if (counted.Values.ContainsKey(value) || counted.Values.Count < MaxValues)
                Increment(counted.Values, value);
            else
                counted.Others++;
        }

        /// <summary>Count how a script uses every type and composite: one pass over its entities and links.</summary>
        internal static UsageIndex Build(Commands commands, string level, CancellationToken cancel)
        {
            UsageIndex index = new UsageIndex() { Level = level };
            Classifier classifier = new Classifier(commands);
            int done = 0;
            foreach (Composite composite in commands.Entries)
            {
                if (composite == null) continue;
                if ((++done & 31) == 0) cancel.ThrowIfCancellationRequested();
                foreach (Entity entity in composite.GetEntities())
                {
                    (string key, string label) = classifier.Of(composite, entity);
                    if (key != null && entity is FunctionEntity)
                    {
                        TypeUsage usage = index.Get(key);
                        usage.Count++;
                        if (usage.Samples.Count < MaxSamples && !usage.Samples.Any(o => o.CompositeId == McpScript.Id(composite.shortGUID)))
                            usage.Samples.Add(RefOf(level, commands, composite, entity));
                        foreach (Parameter parameter in entity.parameters)
                            CountParameter(usage, parameter, commands);
                    }
                    else if (key != null && entity is AliasEntity alias)
                    {
                        TypeUsage usage = index.Get(key);
                        foreach (Parameter parameter in alias.parameters)
                            if (parameter.name != ShortGuids.name)
                                Increment(usage.AliasOverrides, McpScript.ParamName(parameter.name));
                    }
                    foreach (EntityConnector link in entity.childLinks)
                    {
                        Entity target = composite.GetEntityByID(link.linkedEntityID);
                        if (target == null) continue;
                        (string targetKey, string targetLabel) = classifier.Of(composite, target);
                        string from = McpScript.ParamName(link.thisParamID), to = McpScript.ParamName(link.linkedParamID);
                        if (key != null)
                            AddPattern(index.Get(key).Out, from + " -> " + targetLabel + "." + to, level, commands, composite, entity);
                        if (targetKey != null)
                            AddPattern(index.Get(targetKey).In, label + "." + from + " -> " + to, level, commands, composite, target);
                    }
                }
            }
            return index;
        }

        /// <summary>The open level's index, from the live script (unsaved edits included); rebuilt when the script changes. UI thread.</summary>
        internal static UsageIndex OpenIndex(McpCall call, Commands commands, string level)
        {
            McpCollision.Hook();
            Hook();
            int version = McpCollision.Version;
            lock (_lock)
            {
                if (_open != null && _openFor == commands && _openVersion == version)
                    return _open;
            }
            UsageIndex built = Build(commands, level, call?.Cancel ?? default(CancellationToken));
            lock (_lock)
            {
                _open = built;
                _openFor = commands;
                _openVersion = version;
            }
            return built;
        }

        /// <summary>Forget the open level's index when the level closes (listening once). UI thread.</summary>
        private static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            Singleton.OnLevelClosing += _ =>
            {
                lock (_lock)
                {
                    _open = null;
                    _openFor = null;
                    _openVersion = -1;
                }
            };
        }

        private static string NormaliseLevel(string level) => (level ?? "").Replace('\\', '/').Trim().Trim('/').ToUpperInvariant();

        /// <summary>The levels on disk, as list_levels names them.</summary>
        private static List<string> Levels() => Level.GetLevels(Singleton.PathToAI).Select(NormaliseLevel).Distinct().OrderBy(o => o).ToList();

        private static string CommandsPathOf(string level, List<string> levels)
        {
            if (!levels.Contains(level))
                throw McpError.NotFound("level", level, levels, "list_levels lists them.");
            return new Level(Singleton.PathToAI + "/DATA/ENV/" + level, Singleton.Global, false).CommandsFilepath;
        }

        private static string DiskCache(string level) =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenCAGE", "mcp_usage", "v" + UsageIndex.FormatVersion, level.Replace('/', '_') + ".json");

        /// <summary>A level's index from its saved script: kept in memory, and on disk beside OpenCAGE's other caches, until the file changes.</summary>
        internal static UsageIndex LevelIndex(McpCall call, string level, List<string> levels)
        {
            string path = CommandsPathOf(level, levels);
            FileInfo file = new FileInfo(path);
            if (!file.Exists)
                throw new McpError(level + " has no script file (" + path + ").");
            long written = file.LastWriteTimeUtc.Ticks, length = file.Length;
            lock (_lock)
            {
                if (_levels.TryGetValue(level, out UsageIndex cached) && cached.Written == written && cached.Length == length)
                    return cached;
            }

            UsageIndex index = null;
            string cache = DiskCache(level);
            try
            {
                if (File.Exists(cache))
                {
                    UsageIndex stored = JsonConvert.DeserializeObject<UsageIndex>(File.ReadAllText(cache));
                    if (stored != null && stored.Format == UsageIndex.FormatVersion && stored.Written == written && stored.Length == length)
                        index = stored;
                }
            }
            catch (Exception e)
            {
                Debug.Log("MCP", "Could not read the usage cache " + cache + ": " + e.Message);
            }

            if (index == null)
            {
                call.Progress("Reading " + level + "'s script");
                Commands commands = LoadCommands(level, path, written);
                index = Build(commands, level, call.Cancel);
                index.Written = written;
                index.Length = length;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(cache));
                    File.WriteAllText(cache, JsonConvert.SerializeObject(index));
                }
                catch (Exception e)
                {
                    Debug.Log("MCP", "Could not write the usage cache " + cache + ": " + e.Message);
                }
            }
            lock (_lock)
                _levels[level] = index;
            return index;
        }

        /// <summary>A level's script alone (no models, textures or collision), read from disk. Not the open level: that one is live.</summary>
        private static Commands LoadCommands(string level, string path, long written)
        {
            lock (_lock)
            {
                if (_loaded != null && _loadedLevel == level && _loadedWritten == written)
                    return _loaded;
            }
            Commands commands = McpPortingTools.ReadScript(path);
            lock (_lock)
            {
                _loaded = commands;
                _loadedLevel = level;
                _loadedWritten = written;
            }
            return commands;
        }

        /// <summary>Which levels a 'level' argument names: null for the open one alone.</summary>
        private static List<string> ReadLevels(McpCall call, string open, List<string> levels)
        {
            JToken token = call.Token("level");
            if (token == null) return null;
            List<string> wanted = token is JArray array ? array.Select(o => McpValues.ReadString(o)).ToList() : new List<string>() { McpValues.ReadString(token) };
            if (wanted.Count == 1 && string.Equals(wanted[0].Trim(), "open", StringComparison.OrdinalIgnoreCase))
                return null;
            if (wanted.Count == 1 && string.Equals(wanted[0].Trim(), "all", StringComparison.OrdinalIgnoreCase))
                return levels;
            //The game's own levels: not ones made with OpenCAGE beside them
            if (wanted.Count == 1 && string.Equals(wanted[0].Trim(), "retail", StringComparison.OrdinalIgnoreCase))
                return levels.Where(o => o.StartsWith("PRODUCTION/", StringComparison.OrdinalIgnoreCase)).ToList();
            List<string> named = new List<string>();
            foreach (string text in wanted)
            {
                string level = NormaliseLevel(text);
                if (level == "OPEN") level = open ?? level;
                if (!levels.Contains(level))
                {
                    //'BSP_TORRENS' for 'PRODUCTION/BSP_TORRENS'
                    List<string> ending = levels.Where(o => o.EndsWith("/" + level)).ToList();
                    if (ending.Count == 1) level = ending[0];
                    else throw McpError.NotFound("level", text, levels, "list_levels lists them; 'open' is the open level, 'retail' the game's own levels and 'all' every level.");
                }
                if (!named.Contains(level)) named.Add(level);
            }
            return named;
        }
        #endregion

        #region get_usage_patterns
        private static object GetUsagePatterns(McpCall call)
        {
            string type = call.Str("type", required: true).Trim();
            string pin = call.Str("pin")?.Trim();
            int examples = Math.Max(0, Math.Min(8, call.Int("examples", 2)));
            int limit = Math.Max(1, Math.Min(50, call.Int("limit", 12)));

            //The open level: its name, its script, and the key the type has
            Commands openCommands = null;
            string openLevel = null;
            McpEditor.UI(() =>
            {
                LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
                if (content != null && content.IsLevelDataLoaded && content.Level?.Commands != null)
                {
                    openCommands = content.Level.Commands;
                    openLevel = NormaliseLevel(content.Level.Name);
                }
            });
            List<string> levels = Levels();
            List<string> scope = ReadLevels(call, openLevel, levels);
            if (scope == null && openCommands == null)
                throw new McpError(McpErrorCodes.LevelNotLoaded, "No level is open: load_level first, or pass 'level' (a level name, or 'all') to read others from disk.");

            //Each level's index: the open one live, the rest from disk
            List<UsageIndex> indexes = new List<UsageIndex>();
            using (McpEditorTools.Heartbeat(call, "Counting how the level's scripts use " + type))
            {
                if (scope == null || (openLevel != null && scope.Contains(openLevel)))
                    indexes.Add(McpEditor.UI(() => OpenIndex(call, openCommands, openLevel)));
                if (scope != null)
                {
                    int done = 0;
                    foreach (string level in scope)
                    {
                        call.ThrowIfCancelled();
                        done++;
                        if (level == openLevel) continue;
                        call.Progress("Reading " + level + " (" + done + "/" + scope.Count + ")", done, scope.Count);
                        try { indexes.Add(LevelIndex(call, level, levels)); }
                        catch (McpError e) when (scope.Count > 2) { call.Note(level + " was skipped: " + e.Message); }
                    }
                }
            }

            //What the type is: a function type, else a composite (by path in the open level, else by the indexes' keys)
            string key;
            string kind;
            if (McpBrowseTools.TryParseFunctionType(type, out FunctionType function))
            {
                key = function.ToString();
                kind = "function";
            }
            else
            {
                key = McpEditor.UI(() =>
                {
                    if (openCommands == null) return null;
                    try { return CompositeKey(McpScript.FindComposite(openCommands, type)); }
                    catch (McpError) { return null; }
                }) ?? CompositeKeyIn(indexes, type);
                kind = "composite";
            }

            List<(string level, TypeUsage usage)> found = indexes.Where(o => o.Types.ContainsKey(key)).Select(o => (o.Level, o.Types[key])).ToList();
            JObject result = new JObject() { ["type"] = kind == "function" ? key : key.Substring("composite:".Length), ["kind"] = kind };
            if (indexes.Count == 1) result["level"] = indexes[0].Level;
            else result["levels"] = indexes.Count;
            int count = found.Sum(o => o.usage.Count);
            result["count"] = count;
            if (found.Count > 1)
                result["in_levels"] = new JObject(found.OrderByDescending(o => o.usage.Count).Select(o => new JProperty(o.level, o.usage.Count)));
            if (found.Count == 0)
            {
                result["note"] = (kind == "function" ? key + " is" : "That composite is") + " not used in " + (indexes.Count == 1 ? indexes[0].Level : "these levels") + "." +
                    (scope == null ? " level: 'all' looks in every level." : "");
                return result;
            }
            bool several = indexes.Count > 1;
            Report(result, found.Select(o => o.usage).ToList(), pin, limit, several);
            if (count == 0)
                result["note"] = "There are no " + key + " entities, but links reach it through aliases or proxies (the patterns below).";

            //Whole examples: from the open level when it has some, else from the level with the most
            if (examples > 0)
            {
                JArray shown = new JArray();
                (string level, TypeUsage usage) source = found.FirstOrDefault(o => o.level == openLevel && ExamplesOf(o.usage, pin, 1).Count != 0);
                if (source.usage == null) source = found.Where(o => ExamplesOf(o.usage, pin, 1).Count != 0).OrderByDescending(o => o.usage.Count).FirstOrDefault();
                if (source.usage != null)
                {
                    bool open = source.level == openLevel && openCommands != null;
                    List<Ref> picks = ExamplesOf(source.usage, pin, examples);
                    if (open)
                        McpEditor.UI(() => { foreach (Ref pick in picks) { JObject example = Example(openCommands, pick, false, pin); if (example != null) shown.Add(example); } });
                    else
                    {
                        string path = CommandsPathOf(source.level, levels);
                        Commands commands = LoadCommands(source.level, path, new FileInfo(path).LastWriteTimeUtc.Ticks);
                        foreach (Ref pick in picks) { JObject example = Example(commands, pick, true, pin); if (example != null) shown.Add(example); }
                    }
                }
                if (shown.Count != 0) result["examples"] = shown;
            }
            return result;
        }

        /// <summary>The key of a composite named by path or leaf in the indexes (for a composite the open level does not have).</summary>
        private static string CompositeKeyIn(List<UsageIndex> indexes, string text)
        {
            string wanted = McpScript.NormalisePath(text);
            List<string> keys = indexes.SelectMany(o => o.Types.Keys).Where(o => o.StartsWith("composite:", StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            string exact = keys.FirstOrDefault(o => string.Equals(o.Substring(10), wanted, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
            List<string> ending = keys.Where(o => o.EndsWith("\\" + wanted, StringComparison.OrdinalIgnoreCase) || McpNames.Squash(McpNames.Leaf(o.Substring(10))) == McpNames.Squash(wanted)).ToList();
            if (ending.Count == 1) return ending[0];
            if (ending.Count > 1)
                throw McpError.Ambiguous("composites", text, ending.Take(20).Select(o => new JObject() { ["name"] = o.Substring(10) }), "Give the full path.");
            throw McpError.NotFound("function type or composite", text, Enum.GetNames(typeof(FunctionType)).Concat(keys.Select(o => McpNames.Leaf(o.Substring(10)))),
                "list_function_types lists the types; find_composites (or search_level) the composites.");
        }

        /// <summary>A type's usage over one or more levels, as a result: parameters with typical values, then the commonest links in and out.</summary>
        private static void Report(JObject result, List<TypeUsage> usages, string pin, int limit, bool levelInExamples)
        {
            int count = usages.Sum(o => o.Count);
            bool ThroughPin(string pattern) => HasPin(pattern, pin);

            //Parameters: how many set each, and the values they set it to
            Dictionary<string, ParameterUsage> merged = new Dictionary<string, ParameterUsage>(StringComparer.OrdinalIgnoreCase);
            foreach (TypeUsage usage in usages)
            {
                foreach (KeyValuePair<string, ParameterUsage> parameter in usage.Parameters)
                {
                    if (!merged.TryGetValue(parameter.Key, out ParameterUsage into))
                        merged[parameter.Key] = into = new ParameterUsage();
                    into.Count += parameter.Value.Count;
                    into.Others += parameter.Value.Others;
                    foreach (KeyValuePair<string, int> value in parameter.Value.Values)
                        into.Values[value.Key] = (into.Values.TryGetValue(value.Key, out int n) ? n : 0) + value.Value;
                    into.Numbers.AddRange(parameter.Value.Numbers.Take(MaxNumbers));
                }
            }
            JArray parameters = new JArray();
            foreach (KeyValuePair<string, ParameterUsage> parameter in merged.Where(o => pin == null || string.Equals(o.Key, pin, StringComparison.OrdinalIgnoreCase)).OrderByDescending(o => o.Value.Count).ThenBy(o => o.Key).Take(limit))
            {
                JObject described = new JObject() { ["name"] = parameter.Key, ["set_on"] = parameter.Value.Count };
                List<KeyValuePair<string, int>> values = parameter.Value.Values.OrderByDescending(o => o.Value).ToList();
                if (values.Count != 0)
                    described["values"] = new JArray(values.Take(5).Select(o => new JObject() { ["value"] = ParseValue(o.Key), ["n"] = o.Value }));
                int distinct = values.Count + (parameter.Value.Others > 0 ? 1 : 0);
                if (distinct > 5) described["distinct_values"] = values.Count + (parameter.Value.Others > 0 ? "+" : "");
                if (parameter.Value.Numbers.Count > 2)
                {
                    List<double> sorted = parameter.Value.Numbers.OrderBy(o => o).ToList();
                    described["range"] = new JArray(Math.Round(sorted[0], 4), Math.Round(sorted[sorted.Count / 2], 4), Math.Round(sorted[sorted.Count - 1], 4));
                }
                parameters.Add(described);
            }
            result["parameters"] = parameters;
            if (parameters.Count != 0)
                result["parameters_key"] = "set_on: how many of the " + count + " set it (the rest keep the default); values: the commonest, with how many; range: [min, median, max]";

            Dictionary<string, int> overrides = new Dictionary<string, int>();
            foreach (TypeUsage usage in usages)
                foreach (KeyValuePair<string, int> o in usage.AliasOverrides)
                    overrides[o.Key] = (overrides.TryGetValue(o.Key, out int n) ? n : 0) + o.Value;
            List<KeyValuePair<string, int>> overriding = overrides.Where(o => pin == null || string.Equals(o.Key, pin, StringComparison.OrdinalIgnoreCase)).OrderByDescending(o => o.Value).Take(8).ToList();
            if (overriding.Count != 0)
                result["alias_overrides"] = new JObject(overriding.Select(o => new JProperty(o.Key, o.Value)));

            JArray Patterns(Func<TypeUsage, Dictionary<string, PatternUsage>> of, string field)
            {
                Dictionary<string, (int count, List<Ref> examples)> all = new Dictionary<string, (int, List<Ref>)>();
                foreach (TypeUsage usage in usages)
                {
                    foreach (KeyValuePair<string, PatternUsage> pattern in of(usage))
                    {
                        if (!ThroughPin(pattern.Key)) continue;
                        all.TryGetValue(pattern.Key, out (int count, List<Ref> examples) known);
                        List<Ref> examples = known.examples ?? new List<Ref>();
                        if (examples.Count < MaxPatternExamples) examples.AddRange(pattern.Value.Examples.Take(MaxPatternExamples - examples.Count));
                        all[pattern.Key] = (known.count + pattern.Value.Count, examples);
                    }
                }
                List<KeyValuePair<string, (int count, List<Ref> examples)>> ordered = all.OrderByDescending(o => o.Value.count).ThenBy(o => o.Key).ToList();
                if (ordered.Count > limit) result[field + "_total"] = ordered.Count;
                return new JArray(ordered.Take(limit).Select(o =>
                {
                    JObject pattern = new JObject() { ["pattern"] = o.Key, ["n"] = o.Value.count };
                    if (o.Value.examples.Count != 0) pattern["example"] = o.Value.examples[0].Json(levelInExamples);
                    return pattern;
                }));
            }
            result["links_in"] = Patterns(o => o.In, "links_in");
            result["links_out"] = Patterns(o => o.Out, "links_out");
        }

        /// <summary>Whether a link pattern ('Type.pin -> pin' or 'pin -> Type.pin') goes through a pin (null: any).</summary>
        private static bool HasPin(string pattern, string pin) => pin == null || pattern.Split(new[] { ' ', '.', '>', '-' }, StringSplitOptions.RemoveEmptyEntries).Any(o => string.Equals(o, pin, StringComparison.OrdinalIgnoreCase));

        /// <summary>Entities worth showing whole: those of the commonest patterns (through the pin, when one is asked about) first, then any.</summary>
        private static List<Ref> ExamplesOf(TypeUsage usage, string pin, int count)
        {
            List<Ref> picks = new List<Ref>();
            foreach (PatternUsage pattern in usage.In.Concat(usage.Out).Where(o => HasPin(o.Key, pin)).OrderByDescending(o => o.Value.Count).Select(o => o.Value))
                foreach (Ref example in pattern.Examples)
                    if (picks.Count < count && !picks.Any(o => o.CompositeId == example.CompositeId && o.Id == example.Id))
                        picks.Add(example);
            foreach (Ref sample in usage.Samples)
                if (picks.Count < count && !picks.Any(o => o.CompositeId == sample.CompositeId && o.Id == sample.Id))
                    picks.Add(sample);
            return picks;
        }

        private static JToken ParseValue(string json)
        {
            try { return JToken.Parse(json); }
            catch { return json; }
        }

        /// <summary>One entity in full, for an example: its own parameters and its links in and out, each end with its type.</summary>
        private static JObject Example(Commands commands, Ref reference, bool withLevel, string pin)
        {
            Composite composite = commands.GetComposite(new ShortGuid(reference.CompositeId));
            Entity entity = composite?.GetEntityByID(new ShortGuid(reference.Id));
            if (entity == null) return null;
            JObject example = new JObject();
            if (withLevel) example["level"] = reference.Level;
            example["composite"] = composite.name;
            example["id"] = reference.Id;
            example["name"] = McpScript.EntityName(commands, composite, entity);
            example["type"] = McpScript.TypeName(commands, composite, entity);
            JObject values = new JObject();
            foreach (Parameter parameter in entity.parameters)
                if (parameter.name != ShortGuids.name && !(parameter.content is cResource))
                    values[McpScript.ParamName(parameter.name)] = McpValues.ToJson(parameter.content, commands);
            example["parameters"] = values;
            JObject fed = McpScript.FedBy(commands, composite, entity);
            if (fed != null) example["parameters_fed_by"] = fed;
            //Each other end with its type; a composite pin as "pin 'name'" (its link pin is its own name)
            string End(Entity other, ShortGuid pinOf) => other is VariableEntity
                ? "pin '" + McpScript.EntityName(commands, composite, other) + "'"
                : McpScript.EntityName(commands, composite, other) + " (" + McpScript.TypeName(commands, composite, other) + ")." + McpScript.ParamName(pinOf);
            List<string> incoming = new List<string>();
            foreach (Entity other in composite.GetEntities())
                foreach (EntityConnector link in other.childLinks)
                    if (link.linkedEntityID == entity.shortGUID && (pin == null || string.Equals(McpScript.ParamName(link.linkedParamID), pin, StringComparison.OrdinalIgnoreCase) || string.Equals(McpScript.ParamName(link.thisParamID), pin, StringComparison.OrdinalIgnoreCase)))
                        incoming.Add(End(other, link.thisParamID) + " -> " + McpScript.ParamName(link.linkedParamID));
            List<string> outgoing = new List<string>();
            foreach (EntityConnector link in entity.childLinks)
            {
                Entity other = composite.GetEntityByID(link.linkedEntityID);
                if (other == null) continue;
                if (pin != null && !string.Equals(McpScript.ParamName(link.thisParamID), pin, StringComparison.OrdinalIgnoreCase) && !string.Equals(McpScript.ParamName(link.linkedParamID), pin, StringComparison.OrdinalIgnoreCase)) continue;
                outgoing.Add(McpScript.ParamName(link.thisParamID) + " -> " + End(other, link.linkedParamID));
            }
            example["links_in"] = new JArray(incoming.Take(15));
            example["links_out"] = new JArray(outgoing.Take(15));
            if (incoming.Count > 15 || outgoing.Count > 15)
                example["links_total"] = new JArray(incoming.Count, outgoing.Count);
            if (entity is TriggerSequence sequence)
                example["sequence"] = sequence.sequence.Count + " entries, methods " + string.Join(", ", sequence.methods.Select(o => McpScript.ParamName(o.method)));
            if (entity is CAGEAnimation animation)
                example["animation"] = animation.connections.Count + " bindings, " + animation.eventTracks.Count + " event tracks";
            return example;
        }

        /// <summary>
        /// A short account of how the open level uses a function type, for describe_function_type: the commonest parameters and
        /// links in and out. Call from a tool's thread.
        /// </summary>
        internal static JObject Summary(McpCall call, FunctionType type)
        {
            return McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                UsageIndex index = OpenIndex(call, content.Level.Commands, NormaliseLevel(content.Level.Name));
                if (!index.Types.TryGetValue(type.ToString(), out TypeUsage usage))
                    return new JObject() { ["count"] = 0, ["note"] = type + " is not used in this level: get_usage_patterns with level 'all' looks in every level." };
                JObject summary = new JObject() { ["count"] = usage.Count };
                Report(summary, new List<TypeUsage>() { usage }, null, 6, false);
                summary.Remove("parameters_key");
                //A few of them to look at whole (describe_entity takes composite + id), from the commonest patterns first
                List<Ref> examples = ExamplesOf(usage, null, 3);
                if (examples.Count != 0)
                    summary["examples"] = new JArray(examples.Select(o => o.Json(false)));
                summary["more"] = "describe_entity shows an example whole; get_usage_patterns gives more patterns, whole examples, and other levels.";
                return summary;
            });
        }
        #endregion

        #region trace_links
        /// <summary>A place a trace has reached: an entity (and the pin the trace is about, if any) in a composite, and the instances it came down through.</summary>
        private sealed class Node
        {
            public Composite Composite;
            public Entity Entity;
            public ShortGuid? Pin;
            /// <summary>The instances descended through to get here, innermost last: going up out of the composite returns to the last.</summary>
            public List<(Composite parent, Entity instance)> Down = new List<(Composite, Entity)>();
            public int Depth;
            public int Hop = -1;        //the hop that reached it
            public Node From;
            /// <summary>For a composite's pin: true when the trace reached it from inside (it goes on outside), false from outside (it goes on inside), null both.</summary>
            public bool? Outward;
        }

        /// <summary>Who points at what, worked out once per trace: aliases and proxies by target, sequences and animations by entity listed, instances by composite.</summary>
        private sealed class Pointers
        {
            public readonly Dictionary<(Composite, Entity), List<(Composite owner, Entity pointer)>> Aliases = new Dictionary<(Composite, Entity), List<(Composite, Entity)>>();
            public readonly Dictionary<(Composite, Entity), List<(Composite owner, Entity sequence)>> Sequences = new Dictionary<(Composite, Entity), List<(Composite, Entity)>>();
            public readonly Dictionary<(Composite, Entity), List<(Composite owner, Entity animation, string how)>> Animations = new Dictionary<(Composite, Entity), List<(Composite, Entity, string)>>();
            public readonly Dictionary<Composite, List<(Composite parent, Entity instance)>> Instances = new Dictionary<Composite, List<(Composite, Entity)>>();

            private static void Add<T>(Dictionary<(Composite, Entity), List<T>> map, (Composite, Entity) key, T value)
            {
                if (!map.TryGetValue(key, out List<T> list)) map[key] = list = new List<T>();
                list.Add(value);
            }

            public Pointers(Commands commands, CancellationToken cancel)
            {
                foreach (Composite composite in commands.Entries)
                {
                    if (composite == null) continue;
                    cancel.ThrowIfCancellationRequested();
                    foreach (FunctionEntity function in composite.functions)
                    {
                        if (function.function.IsFunctionType) continue;
                        Composite child = commands.GetComposite(function.function);
                        if (child == null) continue;
                        if (!Instances.TryGetValue(child, out List<(Composite, Entity)> list)) Instances[child] = list = new List<(Composite, Entity)>();
                        list.Add((composite, function));
                    }
                    foreach (AliasEntity alias in composite.aliases)
                    {
                        (Composite c, Entity e) target = (null, null);
                        try { target = commands.Utils.GetResolvedTarget(commands.Utils.ResolveAlias(alias, composite)); } catch { }
                        if (target.e != null) Add(Aliases, (target.c, target.e), (composite, (Entity)alias));
                    }
                    foreach (ProxyEntity proxy in composite.proxies)
                    {
                        (Composite c, Entity e) target = (null, null);
                        try { target = commands.Utils.GetResolvedTarget(commands.Utils.ResolveProxy(proxy)); } catch { }
                        if (target.e != null) Add(Aliases, (target.c, target.e), (composite, (Entity)proxy));
                    }
                    foreach (TriggerSequence sequence in composite.functions.OfType<TriggerSequence>())
                    {
                        HashSet<(Composite, Entity)> listed = new HashSet<(Composite, Entity)>();
                        foreach (TriggerSequence.SequenceEntry entry in sequence.sequence)
                        {
                            if (entry?.connectedEntity?.path == null) continue;
                            (Composite c, Entity e) target = (null, null);
                            try { target = commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(entry.connectedEntity, composite)); } catch { }
                            if (target.e != null && listed.Add((target.c, target.e))) Add(Sequences, (target.c, target.e), (composite, (Entity)sequence));
                        }
                    }
                    foreach (CAGEAnimation animation in composite.functions.OfType<CAGEAnimation>())
                    {
                        HashSet<(Composite, Entity)> bound = new HashSet<(Composite, Entity)>();
                        foreach (CAGEAnimation.Connection connection in animation.connections)
                        {
                            if (connection?.connectedEntity?.path == null) continue;
                            (Composite c, Entity e) target = (null, null);
                            try { target = commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(connection.connectedEntity, composite)); } catch { }
                            if (target.e != null && bound.Add((target.c, target.e))) Add(Animations, (target.c, target.e), (composite, (Entity)animation, "animates it"));
                        }
                        //An event key that fires a method of an entity beside it
                        foreach (CAGEAnimation.EventTrack track in animation.eventTracks.Where(o => o != null))
                            foreach (CAGEAnimation.EventTrack.Keyframe key in track.keyframes.Where(o => o != null && o.track_type == ANIM_TRACK_TYPE.T_GUID))
                            {
                                Entity fired = composite.GetEntityByID(key.forward);
                                if (fired != null && bound.Add((composite, fired))) Add(Animations, (composite, fired), (composite, (Entity)animation, "fires it as an event"));
                            }
                    }
                }
            }
        }

        private static object TraceLinks(McpCall call)
        {
            string direction = (call.Str("direction") ?? "up").Trim().ToLowerInvariant();
            if (direction == "upstream") direction = "up";
            if (direction == "downstream") direction = "down";
            if (direction != "up" && direction != "down")
                throw McpError.Invalid("'direction' is 'up' (what drives it) or 'down' (what it drives).");
            bool up = direction == "up";
            int maxDepth = Math.Max(1, Math.Min(20, call.Int("depth", 6)));
            int limit = Math.Max(1, Math.Min(500, call.Int("limit", 100)));

            using (McpEditorTools.Heartbeat(call, "Tracing links"))
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                Composite root = commands.EntryPoints[0];
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                McpBrowseTools.CompileIfShown(composite);
                Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity", required: true));
                ShortGuid? pin = call.Has("pin") ? McpScript.ParamId(call.Str("pin")) : (ShortGuid?)null;

                //A placement: crossing up out of a composite on it goes to the one instance placing it there
                List<Entity> placement = null;
                List<Composite> along = null;
                if (call.Has("path"))
                {
                    placement = McpScript.ChainFrom(commands, root, McpScript.PathSteps(call.Token("path")));
                    along = McpScript.CompositesAlong(commands, root, placement);
                    if (placement[placement.Count - 1] != entity || along[placement.Count - 1] != composite)
                        throw McpError.Invalid("'path' does not lead to " + McpScript.EntityName(commands, composite, entity) + " in " + composite.name + ": it is a placement of that entity, from the root.");
                }

                Pointers pointers = new Pointers(commands, call.Cancel);
                JArray hops = new JArray();
                List<Node> nodes = new List<Node>();
                HashSet<(Composite, Entity, ShortGuid?)> seen = new HashSet<(Composite, Entity, ShortGuid?)>();
                Queue<Node> pending = new Queue<Node>();
                Node start = new Node() { Composite = composite, Entity = entity, Pin = pin };
                pending.Enqueue(start);
                seen.Add((composite, entity, pin));
                int cut = 0;
                List<Node> ends = new List<Node>();

                string Label(Composite c, Entity e, ShortGuid? p) => McpScript.EntityName(commands, c, e) + (p == null ? "" : "." + McpScript.ParamName(p.Value));
                JObject Hop(int depth, string kind, Composite place, Composite fromComposite, Entity fromEntity, ShortGuid? fromPin, Composite toComposite, Entity toEntity, ShortGuid? toPin, string note = null)
                {
                    JObject hop = new JObject()
                    {
                        ["depth"] = depth,
                        ["kind"] = kind,
                        ["composite"] = place.name,
                        ["from"] = Label(fromComposite, fromEntity, fromPin),
                        ["from_id"] = McpScript.Id(fromEntity.shortGUID),
                        ["from_type"] = McpScript.TypeName(commands, fromComposite, fromEntity),
                        ["to"] = Label(toComposite, toEntity, toPin),
                        ["to_id"] = McpScript.Id(toEntity.shortGUID),
                    };
                    if (fromComposite != place) hop["from_composite"] = fromComposite.name;
                    if (toComposite != place) hop["to_composite"] = toComposite.name;
                    if (note != null) hop["note"] = note;
                    return hop;
                }
                //A step of the trace: the hop is recorded, and the place it reaches is followed further unless already seen
                bool Step(Node from, JObject hop, Composite c, Entity e, ShortGuid? p, List<(Composite, Entity)> down, bool? outward = null)
                {
                    if (hops.Count >= limit) { cut++; return false; }
                    hops.Add(hop);
                    Node next = new Node() { Composite = c, Entity = e, Pin = p, Down = down ?? from.Down, Depth = from.Depth + 1, Hop = hops.Count - 1, From = from, Outward = outward };
                    nodes.Add(next);
                    if (!seen.Add((c, e, p))) { hop["note"] = ((string)hop["note"] == null ? "" : (string)hop["note"] + "; ") + "reached already: not followed again"; return true; }
                    if (next.Depth >= maxDepth) { cut++; hop["note"] = ((string)hop["note"] == null ? "" : (string)hop["note"] + "; ") + "depth limit: not followed further"; return true; }
                    pending.Enqueue(next);
                    return true;
                }
                //Where a composite's pin leads outside it: the instance it came down through, else this placement's parent, else every instance
                List<(Composite parent, Entity instance, List<(Composite, Entity)> down)> Parents(Node node)
                {
                    List<(Composite, Entity, List<(Composite, Entity)>)> parents = new List<(Composite, Entity, List<(Composite, Entity)>)>();
                    if (node.Down.Count != 0)
                    {
                        (Composite parent, Entity instance) last = node.Down[node.Down.Count - 1];
                        parents.Add((last.parent, last.instance, node.Down.Take(node.Down.Count - 1).ToList()));
                        return parents;
                    }
                    if (along != null)
                    {
                        int at = along.IndexOf(node.Composite);
                        if (at > 0) parents.Add((along[at - 1], placement[at - 1], node.Down));
                        return parents;
                    }
                    if (pointers.Instances.TryGetValue(node.Composite, out List<(Composite parent, Entity instance)> all))
                        foreach ((Composite parent, Entity instance) in all) parents.Add((parent, instance, node.Down));
                    return parents;
                }
                //A link out of a value parameter is its owner reading a value from the other end, not driving it (as parameters_fed_by and the 'down' trace read it)
                int reads = 0, readsListed = 0;
                bool Reads(Composite owner, Entity source, ShortGuid param)
                {
                    if (source is VariableEntity) return false;
                    (ParameterVariant? variant, DataType? _, ShortGuid __) = commands.Utils.GetParameterMetadata(source, param, owner);
                    return McpScript.HoldsValue(variant);
                }
                //Such a reader is never followed; it is listed when it reads the pin asked about
                void ReadBy(Node at, JObject hop)
                {
                    reads++;
                    if (at != start || pin == null) return;
                    if (hops.Count >= limit) { cut++; return; }
                    hop["kind"] = "read_by";
                    hop["note"] = ((string)hop["note"] == null ? "" : (string)hop["note"] + "; ") + "reads a value from it through a link out of its value parameter: it does not drive it, so it is not followed";
                    hops.Add(hop);
                    readsListed++;
                }

                while (pending.Count != 0)
                {
                    Node node = pending.Dequeue();
                    int before = hops.Count;
                    Composite c = node.Composite;
                    Entity e = node.Entity;
                    if (up)
                    {
                        //Links into it (into the pin asked about); a composite pin reached from inside is driven from outside only
                        foreach (Entity source in node.Outward == true ? new List<Entity>() : c.GetEntities())
                        {
                            foreach (EntityConnector link in source.childLinks)
                            {
                                if (link.linkedEntityID != e.shortGUID || (node.Pin != null && link.linkedParamID != node.Pin.Value)) continue;
                                JObject hop = Hop(node.Depth + 1, "link", c, c, source, link.thisParamID, c, e, link.linkedParamID);
                                if (source is VariableEntity variable)
                                {
                                    //A pin of the composite: what reaches it comes in from outside, through the instance's pin of that name
                                    hop["note"] = "a pin of " + c.name + ": followed out to the instances placing it";
                                    if (!Step(node, hop, c, source, variable.name, null, outward: true)) break;
                                    continue;
                                }
                                if (Reads(c, source, link.thisParamID))
                                {
                                    ReadBy(node, hop);
                                    continue;
                                }
                                //A relay fires when its method is called (LogicCounter's on_Up when Up is): follow what calls that method
                                ShortGuid? method = null;
                                if (NodeUtils.GetMethodRelays(source, c, commands).TryGetValue(link.thisParamID, out ShortGuid called)) method = called;
                                if (!Step(node, hop, c, source, method, null)) break;
                            }
                        }
                        //A pin of a composite: the instances' pins outside
                        if (e is VariableEntity pinVariable && node.Outward != false)
                        {
                            foreach ((Composite parent, Entity instance, List<(Composite, Entity)> down) in Parents(node))
                            {
                                foreach (Entity source in parent.GetEntities())
                                    foreach (EntityConnector link in source.childLinks)
                                        if (link.linkedEntityID == instance.shortGUID && link.linkedParamID == pinVariable.name)
                                        {
                                            JObject hop = Hop(node.Depth + 1, "link", parent, parent, source, link.thisParamID, parent, instance, link.linkedParamID, "into the instance's pin '" + ShortGuidUtils.FindString(pinVariable.name) + "'");
                                            if (Reads(parent, source, link.thisParamID)) ReadBy(node, hop);
                                            else Step(node, hop, parent, source, null, down);
                                        }
                                //And through aliases of that instance (its pin linked on the alias)
                                if (pointers.Aliases.TryGetValue((parent, instance), out List<(Composite owner, Entity pointer)> instancePointers))
                                    foreach ((Composite owner, Entity pointer) in instancePointers)
                                        foreach (Entity source in owner.GetEntities())
                                            foreach (EntityConnector link in source.childLinks)
                                                if (link.linkedEntityID == pointer.shortGUID && link.linkedParamID == pinVariable.name)
                                                {
                                                    JObject hop = Hop(node.Depth + 1, McpScript.Kind(pointer), owner, owner, source, link.thisParamID, owner, pointer, link.linkedParamID, "through " + McpScript.Kind(pointer) + " of instance " + McpScript.EntityName(commands, parent, instance));
                                                    if (Reads(owner, source, link.thisParamID)) ReadBy(node, hop);
                                                    else Step(node, hop, owner, source, null, new List<(Composite, Entity)>());
                                                }
                            }
                        }
                        //Aliases and proxies standing for it: links into them
                        if (pointers.Aliases.TryGetValue((c, e), out List<(Composite owner, Entity pointer)> standing))
                            foreach ((Composite owner, Entity pointer) in standing)
                                foreach (Entity source in owner.GetEntities())
                                    foreach (EntityConnector link in source.childLinks)
                                        if (link.linkedEntityID == pointer.shortGUID && (node.Pin == null || link.linkedParamID == node.Pin.Value))
                                        {
                                            JObject hop = Hop(node.Depth + 1, McpScript.Kind(pointer), owner, owner, source, link.thisParamID, owner, pointer, link.linkedParamID, McpScript.Kind(pointer) + " of " + McpScript.EntityName(commands, c, e) + " in " + c.name);
                                            if (Reads(owner, source, link.thisParamID)) ReadBy(node, hop);
                                            else Step(node, hop, owner, source, null, new List<(Composite, Entity)>());
                                        }
                        //TriggerSequences listing it, and CAGEAnimations animating it or firing it
                        if (pointers.Sequences.TryGetValue((c, e), out List<(Composite owner, Entity sequence)> sequences))
                            foreach ((Composite owner, Entity sequence) in sequences)
                            {
                                List<string> methods = ((TriggerSequence)sequence).methods.Select(o => McpScript.ParamName(o.method)).ToList();
                                Step(node, Hop(node.Depth + 1, "sequence", owner, owner, sequence, null, c, e, null, "lists it" + (methods.Count == 0 ? " (a list the sequence's owner reads, such as a Zone's contents)" : "; its methods: " + string.Join(", ", methods))), owner, sequence, null, new List<(Composite, Entity)>());
                            }
                        if (pointers.Animations.TryGetValue((c, e), out List<(Composite owner, Entity animation, string how)> animations))
                            foreach ((Composite owner, Entity animation, string how) in animations)
                                Step(node, Hop(node.Depth + 1, "animation", owner, owner, animation, null, c, e, null, how), owner, animation, null, new List<(Composite, Entity)>());
                    }
                    else
                    {
                        //Its events out (not the data links its parameters read through); a composite pin reached from inside goes on outside only
                        foreach (EntityConnector link in node.Outward == true ? new List<EntityConnector>() : e.childLinks)
                        {
                            if (node.Pin != null && link.thisParamID != node.Pin.Value) continue;
                            if (node.Pin == null && !(e is VariableEntity))
                            {
                                (ParameterVariant? variant, DataType? _, ShortGuid __) = commands.Utils.GetParameterMetadata(e, link.thisParamID, c);
                                if (McpScript.HoldsValue(variant)) continue;
                            }
                            Entity target = c.GetEntityByID(link.linkedEntityID);
                            if (target == null) continue;
                            JObject hop = Hop(node.Depth + 1, "link", c, c, e, link.thisParamID, c, target, link.linkedParamID);
                            if (target is VariableEntity output)
                            {
                                //Out through a pin of the composite: on to what each instance's pin of that name links to
                                hop["note"] = "a pin of " + c.name + ": followed out to the instances placing it";
                                Step(node, hop, c, target, output.name, null, outward: true);
                                continue;
                            }
                            Composite inner = McpScript.InstancedComposite(commands, target);
                            if (inner != null)
                            {
                                //Into an instance's pin: the pin variable inside, and what it links to there (the instance itself is not followed)
                                VariableEntity inside = inner.variables.FirstOrDefault(o => o.name == link.linkedParamID);
                                if (inside == null)
                                {
                                    hop["note"] = inner.name + " has no pin '" + McpScript.ParamName(link.linkedParamID) + "' inside";
                                    Step(node, hop, c, target, link.linkedParamID, null);
                                }
                                else
                                {
                                    hop["note"] = "into " + inner.name + "'s pin";
                                    Step(node, hop, inner, inside, null, node.Down.Concat(new[] { (c, target) }).ToList(), outward: false);
                                }
                                continue;
                            }
                            if (target is AliasEntity || target is ProxyEntity)
                            {
                                (Composite tc, Entity te) = (null, null);
                                try { (tc, te) = commands.Utils.GetResolvedTarget(target is AliasEntity a ? commands.Utils.ResolveAlias(a, c) : commands.Utils.ResolveProxy((ProxyEntity)target)); } catch { }
                                Step(node, hop, c, target, link.linkedParamID, null);
                                if (te != null)
                                    Step(node, Hop(node.Depth + 1, McpScript.Kind(target), tc, c, target, link.linkedParamID, tc, te, link.linkedParamID, "stands for it"), tc, te, null, new List<(Composite, Entity)>());
                                continue;
                            }
                            //A method's relay fires next (LogicCounter's Up fires on_Up); other entities' events follow too
                            ShortGuid relay = commands.Utils.GetRelay(link.linkedParamID);
                            Step(node, hop, c, target, null, null);
                            if (relay != ShortGuid.Invalid && target.childLinks.Any(o => o.thisParamID == relay))
                                hop["note"] = "its relay " + McpScript.ParamName(relay) + " fires next";
                        }
                        //A composite's pin: the instances' pins outside, and what they link to there
                        if (e is VariableEntity pinVariable && node.Outward != false)
                        {
                            foreach ((Composite parent, Entity instance, List<(Composite, Entity)> down) in Parents(node))
                                foreach (EntityConnector link in instance.childLinks)
                                {
                                    if (link.thisParamID != pinVariable.name) continue;
                                    Entity target = parent.GetEntityByID(link.linkedEntityID);
                                    if (target != null)
                                        Step(node, Hop(node.Depth + 1, "link", parent, parent, instance, link.thisParamID, parent, target, link.linkedParamID, "out of " + c.name + "'s pin, on its instance"), parent, target, null, down);
                                }
                        }
                        //A TriggerSequence runs through its entries
                        if (e is TriggerSequence sequence && node.Depth < maxDepth)
                        {
                            int listed = 0;
                            foreach (TriggerSequence.SequenceEntry entry in sequence.sequence)
                            {
                                if (entry?.connectedEntity?.path == null) continue;
                                (Composite tc, Entity te) = (null, null);
                                try { (tc, te) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(entry.connectedEntity, c)); } catch { }
                                if (te == null) continue;
                                if (++listed > 20) { cut++; break; }
                                string through = string.Join(" > ", McpScript.DescribePath(commands, commands.Utils.ResolveEntityPath(entry.connectedEntity, c), entry.connectedEntity.path).Select(o => (string)o));
                                Step(node, Hop(node.Depth + 1, "sequence", c, c, e, null, tc, te, null, "entry " + through + ", at " + Math.Round((double)entry.timing, 2) + " s"), tc, te, null, new List<(Composite, Entity)>());
                            }
                        }
                    }
                    if (hops.Count == before) ends.Add(node);
                }

                //One line per root cause (up) or end (down): the hops from the start to it
                JArray chains = new JArray();
                foreach (Node end in ends.Where(o => o != start).Take(15))
                {
                    List<string> parts = new List<string>();
                    for (Node at = end; at != null && at.Hop >= 0; at = at.From)
                    {
                        JObject hop = (JObject)hops[at.Hop];
                        parts.Add(up ? (string)hop["from"] + " -> " + (string)hop["to"] : (string)hop["to"]);
                    }
                    if (up)
                        chains.Add(string.Join(" => ", parts.Select(o => o.Split(new[] { " -> " }, StringSplitOptions.None)[0])) + " => " + Label(composite, entity, pin));
                    else
                    {
                        parts.Reverse();
                        chains.Add(Label(composite, entity, pin) + " => " + string.Join(" => ", parts));
                    }
                }

                JObject result = new JObject()
                {
                    ["entity"] = new JObject() { ["composite"] = composite.name, ["id"] = McpScript.Id(entity.shortGUID), ["name"] = McpScript.EntityName(commands, composite, entity), ["type"] = McpScript.TypeName(commands, composite, entity) },
                    ["direction"] = direction,
                    ["hops"] = hops,
                };
                if (chains.Count != 0) result["chains"] = chains;
                if (hops.Count == 0)
                    result["note"] = up ? "Nothing links into it (or into an alias, proxy, sequence or animation of it): it starts by itself (start_on_reset or the like) or is never triggered."
                                        : "It fires no events out (its links out, if any, are data its parameters read).";
                else if (cut != 0)
                    result["truncated"] = "Some branches were not followed (depth " + maxDepth + ", " + limit + " hops): raise 'depth' or 'limit', or trace from a hop's entity.";
                if (reads > readsListed)
                    result["value_reads"] = (reads - readsListed) + " link" + (reads - readsListed == 1 ? " comes" : "s come") + " out of value parameters: those entities read a value from what they link to rather than drive it, so they were not followed" +
                        (pin == null ? " (give 'pin' to list the ones reading a pin of this entity as read_by hops)." : ".");
                return result;
            });
        }
        #endregion

        #region find_links
        private static object FindLinks(McpCall call)
        {
            string fromType = call.Str("from_type")?.Trim(), toType = call.Str("to_type")?.Trim();
            string fromPin = call.Str("from_pin")?.Trim(), toPin = call.Str("to_pin")?.Trim();
            string fromName = call.Str("from_name"), toName = call.Str("to_name");
            if (fromType == null && toType == null && fromPin == null && toPin == null && fromName == null && toName == null)
                throw McpError.Invalid("Give at least one of from_type, from_pin, from_name, to_type, to_pin or to_name.");
            if (call.Has("composite") && call.Has("within"))
                throw McpError.Invalid("Give 'composite' (that composite only) or 'within' (it and everything placed under it), not both.");
            ShortGuid? fromId = fromPin == null ? (ShortGuid?)null : McpScript.ParamId(fromPin);
            ShortGuid? toId = toPin == null ? (ShortGuid?)null : McpScript.ParamId(toPin);
            Func<string, bool> fromMatch = TypeMatcher(fromType), toMatch = TypeMatcher(toType);
            int limit = McpPaging.Limit(call, 100), offset = McpPaging.Offset(call);

            using (McpEditorTools.Heartbeat(call, "Searching links"))
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                IEnumerable<Composite> scope = call.Has("composite") ? new List<Composite>() { McpScript.FindComposite(commands, call.Str("composite")) }
                    : call.Has("within") ? McpBrowseTools.ReachableFrom(commands, McpScript.FindComposite(commands, call.Str("within"))).OrderBy(o => o.name, StringComparer.OrdinalIgnoreCase).ToList()
                    : (IEnumerable<Composite>)commands.Entries.Where(o => o != null);
                Classifier classifier = new Classifier(commands);
                JArray found = new JArray();
                int total = 0;
                foreach (Composite composite in scope)
                {
                    call.ThrowIfCancelled();
                    foreach (Entity source in composite.GetEntities())
                    {
                        if (source.childLinks.Count == 0) continue;
                        if (fromName != null && McpScript.EntityName(commands, composite, source).IndexOf(fromName, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        string sourceType = null;
                        foreach (EntityConnector link in source.childLinks)
                        {
                            if (fromId != null && link.thisParamID != fromId.Value) continue;
                            if (toId != null && link.linkedParamID != toId.Value) continue;
                            if (fromMatch != null && !fromMatch(sourceType ?? (sourceType = TypeText(commands, classifier, composite, source)))) break;
                            Entity target = composite.GetEntityByID(link.linkedEntityID);
                            if (target == null) continue;
                            if (toName != null && McpScript.EntityName(commands, composite, target).IndexOf(toName, StringComparison.OrdinalIgnoreCase) < 0) continue;
                            if (toMatch != null && !toMatch(TypeText(commands, classifier, composite, target))) continue;
                            total++;
                            if (total <= offset || found.Count >= limit) continue;
                            found.Add(new JObject()
                            {
                                ["composite"] = composite.name,
                                ["from"] = McpScript.EntityName(commands, composite, source) + "." + McpScript.ParamName(link.thisParamID),
                                ["from_id"] = McpScript.Id(source.shortGUID),
                                ["from_type"] = McpScript.TypeName(commands, composite, source),
                                ["to"] = McpScript.EntityName(commands, composite, target) + "." + McpScript.ParamName(link.linkedParamID),
                                ["to_id"] = McpScript.Id(target.shortGUID),
                                ["to_type"] = McpScript.TypeName(commands, composite, target),
                            });
                        }
                    }
                }
                JObject result = new JObject() { ["links"] = found };
                McpPaging.Describe(result, total, offset, found.Count);
                if (total == 0)
                {
                    List<string> unknown = new List<string>();
                    foreach (string named in new[] { fromType, toType })
                        if (named != null && !named.Equals("pin", StringComparison.OrdinalIgnoreCase) && !Enum.GetNames(typeof(FunctionType)).Any(o => o.IndexOf(named, StringComparison.OrdinalIgnoreCase) >= 0) && !commands.Entries.Any(o => o?.name != null && o.name.IndexOf(named, StringComparison.OrdinalIgnoreCase) >= 0))
                            unknown.Add("'" + named + "' is no function type or composite." + McpNames.DidYouMean(Enum.GetNames(typeof(FunctionType)), named, 5));
                    result["note"] = unknown.Count != 0 ? string.Join(" ", unknown) : "No link matches all of those.";
                }
                return result;
            });
        }

        /// <summary>A type as find_links matches it: the function type, "Instance:path" for an instance, "Pin", or "Alias:..."/"Proxy:..." with what it stands for.</summary>
        private static string TypeText(Commands commands, Classifier classifier, Composite composite, Entity entity)
        {
            (string key, string label) = classifier.Of(composite, entity);
            if (key != null && key.StartsWith("composite:", StringComparison.OrdinalIgnoreCase))
                return label.Substring(0, label.IndexOf(':') + 1) + key.Substring(10);
            return label;
        }

        /// <summary>Whether a type text fits what was asked: a whole function type exactly; else any type containing it, or text in an instance's composite path.</summary>
        private static Func<string, bool> TypeMatcher(string wanted)
        {
            if (wanted == null) return null;
            if (McpBrowseTools.TryParseFunctionType(wanted, out FunctionType exact))
            {
                string name = exact.ToString();
                return text => string.Equals(text, name, StringComparison.OrdinalIgnoreCase) || text.EndsWith(":" + name, StringComparison.OrdinalIgnoreCase);
            }
            if (wanted.Equals("pin", StringComparison.OrdinalIgnoreCase) || wanted.Equals("variable", StringComparison.OrdinalIgnoreCase))
                return text => text == "Pin";
            return text => text.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        #endregion
    }
}
