using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Finding composites and entities from what a client names them, and describing them back.
    /// </summary>
    /// <remarks>
    /// Ids are ShortGuids written as their byte string ("AA-BB-CC-DD"), as OpenCAGE shows them. Entity
    /// ids are only unique within their composite, so an entity is always named together with its
    /// composite. Names are accepted wherever an id is, as long as they pick out one thing.
    /// </remarks>
    internal static class McpScript
    {
        private static readonly Regex _byteString = new Regex("^[0-9A-Fa-f]{2}(-[0-9A-Fa-f]{2}){3}$", RegexOptions.Compiled);
        //"Name (AA-BB-CC-DD)" as paths and links are shown, and "[AA-BB-CC-DD] Name (type, path)" as candidates are listed
        private static readonly Regex _namedId = new Regex(@"^(?<name>.*?)\s*\((?<id>[0-9A-Fa-f]{2}(-[0-9A-Fa-f]{2}){3})\)\s*$", RegexOptions.Compiled);
        private static readonly Regex _listedId = new Regex(@"^\[(?<id>[0-9A-Fa-f]{2}(-[0-9A-Fa-f]{2}){3})\]\s*(?<name>.*)$", RegexOptions.Compiled);

        public static string Id(ShortGuid guid) => guid.ToByteString();

        /// <summary>
        /// The name part of a reference written as results show one - "Name (AA-BB-CC-DD)" or "[AA-BB-CC-DD] Name" - with the id
        /// it carries; the text as it is (and no id) otherwise.
        /// </summary>
        public static string StripId(string text, out ShortGuid? id)
        {
            id = null;
            if (string.IsNullOrWhiteSpace(text)) return text;
            string trimmed = text.Trim();
            Match named = _namedId.Match(trimmed);
            if (named.Success)
            {
                id = new ShortGuid(named.Groups["id"].Value.ToUpperInvariant());
                return named.Groups["name"].Value.Trim();
            }
            Match listed = _listedId.Match(trimmed);
            if (listed.Success)
            {
                id = new ShortGuid(listed.Groups["id"].Value.ToUpperInvariant());
                string rest = listed.Groups["name"].Value.Trim();
                int extra = rest.IndexOf(" (", StringComparison.Ordinal);
                return extra > 0 ? rest.Substring(0, extra) : rest;
            }
            return trimmed;
        }

        /// <summary>A ShortGuid written as a byte string or as its number; null if it is neither.</summary>
        public static ShortGuid? ParseId(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim();
            if (_byteString.IsMatch(text)) return new ShortGuid(text.ToUpperInvariant());
            if (uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint number) && number > 0xFFFF) return new ShortGuid(number);
            return null;
        }

        /// <summary>A parameter or pin name as it is hashed ("position"), or its id if the client gives one.</summary>
        public static ShortGuid ParamId(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new McpError("A parameter name is missing.");
            ShortGuid? id = ParseId(name);
            return id ?? ShortGuidUtils.Generate(name.Trim());
        }

        public static string ParamName(ShortGuid id) => ShortGuidUtils.FindString(id);

        public static string NormalisePath(string path) => (path ?? "").Replace('/', '\\').Trim().Trim('\\');

        #region Composites
        /// <summary>
        /// A composite from its id, its path (either slash, any case), "root" / "global" / "pausemenu", or
        /// the last part of its path when only one composite ends that way. A folder placeholder (a name ending in a
        /// separator, as manage_composites create_folder makes) is never taken for a composite of the same name: only a
        /// reference ending in a separator ('AYZ\MyFolder\'), or its id, finds the placeholder itself.
        /// </summary>
        public static Composite FindComposite(Commands commands, string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                throw McpError.Invalid("Say which composite: its path (e.g. 'AYZ\\Science\\Corridor'), its id, or 'root'.");
            string written = reference.Trim();
            bool askedForFolder = written.EndsWith("\\") || written.EndsWith("/");
            string wanted = NormalisePath(StripId(reference, out ShortGuid? shown));
            if (shown != null)
            {
                Composite byShownId = commands.GetComposite(shown.Value);
                if (byShownId != null) return byShownId;
            }

            switch (wanted.ToLowerInvariant())
            {
                case "root": return commands.EntryPoints[0];
                case "global": return commands.EntryPoints[1];
                case "pausemenu": return commands.EntryPoints[2];
            }

            ShortGuid? id = ParseId(wanted);
            if (id != null)
            {
                Composite byId = commands.GetComposite(id.Value);
                if (byId != null) return byId;
            }

            List<Composite> named = commands.Entries.Where(o => o != null && !string.IsNullOrEmpty(o.name)).ToList();
            List<Composite> folders = named.Where(IsFolder).ToList();
            if (askedForFolder)
            {
                Composite placeholder = folders.FirstOrDefault(o => string.Equals(NormalisePath(o.name), wanted, StringComparison.OrdinalIgnoreCase));
                if (placeholder != null) return placeholder;
            }
            //Placeholders hold no script: 'X\' must not answer for (or clash with) a composite called 'X'
            List<Composite> all = named.Where(o => !IsFolder(o)).ToList();
            List<Composite> exact = all.Where(o => string.Equals(NormalisePath(o.name), wanted, StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count == 1) return exact[0];
            if (exact.Count > 1)
                throw McpError.Ambiguous("composites", wanted, exact.Select(CompositeCandidate));

            List<Composite> ending = all.Where(o => NormalisePath(o.name).EndsWith("\\" + wanted, StringComparison.OrdinalIgnoreCase)).ToList();
            if (ending.Count == 1) return ending[0];
            if (ending.Count > 1)
                throw McpError.Ambiguous("composites", wanted, ending.Select(CompositeCandidate), "Give the full path or an id.");

            //The same name written another way ("medical bay" for Medical_Bay): case, spaces, '_' and '-' ignored, when only one fits
            string squashed = McpNames.Squash(wanted);
            if (squashed.Length != 0)
            {
                List<Composite> loose = all.Where(o => McpNames.Squash(o.name) == squashed || McpNames.Squash(McpNames.Leaf(NormalisePath(o.name))) == squashed).ToList();
                if (loose.Count == 1) return loose[0];
                if (loose.Count > 1)
                    throw McpError.Ambiguous("composites", wanted, loose.Select(CompositeCandidate), "Give the full path or an id.");
            }

            string hint = "find_composites searches paths by words.";
            Entity instance = null;
            Composite holder = null;
            foreach (Composite other in squashed.Length == 0 ? new List<Composite>() : all)
            {
                foreach (FunctionEntity function in other.functions)
                {
                    if (function.function.IsFunctionType || McpNames.Squash(commands.Utils.GetEntityName(other, function)) != squashed) continue;
                    instance = function;
                    holder = other;
                    break;
                }
                if (instance != null) break;
            }
            if (instance != null)
                hint = "'" + wanted + "' is the name of an instance (of " + (InstancedComposite(commands, instance)?.name ?? "?") + ") in " + holder.name + ": find_entities name:'" + wanted + "' lists such instances with their paths. " + hint;
            else
                hint += " A room may be named by an instance or a zone: find_places finds it by name.";
            Composite folder = folders.FirstOrDefault(o => string.Equals(NormalisePath(o.name), wanted, StringComparison.OrdinalIgnoreCase));
            if (folder != null)
                hint = "'" + folder.name + "' is a folder placeholder (an empty folder for the composite browser), not a composite: create_composite makes one inside it ('" + folder.name + "Name'). " + hint;
            throw McpError.NotFound("composite", wanted, all.Select(o => o.name), hint);
        }

        /// <summary>A folder placeholder: a composite entry whose name ends in a separator, which holds no script.</summary>
        public static bool IsFolder(Composite composite) => composite?.name != null && (composite.name.EndsWith("\\") || composite.name.EndsWith("/"));

        private static JObject CompositeCandidate(Composite composite) => new JObject() { ["id"] = Id(composite.shortGUID), ["name"] = composite.name };

        public static string CompositeLeaf(Composite composite) => EditorUtils.GetCompositeName(composite);

        public static JObject CompositeSummary(Commands commands, Composite composite)
        {
            JObject summary = new JObject()
            {
                ["id"] = Id(composite.shortGUID),
                ["path"] = composite.name,
            };
            if (composite == commands.EntryPoints[0]) summary["role"] = "root";
            else if (composite == commands.EntryPoints[1]) summary["role"] = "global";
            else if (composite == commands.EntryPoints[2]) summary["role"] = "pausemenu";
            if (IsFolder(composite)) summary["kind"] = "folder";
            return summary;
        }

        /// <summary>
        /// "The room called X" when X is not a composite path: the placements a name stands for - a zone, a composite's or an
        /// instance's placements, a model - ranked as find_places ranks them, an unclear name refused with the candidates.
        /// Its Roots are entity chains from the level root. The lookup region arguments use. UI thread.
        /// </summary>
        public static McpRegion FindPlacement(McpCall call, Commands commands, Level level, string name) =>
            McpRegion.Resolve(call, commands, level, new JObject() { ["name"] = name }, "place");
        #endregion

        #region Entities
        /// <summary>An entity of the composite from its id or its name (which must pick out one entity).</summary>
        public static Entity FindEntity(Commands commands, Composite composite, string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                throw McpError.Invalid("Say which entity in " + composite.name + ": its id or its name.");
            //"Name (id)" as results show it: the id decides, the name is only a label
            string wanted = StripId(reference, out ShortGuid? shown);
            if (shown != null)
            {
                Entity byShownId = composite.GetEntityByID(shown.Value);
                if (byShownId != null) return byShownId;
            }

            ShortGuid? id = ParseId(wanted);
            if (id != null)
            {
                Entity byId = composite.GetEntityByID(id.Value);
                if (byId != null) return byId;
            }

            List<Entity> entities = composite.GetEntities();
            List<Entity> named = entities.Where(o => string.Equals(EntityName(commands, composite, o), wanted, StringComparison.OrdinalIgnoreCase)).ToList();
            if (named.Count == 1) return named[0];
            if (named.Count > 1)
                throw McpError.Ambiguous("entities in " + composite.name, wanted, named.Select(o => EntityCandidate(commands, composite, o)));

            //The same name written another way ("alarm trigger" for Alarm_Trigger), when only one fits
            string squashed = McpNames.Squash(wanted);
            if (squashed.Length != 0)
            {
                List<Entity> loose = entities.Where(o => McpNames.Squash(EntityName(commands, composite, o)) == squashed).ToList();
                if (loose.Count == 1) return loose[0];
                if (loose.Count > 1)
                    throw McpError.Ambiguous("entities in " + composite.name, wanted, loose.Select(o => EntityCandidate(commands, composite, o)));
            }

            McpError missing = McpError.NotFound("entity", wanted, entities.Select(o => EntityName(commands, composite, o)),
                "(Looked in " + composite.name + ".) get_composite lists its entities; find_entities name:'" + wanted + "' searches the whole level.");
            //The near names as candidates a caller can pass straight back
            List<string> near = missing.Candidates?.Select(o => (string)o).ToList() ?? new List<string>();
            if (near.Count != 0)
                missing.Candidates = new JArray(entities.Where(o => near.Contains(EntityName(commands, composite, o), StringComparer.OrdinalIgnoreCase)).Take(8).Select(o => EntityCandidate(commands, composite, o)));
            throw missing;
        }

        private static JObject EntityCandidate(Commands commands, Composite composite, Entity entity) =>
            new JObject() { ["id"] = Id(entity.shortGUID), ["name"] = EntityName(commands, composite, entity), ["type"] = TypeName(commands, composite, entity) };

        public static string EntityName(Commands commands, Composite composite, Entity entity)
        {
            if (entity is VariableEntity variable)
                return ShortGuidUtils.FindString(variable.name);
            return commands.Utils.GetEntityName(composite, entity);
        }

        public static string Kind(Entity entity)
        {
            switch (entity.variant)
            {
                case EntityVariant.FUNCTION: return ((FunctionEntity)entity).function.IsFunctionType ? "function" : "instance";
                case EntityVariant.VARIABLE: return "variable";
                case EntityVariant.ALIAS: return "alias";
                case EntityVariant.PROXY: return "proxy";
            }
            return entity.variant.ToString().ToLowerInvariant();
        }

        /// <summary>What an entity is: its function type, the composite it instances, its pin type, or what it stands for.</summary>
        public static string TypeName(Commands commands, Composite composite, Entity entity)
        {
            switch (entity)
            {
                case FunctionEntity function:
                    if (function.function.IsFunctionType) return function.function.AsFunctionType.ToString();
                    return commands.GetComposite(function.function)?.name ?? ("missing composite " + Id(function.function));
                case VariableEntity variable:
                    CompositePinInfoTable.PinInfo info = commands.Utils.GetPinInfo(composite, variable);
                    return info != null ? info.PinTypeGUID.AsCompositePinType.ToString() : variable.type.ToString();
                case AliasEntity alias:
                    {
                        (Composite c, Entity e) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveAlias(alias, composite));
                        return e == null ? "alias (unresolved)" : "alias of " + TypeName(commands, c, e);
                    }
                case ProxyEntity proxy:
                    {
                        (Composite c, Entity e) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveProxy(proxy));
                        return e == null ? "proxy (dead)" : "proxy of " + TypeName(commands, c, e);
                    }
            }
            return entity.variant.ToString();
        }

        /// <summary>"a ..." or "an ..." for a type or kind in a message ("an alias of ModelReference", "a LogicGate").</summary>
        public static string WithArticle(string what) => (!string.IsNullOrEmpty(what) && "aeiouAEIOU".IndexOf(what[0]) >= 0 ? "an " : "a ") + what;

        /// <summary>A composite pin type as create_entities takes it ('input_float', 'method', 'output_zone_link'), from its enum name (CompositeInputFloatVariablePin).</summary>
        public static string PinTypeWord(string pinType)
        {
            if (string.IsNullOrEmpty(pinType) || !pinType.StartsWith("Composite", StringComparison.Ordinal))
                return pinType?.ToLowerInvariant();
            string core = pinType.Substring("Composite".Length);
            if (core.EndsWith("VariablePin", StringComparison.Ordinal)) core = core.Substring(0, core.Length - "VariablePin".Length);
            else if (core.EndsWith("Pin", StringComparison.Ordinal)) core = core.Substring(0, core.Length - "Pin".Length);
            core = core.Replace("Ptr", "");
            return Regex.Replace(core, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
        }

        /// <summary>The composite an instance entity places, or null.</summary>
        public static Composite InstancedComposite(Commands commands, Entity entity)
        {
            return entity is FunctionEntity function && !function.function.IsFunctionType ? commands.GetComposite(function.function) : null;
        }

        /// <summary>The steps of an alias's or proxy's path, as names.</summary>
        public static JArray DescribePath(Commands commands, List<Tuple<Composite, Entity>> resolved, ShortGuid[] stored)
        {
            JArray steps = new JArray();
            if (resolved != null && resolved.Count != 0)
            {
                foreach (Tuple<Composite, Entity> step in resolved)
                    steps.Add(step.Item2 == null ? "?" : EntityName(commands, step.Item1, step.Item2) + " (" + Id(step.Item2.shortGUID) + ")");
                return steps;
            }
            foreach (ShortGuid id in stored ?? new ShortGuid[0])
                if (id != ShortGuid.Invalid) steps.Add(Id(id));
            return steps;
        }

        /// <summary>
        /// An alias path from names: each step names an entity in the composite the previous step instances,
        /// starting in <paramref name="from"/>. Every step but the last must be a composite instance.
        /// </summary>
        public static ShortGuid[] ResolvePath(Commands commands, Composite from, IList<string> steps, out Composite targetComposite, out Entity target)
        {
            if (steps == null || steps.Count == 0)
                throw McpError.Invalid("A path needs at least one step.");
            //A path written from the root may start with 'root' itself, as a result's "from" says
            if (steps.Count > 1 && from == commands.EntryPoints[0] && string.Equals(steps[0]?.Trim(), "root", StringComparison.OrdinalIgnoreCase)
                && !from.GetEntities().Any(o => string.Equals(EntityName(commands, from, o), "root", StringComparison.OrdinalIgnoreCase)))
                steps = steps.Skip(1).ToList();
            List<ShortGuid> path = new List<ShortGuid>();
            Composite current = from;
            target = null;
            targetComposite = null;
            for (int i = 0; i < steps.Count; i++)
            {
                Entity step = FindEntity(commands, current, steps[i]);
                path.Add(step.shortGUID);
                if (i == steps.Count - 1)
                {
                    target = step;
                    targetComposite = current;
                    break;
                }
                Composite next = InstancedComposite(commands, step);
                if (next == null)
                    throw McpError.Invalid("'" + steps[i] + "' in " + current.name + " is " + WithArticle(TypeName(commands, current, step)) + ", not a composite instance, so the path cannot go through it (every step but the last must be an instance).");
                current = next;
            }
            path.Add(ShortGuid.Invalid);
            return path.ToArray();
        }

        /// <summary>
        /// The steps of a path of entities, as a client may write one: an array of ids or names (or "Name (id)" steps, as results
        /// show them), one string split on '/', '\' or '>', or a result's own path object - {"ids": [...]} or {"path": [...], "ids": [...]},
        /// ids taken first. A leading 'root' step is dropped by <see cref="ResolvePath"/> when the path runs from the root.
        /// </summary>
        public static List<string> PathSteps(JToken token, string what = "path")
        {
            if (token is JObject obj)
            {
                JToken ids = obj["ids"] ?? obj["path_ids"];
                if (ids is JArray && ((JArray)ids).Count != 0) return PathSteps(ids, what);
                if (obj["path"] != null && !(obj["path"] is JObject)) return PathSteps(obj["path"], what);
                throw McpError.Invalid("'" + what + "' as an object takes 'ids' (or 'path'): the shape results give a placement in, e.g. {\"ids\": [\"01-02-03-04\", ...]}.");
            }
            if (token is JArray array && array.Count != 0)
            {
                if (array.Any(o => o.Type != JTokenType.String && o.Type != JTokenType.Integer))
                    throw McpError.Invalid("'" + what + "' is a list of entity ids or names (text), e.g. [\"Door_1\", \"Keypad\"].");
                return array.Select(o => McpValues.ReadString(o).Trim()).ToList();
            }
            if (token?.Type == JTokenType.String && ((string)token).Trim().Length != 0)
                return ((string)token).Split(new[] { '/', '\\', '>' }, StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim()).Where(o => o.Length != 0).ToList();
            throw McpError.Invalid("'" + what + "' is a list of entity ids or names from the root composite through instances, e.g. [\"Door_1\", \"Keypad\"] (any 'ids' array a result gives can be passed back as it is).");
        }

        /// <summary>The entities a path from <paramref name="from"/> steps through, the last being its target. Every step but the last must be an instance.</summary>
        public static List<Entity> ChainFrom(Commands commands, Composite from, IList<string> steps)
        {
            ResolvePath(commands, from, steps, out Composite _, out Entity _);
            if (steps.Count > 1 && from == commands.EntryPoints[0] && string.Equals(steps[0]?.Trim(), "root", StringComparison.OrdinalIgnoreCase)
                && !from.GetEntities().Any(o => string.Equals(EntityName(commands, from, o), "root", StringComparison.OrdinalIgnoreCase)))
                steps = steps.Skip(1).ToList();
            List<Entity> chain = new List<Entity>();
            Composite current = from;
            foreach (string step in steps)
            {
                Entity entity = FindEntity(commands, current, step);
                chain.Add(entity);
                current = InstancedComposite(commands, entity);
            }
            return chain;
        }

        /// <summary>The composites a chain from <paramref name="from"/> runs through: [0] is <paramref name="from"/>, [i + 1] the one chain[i] instances.</summary>
        public static List<Composite> CompositesAlong(Commands commands, Composite from, IList<Entity> chain)
        {
            List<Composite> composites = new List<Composite>() { from };
            foreach (Entity entity in chain)
                composites.Add(InstancedComposite(commands, entity));
            return composites;
        }

        /// <summary>
        /// Everything about an entity a client would want: what it is, its parameters (and which of them a link feeds), its links.
        /// The long lists - sequence entries, links out and in - are paged by <paramref name="limit"/> and <paramref name="offset"/>,
        /// with each list's total given when it is cut. When any list has more: next_offset if <paramref name="paged"/> (the
        /// tool takes limit/offset for these lists), otherwise a lists_note, so a tool paging something else keeps its own next_offset.
        /// </summary>
        public static JObject Describe(Commands commands, Composite composite, Entity entity, bool parameters = true, bool links = true, bool defaults = false, int limit = 200, int offset = 0, bool paged = false)
        {
            JObject result = new JObject()
            {
                ["id"] = Id(entity.shortGUID),
                ["name"] = EntityName(commands, composite, entity),
                ["kind"] = Kind(entity),
                ["type"] = TypeName(commands, composite, entity),
            };
            limit = Math.Max(1, limit);
            offset = Math.Max(0, offset);
            bool more = false;
            JArray Page<T>(IList<T> items, Func<T, JToken> select, string totalField)
            {
                List<T> page = items.Skip(offset).Take(limit).ToList();
                if (page.Count != items.Count) result[totalField] = items.Count;
                if (offset + page.Count < items.Count) more = true;
                return new JArray(page.Select(select));
            }

            switch (entity)
            {
                case VariableEntity variable:
                    result["data_type"] = variable.type.ToString();
                    result["pin_type"] = PinTypeWord((string)result["type"]);
                    break;
                case AliasEntity alias:
                    {
                        List<Tuple<Composite, Entity>> resolved = commands.Utils.ResolveAlias(alias, composite);
                        result["path"] = DescribePath(commands, resolved, alias.alias?.path);
                        DescribeTarget(commands, result, resolved, alias, parameters);
                    }
                    break;
                case ProxyEntity proxy:
                    {
                        List<Tuple<Composite, Entity>> resolved = commands.Utils.ResolveProxy(proxy);
                        result["path"] = DescribePath(commands, resolved, proxy.proxy?.path);
                        result["path_from"] = "root";
                        DescribeTarget(commands, result, resolved, proxy, parameters);
                    }
                    break;
            }

            //A trigger sequence's entries and methods are not parameters: show them too
            List<TriggerSequence.SequenceEntry> sequence = (entity as TriggerSequence)?.sequence ?? (entity is ProxyEntity p && p.function == FunctionType.TriggerSequence ? p.sequence : null);
            List<TriggerSequence.MethodEntry> methods = (entity as TriggerSequence)?.methods ?? (entity is ProxyEntity q && q.function == FunctionType.TriggerSequence ? q.methods : null);
            if (sequence != null)
            {
                result["sequence"] = Page(sequence, o => new JObject()
                {
                    ["path"] = DescribePath(commands, commands.Utils.ResolveEntityPath(o.connectedEntity, composite), o.connectedEntity?.path),
                    ["delay"] = Math.Round((double)o.timing, 4),
                }, "sequence_count");
                result["methods"] = new JArray(methods.Select(o => ParamName(o.method)));
            }
            if (entity is CAGEAnimation animation)
                result["animation"] = new JObject() { ["connections"] = animation.connections.Count, ["float_tracks"] = animation.floatTracks.Count, ["event_tracks"] = animation.eventTracks.Count };

            //Pins it has from its data rather than its type (trigger methods with their relay/finished pins, animation events): linkable too
            HashSet<ShortGuid> dataPins = NodeUtils.GetDynamicPinParameters(entity, composite, commands);
            if (dataPins.Count != 0)
                result["event_pins"] = new JArray(dataPins.Select(ParamName).OrderBy(o => o, StringComparer.OrdinalIgnoreCase));

            if (parameters)
            {
                JObject values = new JObject();
                foreach (Parameter parameter in entity.parameters)
                {
                    if (parameter.name == ShortGuids.name) continue;
                    values[ParamName(parameter.name)] = McpValues.ToJson(parameter.content, commands);
                }
                result["parameters"] = values;

                //What instancing reads instead of a set value: the other end of a link out of the parameter
                JObject fed = FedBy(commands, composite, entity);
                if (fed != null)
                    result["parameters_fed_by"] = fed;

                if (defaults)
                {
                    //On an alias or proxy these are its target TYPE's defaults, not what the target has (that is 'target.values')
                    bool pointer = entity is AliasEntity || entity is ProxyEntity;
                    JArray available = new JArray();
                    foreach ((ShortGuid name, ParameterVariant variant, DataType type) in commands.Utils.GetAllParameters(entity, composite))
                    {
                        if (entity.GetParameter(name) != null) continue;
                        JObject pin = McpValues.DescribePin(commands, name, variant, type, () => commands.Utils.CreateDefaultParameterData(entity, composite, name));
                        if (pointer && pin["default"] != null)
                        {
                            pin["type_default"] = pin["default"];
                            pin.Remove("default");
                        }
                        available.Add(pin);
                    }
                    result["other_parameters"] = available;

                    //The relays its methods fire (LogicCounter's Up fires on_Up): link sources, though not parameters
                    Dictionary<ShortGuid, ShortGuid> relays = NodeUtils.GetMethodRelays(entity, composite, commands);
                    if (relays.Count != 0)
                        result["method_relays"] = new JObject(relays.Select(o => new JProperty(ParamName(o.Value), ParamName(o.Key))));
                }
            }

            if (links)
            {
                List<EntityConnector> outgoing = entity.childLinks;
                result["links_out"] = Page(outgoing, link =>
                {
                    Entity other = composite.GetEntityByID(link.linkedEntityID);
                    return new JObject()
                    {
                        ["param"] = ParamName(link.thisParamID),
                        ["to"] = Id(link.linkedEntityID),
                        ["to_name"] = other == null ? "(missing)" : EntityName(commands, composite, other),
                        ["to_param"] = ParamName(link.linkedParamID),
                    };
                }, "links_out_count");
                List<(Entity other, EntityConnector link)> incoming = new List<(Entity, EntityConnector)>();
                foreach (Entity other in composite.GetEntities())
                    foreach (EntityConnector link in other.childLinks)
                        if (link.linkedEntityID == entity.shortGUID)
                            incoming.Add((other, link));
                //'param' (its own pin, as in links_out) is kept beside to_param for clients written before the rename
                result["links_in"] = Page(incoming, o => new JObject()
                {
                    ["from"] = Id(o.other.shortGUID),
                    ["from_name"] = EntityName(commands, composite, o.other),
                    ["from_param"] = ParamName(o.link.thisParamID),
                    ["to_param"] = ParamName(o.link.linkedParamID),
                    ["param"] = ParamName(o.link.linkedParamID),
                }, "links_in_count");
            }
            if (more && paged)
                result["next_offset"] = offset + limit;
            else if (more)
                result["lists_note"] = "Lists longer than " + limit + " are cut (their *_count gives the total): describe_entity pages them with limit/offset.";
            return result;
        }

        /// <summary>An alias's or proxy's target, with the target's own values and which of them the pointer overrides.</summary>
        private static void DescribeTarget(Commands commands, JObject result, List<Tuple<Composite, Entity>> resolved, Entity pointer, bool parameters)
        {
            (Composite composite, Entity entity) = commands.Utils.GetResolvedTarget(resolved);
            if (entity == null || composite == null)
            {
                result["target"] = "nothing: the path does not resolve";
                return;
            }
            JObject target = new JObject()
            {
                ["composite"] = composite.name,
                ["id"] = Id(entity.shortGUID),
                ["name"] = EntityName(commands, composite, entity),
                ["type"] = TypeName(commands, composite, entity),
            };
            if (parameters)
            {
                JObject values = new JObject();
                foreach (Parameter parameter in entity.parameters)
                    if (parameter.name != ShortGuids.name)
                        values[ParamName(parameter.name)] = McpValues.ToJson(parameter.content, commands);
                target["values"] = values;
                List<string> overridden = pointer.parameters.Where(o => o.name != ShortGuids.name).Select(o => ParamName(o.name)).ToList();
                if (overridden.Count != 0)
                    target["overridden_here"] = new JArray(overridden);
            }
            result["target"] = target;
        }

        /// <summary>Parameter kinds that hold a value (and so can be fed one by a link), as opposed to methods, relays and references.</summary>
        public static bool HoldsValue(ParameterVariant? variant) =>
            variant == ParameterVariant.PARAMETER || variant == ParameterVariant.INPUT_PIN || variant == ParameterVariant.STATE_PARAMETER || variant == ParameterVariant.INTERNAL;

        /// <summary>
        /// The parameters of an entity a link feeds, as instancing reads them: a value parameter with a link OUT of it takes its value
        /// from the other end of that link (the last such link wins), whatever value is set on it. Null when none are fed.
        /// </summary>
        public static JObject FedBy(Commands commands, Composite composite, Entity entity)
        {
            if (entity is VariableEntity || entity.childLinks.Count == 0)
                return null;
            JObject fed = null;
            foreach (IGrouping<ShortGuid, EntityConnector> group in entity.childLinks.GroupBy(o => o.thisParamID))
            {
                (ParameterVariant? variant, DataType? type, ShortGuid _) = commands.Utils.GetParameterMetadata(entity, group.Key, composite);
                if (!HoldsValue(variant)) continue;
                List<EntityConnector> feeding = group.Where(o => composite.GetEntityByID(o.linkedEntityID) != null).ToList();
                if (feeding.Count == 0) continue;
                bool pointer = type != null && CommandsUtils.IsPointerType(type.Value);
                string text = string.Join("; ", feeding.Select(o => DescribeFeed(commands, composite, o, pointer)));
                if (feeding.Count > 1)
                    text += " (" + feeding.Count + " links: the last wins)";
                if (fed == null) fed = new JObject();
                fed[ParamName(group.Key)] = text;
            }
            return fed;
        }

        private static string DescribeFeed(Commands commands, Composite composite, EntityConnector link, bool pointer)
        {
            Entity other = composite.GetEntityByID(link.linkedEntityID);
            if (other is VariableEntity variable)
                return "composite pin '" + ShortGuidUtils.FindString(variable.name) + "': the value comes from each instance of " + composite.name + " (or an alias overriding it) - set it there";
            string end = EntityName(commands, composite, other) + "." + ParamName(link.linkedParamID) + " (" + Id(other.shortGUID) + ")";
            //A Variable* function's reference gives the value it holds (its initial_value to start with), not a pointer to it
            ShortGuid[] held = !pointer && link.linkedParamID == ShortGuids.reference ? McpEffective.VariableValue(commands, composite, other) : null;
            if (held != null)
                return end + ": the value " + TypeName(commands, composite, other) + " " + EntityName(commands, composite, other) + " holds (its " + string.Join("/", held.Select(ParamName)) + " to start with; set it there), read at run time";
            return end + (pointer || link.linkedParamID == ShortGuids.reference ? ", which it points at" : ", read at run time");
        }

        /// <summary>One line per entity, for listings.</summary>
        public static JObject Brief(Commands commands, Composite composite, Entity entity)
        {
            JObject brief = new JObject()
            {
                ["id"] = Id(entity.shortGUID),
                ["name"] = EntityName(commands, composite, entity),
                ["kind"] = Kind(entity),
                ["type"] = TypeName(commands, composite, entity),
            };
            if (entity.GetParameter(ShortGuids.position)?.content is cTransform transform)
            {
                brief["position"] = McpValues.Vector(transform.position);
                if (transform.rotation != Vector3.Zero)
                    brief["rotation"] = McpValues.Vector(transform.rotation);
            }
            return brief;
        }
        #endregion
    }

    /// <summary>
    /// What a parameter of an entity really is in one placement, worked out as instancing does: a link out of the parameter
    /// (its own, then its aliases', the last one winning) ahead of any value; the outermost alias's value ahead of the entity's
    /// own; a composite pin followed up into the instance that places the composite; then the type's default.
    /// </summary>
    /// <remarks>
    /// A placement is a chain of entities with the composites holding them: comps[i] holds chain[i], comps[0] is where the chain
    /// starts (the root, for a placement in the level), and chain[i] instances comps[i + 1]. Aliases are applied innermost first
    /// and outer ones overwrite, as Instancing.GenerateInstances applies them.
    /// </remarks>
    internal static class McpEffective
    {
        public sealed class Resolved
        {
            /// <summary>The value (null when nothing gives it one and the type has no default).</summary>
            public JToken Value;
            /// <summary>
            /// own, alias, pin (a composite pin passed in from outside), link (another entity, read at run time), variable (a
            /// Variable* function the parameter links to: its initial value) or default.
            /// </summary>
            public string Source;
            /// <summary>Where the value came from, in a line.</summary>
            public string From;
            /// <summary>Each step taken, outermost reason first.</summary>
            public List<string> Route = new List<string>();
        }

        /// <summary>The aliases that reach chain[k], innermost first (the order instancing applies them in, so later ones win).</summary>
        public static List<(Composite owner, AliasEntity alias)> AliasesOf(List<Composite> comps, List<Entity> chain, int k)
        {
            List<(Composite, AliasEntity)> found = new List<(Composite, AliasEntity)>();
            for (int j = k; j >= 0; j--)
            {
                int length = k - j + 1;
                foreach (AliasEntity alias in comps[j].aliases)
                {
                    ShortGuid[] path = alias.alias?.path;
                    if (path == null) continue;
                    int stored = path.Length > 0 && path[path.Length - 1] == ShortGuid.Invalid ? path.Length - 1 : path.Length;
                    if (stored != length) continue;
                    bool same = true;
                    for (int i = 0; i < length && same; i++)
                        same = path[i] == chain[j + i].shortGUID;
                    if (same) found.Add((comps[j], alias));
                }
            }
            return found;
        }

        /// <summary>One parameter of chain[k] in this placement.</summary>
        public static Resolved Of(Commands commands, List<Composite> comps, List<Entity> chain, int k, ShortGuid parameter, int depth = 0)
        {
            Resolved resolved = new Resolved();
            Composite composite = comps[k];
            Entity entity = chain[k];
            string entityName = McpScript.EntityName(commands, composite, entity);
            List<(Composite owner, AliasEntity alias)> aliases = AliasesOf(comps, chain, k);

            //A link out of the parameter feeds it (own links first, then each alias's): the last one wins over any value
            (ParameterVariant? variant, DataType? type, ShortGuid _) = commands.Utils.GetParameterMetadata(entity, parameter, composite);
            if (McpScript.HoldsValue(variant) && depth < 24)
            {
                (int level, Entity holder, EntityConnector link)? last = null;
                foreach (EntityConnector link in entity.childLinks)
                    if (link.thisParamID == parameter && composite.GetEntityByID(link.linkedEntityID) != null)
                        last = (k, entity, link);
                foreach ((Composite owner, AliasEntity alias) in aliases)
                    foreach (EntityConnector link in alias.childLinks)
                        if (link.thisParamID == parameter && owner.GetEntityByID(link.linkedEntityID) != null)
                            last = (comps.IndexOf(owner), alias, link);
                if (last != null)
                {
                    int level = last.Value.level;
                    Composite holderComposite = comps[level];
                    Entity other = holderComposite.GetEntityByID(last.Value.link.linkedEntityID);
                    string by = last.Value.holder is AliasEntity ? "alias " + McpScript.EntityName(commands, holderComposite, last.Value.holder) + " (" + McpScript.Id(last.Value.holder.shortGUID) + ") in " + holderComposite.name : entityName;
                    if (other is VariableEntity variable)
                    {
                        string pin = ShortGuidUtils.FindString(variable.name);
                        resolved.Route.Add(by + " links it to " + holderComposite.name + "'s pin '" + pin + "'");
                        if (level > 0)
                        {
                            Resolved passed = Of(commands, comps, chain, level - 1, variable.name, depth + 1);
                            resolved.Route.AddRange(passed.Route);
                            resolved.Value = passed.Value;
                            resolved.Source = passed.Source == "default" ? "pin" : passed.Source;
                            resolved.From = "pin '" + pin + "' of " + holderComposite.name + " <- " + passed.From;
                            return resolved;
                        }
                        ParameterData fallback = variable.GetParameter(variable.name)?.content;
                        resolved.Value = fallback == null ? null : McpValues.ToJson(fallback, commands);
                        resolved.Source = "pin";
                        resolved.From = "pin '" + pin + "' of " + holderComposite.name + " (its own default: nothing places it here)";
                        return resolved;
                    }

                    //A Variable* function linked by its reference (NPC_Logic.aliance_group -> ALLIANCE_GROUP.reference) gives the value it
                    //holds, worked out for this placement as any other parameter is (an alias above may override it, a pin may pass it in)
                    ShortGuid[] held = last.Value.link.linkedParamID == ShortGuids.reference ? VariableValue(commands, holderComposite, other) : null;
                    if (held != null)
                    {
                        List<Entity> variableChain = chain.Take(level).Concat(new[] { other }).ToList();
                        string provider = McpScript.TypeName(commands, holderComposite, other) + " " + McpScript.EntityName(commands, holderComposite, other) + " (" + McpScript.Id(other.shortGUID) + ")";
                        resolved.Route.Add(by + " links it to " + provider + " in " + holderComposite.name + ", which gives its " + string.Join("/", held.Select(McpScript.ParamName)));
                        List<Resolved> parts = held.Select(o => Of(commands, comps, variableChain, level, o, depth + 1)).ToList();
                        foreach (Resolved part in parts)
                            resolved.Route.AddRange(part.Route);
                        if (parts.Count == 1)
                            resolved.Value = parts[0].Value;
                        else
                            resolved.Value = new JArray(parts.Select(o => o.Value != null && (o.Value.Type == JTokenType.Float || o.Value.Type == JTokenType.Integer) ? o.Value : (JToken)0));
                        resolved.Source = "variable";
                        resolved.From = provider + " in " + holderComposite.name + ": " + (parts.Count == 1
                            ? McpScript.ParamName(held[0]) + " " + parts[0].From
                            : string.Join("; ", parts.Select((o, i) => McpScript.ParamName(held[i]) + " " + o.From)));
                        return resolved;
                    }
                    string linked = McpScript.EntityName(commands, holderComposite, other) + "." + McpScript.ParamName(last.Value.link.linkedParamID);
                    ParameterData current = other.GetParameter(last.Value.link.linkedParamID)?.content;
                    resolved.Route.Add(by + " links it to " + linked + " (" + McpScript.Id(other.shortGUID) + ") in " + holderComposite.name);
                    resolved.Value = current == null ? null : McpValues.ToJson(current, commands);
                    resolved.Source = "link";
                    resolved.From = last.Value.link.linkedParamID == ShortGuids.reference
                        ? "points at " + McpScript.EntityName(commands, holderComposite, other) + " (" + McpScript.Id(other.shortGUID) + ", " + McpScript.TypeName(commands, holderComposite, other) + ")"
                        : "read at run time from " + linked + " (" + McpScript.Id(other.shortGUID) + ")" + (current == null ? "" : ", which holds this value");
                    return resolved;
                }
            }

            //Values: the outermost alias's, else the entity's own
            for (int i = aliases.Count - 1; i >= 0; i--)
            {
                ParameterData overriding = aliases[i].alias.GetParameter(parameter)?.content;
                if (overriding == null) continue;
                resolved.Value = McpValues.ToJson(overriding, commands);
                resolved.Source = "alias";
                resolved.From = "alias " + McpScript.EntityName(commands, aliases[i].owner, aliases[i].alias) + " (" + McpScript.Id(aliases[i].alias.shortGUID) + ") in " + aliases[i].owner.name;
                resolved.Route.Add(resolved.From + " overrides it");
                return resolved;
            }
            ParameterData own = entity.GetParameter(parameter)?.content;
            if (own != null)
            {
                resolved.Value = McpValues.ToJson(own, commands);
                resolved.Source = "own";
                resolved.From = "set on " + entityName + " in " + composite.name;
                return resolved;
            }
            ParameterData standard = null;
            try { standard = commands.Utils.CreateDefaultParameterData(entity, composite, parameter); } catch { }
            resolved.Value = standard == null ? null : McpValues.ToJson(standard, commands);
            resolved.Source = "default";
            Composite placed = McpScript.InstancedComposite(commands, entity);
            resolved.From = standard == null ? "not set (no default)" : placed != null ? placed.name + "'s own default for the pin" : "the type's default";
            return resolved;
        }

        /// <summary>
        /// What a Variable* function hands a link to its 'reference' pin, as instancing reads it: initial_value (VariableBool, Enum,
        /// Float, Int, String, Vector2), initial_colour (VariableColour, VariableFlashScreenColour) or initial_x/y/z (VariableVector).
        /// Null for anything else (VariableThePlayer, VariablePosition, the object holders...): a link to its reference points at it.
        /// </summary>
        public static ShortGuid[] VariableValue(Commands commands, Composite composite, Entity entity)
        {
            if (!(entity is FunctionEntity function) || !function.function.IsFunctionType)
                return null;
            switch (function.function.AsFunctionType)
            {
                case FunctionType.VariableColour:
                case FunctionType.VariableFlashScreenColour:
                    return new[] { ShortGuids.initial_colour };
                case FunctionType.VariableVector:
                    return new[] { ShortGuids.initial_x, ShortGuids.initial_y, ShortGuids.initial_z };
                case FunctionType.VariableBool:
                case FunctionType.VariableEnum:
                case FunctionType.VariableFloat:
                case FunctionType.VariableInt:
                case FunctionType.VariableString:
                case FunctionType.VariableVector2:
                    return new[] { ShortGuids.initial_value };
                case FunctionType.VariableEnumString:
                    //Not one instancing names: take its initial_value when its type has one
                    return commands.Utils.GetParameterMetadata(entity, ShortGuids.initial_value, composite).Item1 != null ? new[] { ShortGuids.initial_value } : null;
            }
            return null;
        }

        /// <summary>The parameters worth listing for chain[k]: those set on it, overridden by an alias or fed by a link, and any asked for.</summary>
        public static List<ShortGuid> Interesting(Commands commands, List<Composite> comps, List<Entity> chain, int k, bool all)
        {
            Composite composite = comps[k];
            Entity entity = chain[k];
            List<ShortGuid> names = new List<ShortGuid>();
            void Add(ShortGuid id) { if (id != ShortGuids.name && !names.Contains(id)) names.Add(id); }
            foreach (Parameter parameter in entity.parameters) Add(parameter.name);
            foreach ((Composite owner, AliasEntity alias) in AliasesOf(comps, chain, k))
            {
                foreach (Parameter parameter in alias.parameters) Add(parameter.name);
                foreach (EntityConnector link in alias.childLinks) Add(link.thisParamID);
            }
            foreach (EntityConnector link in entity.childLinks) Add(link.thisParamID);
            List<(ShortGuid, ParameterVariant, DataType)> schema = commands.Utils.GetAllParameters(entity, composite);
            HashSet<ShortGuid> values = new HashSet<ShortGuid>(schema.Where(o => McpScript.HoldsValue(o.Item2)).Select(o => o.Item1));
            if (all)
                foreach (ShortGuid id in values) Add(id);
            //Links out of relays and methods feed nothing
            return names.Where(o => values.Contains(o) || entity.GetParameter(o) != null).ToList();
        }

        public static JObject Json(Resolved resolved)
        {
            JObject json = new JObject() { ["value"] = resolved.Value ?? JValue.CreateNull(), ["source"] = resolved.Source, ["from"] = resolved.From };
            if (resolved.Route.Count > 1)
                json["route"] = new JArray(resolved.Route);
            return json;
        }
    }

    /// <summary>Parameter values to JSON and back.</summary>
    internal static class McpValues
    {
        private static CommandsUtils _vanilla;

        /// <summary>
        /// CommandsUtils over the vanilla tables alone: a function type's pins, defaults, relays and inheritance and the enums,
        /// which need no level open. Not for anything about entities or composites.
        /// </summary>
        public static CommandsUtils Vanilla => _vanilla ?? (_vanilla = new CommandsUtils(null));

        private static CommandsUtils Enums(Commands commands) => commands?.Utils ?? Vanilla;

        //VECTOR parameters the inspector edits with a colour picker, and the tints named elsewhere; all 0-255 per channel
        private static readonly HashSet<string> _colours = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AMBIENT_LIGHTING_COLOUR", "COLOUR_TINT_START", "COLOUR_TINT_MID", "COLOUR_TINT_END",
            "COLOUR_TINT", "COLOUR_TINT_OUTER", "DEPTH_INTERSECT_COLOUR_VALUE", "DEPTH_INTERSECT_INITIAL_COLOUR",
            "DEPTH_INTERSECT_MIDPOINT_COLOUR", "DEPTH_INTERSECT_END_COLOUR", "DEPTH_FOG_INITIAL_COLOUR",
            "DEPTH_FOG_MIDPOINT_COLOUR", "DEPTH_FOG_END_COLOUR", "ColourFactor", "lens_flare_colour",
            "light_shaft_colour", "initial_colour", "near_colour", "far_colour", "colour",
            "emissive_tint", "lightdecal_tint", "diffuse_colour_scale", "volume_colour_factor",
        };

        /// <summary>
        /// Whether a VECTOR parameter is a colour ([r, g, b], 0-255 per channel): the inspector's colour parameters, the tints, and
        /// pins whose name says colour. vertex_colour_scale is not one (it runs 0-1).
        /// </summary>
        public static bool IsColour(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Equals("vertex_colour_scale", StringComparison.OrdinalIgnoreCase)) return false;
            if (_colours.Contains(name)) return true;
            List<string> words = McpNames.Words(name);
            return words.Contains("colour") || words.Contains("color") || words.Contains("col");
        }

        public static JArray Vector(Vector3 v) => new JArray(Round(v.X), Round(v.Y), Round(v.Z));

        private static double Round(float value) => Math.Round((double)value, 5);

        public static JToken ToJson(ParameterData data, Commands commands, Models models = null)
        {
            switch (data)
            {
                case null: return JValue.CreateNull();
                case cBool b: return b.value;
                case cInteger i: return i.value;
                case cFloat f: return Round(f.value);
                case cEnumString es: return es.value;
                case cString s: return s.value;
                case cVector3 v: return Vector(v.value);
                case cTransform t: return new JObject() { ["position"] = Vector(t.position), ["rotation"] = Vector(t.rotation) };
                case cEnum e:
                    {
                        CathodeEnumTable.EnumDescriptor descriptor = Enums(commands).GetEnum(e.enumID);
                        string entry = descriptor?.Entries.FirstOrDefault(o => o.Index == e.enumIndex)?.Name;
                        return entry ?? (JToken)e.enumIndex;
                    }
                case cSpline spline:
                    return new JObject() { ["points"] = new JArray(spline.splinePoints.Select(o => ToJson(o, commands, models))) };
                case cResource resource:
                    {
                        //The open level's own resources can be named as its material tools name them
                        Level open = Singleton.Editor?.CompositeBrowser?.Content?.Level;
                        Level level = open != null && open.Commands == commands ? open : null;
                        //A material "mapping" parameter holds no resources, only the id of one of the level's mapping sets
                        if ((resource.value == null || resource.value.Count == 0) && resource.shortGUID != ShortGuid.Invalid)
                        {
                            string mapping = level?.MaterialMappings?.Entries?.FirstOrDefault(o => o.ID == resource.shortGUID)?.Name;
                            if (mapping != null) return mapping;
                            return new JObject() { ["resource"] = new JArray(), ["id"] = McpScript.Id(resource.shortGUID) };
                        }
                        JArray references = new JArray();
                        foreach (ResourceReference reference in resource.value ?? new List<ResourceReference>())
                        {
                            JObject item = new JObject() { ["type"] = reference.resource_type.ToString() };
                            if (reference.resource_type == ResourceType.RENDERABLE_INSTANCE)
                                item["models"] = new JArray(reference.RenderableInstance.Select(o => DescribeRenderable(o, models, level)).Distinct().Take(20));
                            references.Add(item);
                        }
                        return new JObject() { ["resource"] = references };
                    }
            }
            return data.dataType.ToString();
        }

        /// <summary>
        /// "model [material]": the material as the material tools take it back (its display name, "name#index" when another
        /// material shows the same name) when the level is the open one, else its stored name.
        /// </summary>
        private static string DescribeRenderable(RenderableElements.Element element, Models models, Level level)
        {
            string model = element.Model == null ? "?" : McpAssets.SubmeshName(element.Model, models);
            string material = element.Material == null ? "?" : element.Material.Name;
            if (element.Material != null && level != null)
            {
                try { material = McpAssets.MaterialRef(level, element.Material) ?? material; }
                catch (Exception) { }
            }
            return model + " [" + material + "]";
        }

        /// <summary>A pin of an entity or function type, for listings: its name, kind, type and default.</summary>
        public static JObject DescribePin(Commands commands, ShortGuid name, ParameterVariant variant, DataType type, Func<ParameterData> defaultValue)
        {
            JObject pin = new JObject()
            {
                ["name"] = McpScript.ParamName(name),
                ["kind"] = PinKind(variant),
            };
            bool valueless = variant == ParameterVariant.METHOD_PIN || variant == ParameterVariant.METHOD_FUNCTION || variant == ParameterVariant.TARGET_PIN || variant == ParameterVariant.REFERENCE_PIN;
            if (!valueless)
            {
                ParameterData fallback = null;
                try { fallback = defaultValue?.Invoke(); } catch { }
                pin["type"] = fallback is cEnum ce ? "enum " + (Enums(commands).GetEnum(ce.enumID)?.Name ?? McpScript.Id(ce.enumID))
                            : fallback is cEnumString ces ? "enum_string " + ShortGuidUtils.FindString(ces.enumID)
                            : type == DataType.VECTOR && IsColour(McpScript.ParamName(name)) ? "colour (RGB 0-255)"
                            : type.ToString().ToLowerInvariant();
                if (fallback != null)
                    pin["default"] = ToJson(fallback, commands);
                else if (CommandsUtils.IsPointerType(type) || type == DataType.RESOURCE || type == DataType.SPLINE)
                    pin["link_only"] = true;
                //What a link does to it: the entity using a value owns the link, from its pin OUT to what provides it - the opposite
                //way to an event. A link into a value pin is refused.
                if (variant == ParameterVariant.INPUT_PIN || variant == ParameterVariant.PARAMETER || variant == ParameterVariant.STATE_PARAMETER)
                {
                    string own = McpScript.ParamName(name);
                    pin["link"] = type == DataType.ZONE || type == DataType.ZONE_LINK || type == DataType.OBJECT
                        ? "from this pin to the 'reference' pin of the entity it points at (or to a pin variable of its composite that passes one in)"
                        : own + " -> provider";
                }
            }
            return pin;
        }

        /// <summary>The one-line account of link direction that goes with <see cref="DescribePin"/>'s 'link' hints.</summary>
        public const string LinkDirections =
            "Events: a relay or target pin links to a method pin ('A.finished -> B.trigger'). Values: the entity that uses a value owns the link, " +
            "from its own pin OUT to what provides it ('Light.colour -> Source.reference' for a Variable* or other holder, the output pin of one that computes it, " +
            "or a pin variable of the composite to take it from each instance) - each value pin's 'link' shows the form. A link into a value pin is refused.";

        public static string PinKind(ParameterVariant variant)
        {
            switch (variant)
            {
                case ParameterVariant.METHOD_PIN: return "method";
                case ParameterVariant.METHOD_FUNCTION: return "method_function";
                case ParameterVariant.TARGET_PIN: return "target";
                case ParameterVariant.REFERENCE_PIN: return "reference";
                case ParameterVariant.INPUT_PIN: return "input";
                case ParameterVariant.OUTPUT_PIN: return "output";
                case ParameterVariant.STATE_PARAMETER: return "state";
                case ParameterVariant.INTERNAL: return "internal";
                case ParameterVariant.PARAMETER: return "parameter";
            }
            return variant.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// The value a JSON token describes, as the kind of data <paramref name="template"/> is (the parameter's
        /// current value or its default). With no template the kind is guessed from the JSON.
        /// </summary>
        public static ParameterData FromJson(JToken value, ParameterData template, string parameterName, Commands commands)
        {
            if (value == null || value.Type == JTokenType.Null)
                throw new McpError("No value given for '" + parameterName + "'.");

            //The editor treats any parameter called "mapping" as a composite's material mapping set, picked by name
            if (string.Equals(parameterName, "mapping", StringComparison.OrdinalIgnoreCase) && value.Type == JTokenType.String)
                return ReadMapping((string)value, commands);

            switch (template)
            {
                case null: return Guess(value, parameterName);
                case cBool _: return new cBool(ReadBool(value, parameterName));
                case cInteger _: return new cInteger(ReadInt(value, parameterName));
                case cFloat _: return new cFloat((float)ReadDouble(value, parameterName));
                case cEnumString es: return new cEnumString(es.enumID, ReadString(value));
                case cString _: return new cString(ReadString(value));
                case cVector3 v: return new cVector3(ReadVector(value, parameterName, v.value));
                case cTransform t: return ReadTransform(value, parameterName, t);
                case cEnum e: return ReadEnum(value, parameterName, e, commands);
                case cSpline _: return ReadSpline(value, parameterName);
                case cResource _:
                    throw new McpError("'" + parameterName + "' is a resource and cannot be written as a value: use set_renderable (model and materials), set_collision, set_physics_system or set_animated_model (get_entity_resources shows them); place_model places a new model, and port_composites brings content in with its models.");
            }
            throw new McpError("'" + parameterName + "' is a " + template.dataType + ", which cannot be set from JSON.");
        }

        /// <summary>A material mapping set of the open level, by name or id ("" or "none" for no mapping), as the "mapping" parameter holds it.</summary>
        private static ParameterData ReadMapping(string text, Commands commands)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 || text.Equals("none", StringComparison.OrdinalIgnoreCase))
                return new cResource(null, ShortGuid.Invalid);
            Level open = Singleton.Editor?.CompositeBrowser?.Content?.Level;
            if (open == null || open.Commands != commands || open.MaterialMappings?.Entries == null)
                throw new McpError("Material mappings can only be set in the level that is open.");
            MaterialMappings.MaterialMapping map = open.MaterialMappings.Entries.FirstOrDefault(o => string.Equals(o.Name, text, StringComparison.OrdinalIgnoreCase));
            if (map == null && McpScript.ParseId(text) is ShortGuid id)
                map = open.MaterialMappings.Entries.FirstOrDefault(o => o.ID == id);
            if (map == null)
                throw new McpError("This level has no material mapping called '" + text + "' (list_material_mappings shows them; \"none\" clears the mapping).");
            return new cResource(null, map.ID);
        }

        private static ParameterData Guess(JToken value, string parameterName)
        {
            switch (value.Type)
            {
                case JTokenType.Boolean: return new cBool((bool)value);
                case JTokenType.Integer: return new cInteger(ReadInt(value, parameterName));
                case JTokenType.Float: return new cFloat((float)(double)value);
                case JTokenType.String: return new cString((string)value);
                case JTokenType.Array: return new cVector3(ReadVector(value, parameterName, null));
                case JTokenType.Object:
                    if (value["position"] != null || value["rotation"] != null) return ReadTransform(value, parameterName, null);
                    if (value["points"] != null) return ReadSpline(value, parameterName);
                    break;
            }
            throw new McpError("Could not tell what kind of value '" + parameterName + "' should be from " + value.ToString(Newtonsoft.Json.Formatting.None) + ".");
        }

        public static bool ReadBool(JToken value, string name)
        {
            switch (value.Type)
            {
                case JTokenType.Boolean: return (bool)value;
                case JTokenType.Integer: return (long)value != 0;
                case JTokenType.String:
                    string text = ((string)value).Trim().ToLowerInvariant();
                    if (text == "true" || text == "1" || text == "yes") return true;
                    if (text == "false" || text == "0" || text == "no") return false;
                    break;
            }
            throw new McpError("'" + name + "' takes true or false.");
        }

        public static int ReadInt(JToken value, string name)
        {
            if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
            {
                double number = (double)value;
                if (Math.Abs(number - Math.Round(number)) < 1e-9 && number >= int.MinValue && number <= int.MaxValue) return (int)Math.Round(number);
                throw new McpError("'" + name + "' takes a whole number between " + int.MinValue + " and " + int.MaxValue + ".");
            }
            if (value.Type == JTokenType.String && int.TryParse((string)value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) return parsed;
            if (value.Type == JTokenType.Boolean) return (bool)value ? 1 : 0;
            throw new McpError("'" + name + "' takes a whole number.");
        }

        public static double ReadDouble(JToken value, string name)
        {
            if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float) return (double)value;
            if (value.Type == JTokenType.String && double.TryParse((string)value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)) return parsed;
            throw new McpError("'" + name + "' takes a number.");
        }

        public static string ReadString(JToken value) => value.Type == JTokenType.String ? (string)value : value.ToString(Newtonsoft.Json.Formatting.None);

        /// <summary>[x, y, z], or {"x":, "y":, "z":}; missing parts keep <paramref name="current"/>'s.</summary>
        public static Vector3 ReadVector(JToken value, string name, Vector3? current)
        {
            Vector3 v = current ?? Vector3.Zero;
            if (value is JArray array)
            {
                if (array.Count != 3) throw new McpError("'" + name + "' takes three numbers [x, y, z].");
                return new Vector3((float)ReadDouble(array[0], name), (float)ReadDouble(array[1], name), (float)ReadDouble(array[2], name));
            }
            if (value is JObject obj)
            {
                if (obj["x"] != null) v.X = (float)ReadDouble(obj["x"], name);
                if (obj["y"] != null) v.Y = (float)ReadDouble(obj["y"], name);
                if (obj["z"] != null) v.Z = (float)ReadDouble(obj["z"], name);
                return v;
            }
            throw new McpError("'" + name + "' takes three numbers [x, y, z].");
        }

        /// <summary>{"position": [x,y,z], "rotation": [x,y,z]} (either may be left out to keep it), or [x,y,z] for just the position.</summary>
        public static cTransform ReadTransform(JToken value, string name, cTransform current)
        {
            cTransform result = new cTransform(current?.position ?? Vector3.Zero, current?.rotation ?? Vector3.Zero);
            if (value is JArray)
            {
                result.position = ReadVector(value, name, null);
                return result;
            }
            if (value is JObject obj)
            {
                if (obj["position"] != null) result.position = ReadVector(obj["position"], name + ".position", result.position);
                if (obj["rotation"] != null) result.rotation = ReadVector(obj["rotation"], name + ".rotation", result.rotation);
                if (obj["position"] == null && obj["rotation"] == null && (obj["x"] != null || obj["y"] != null || obj["z"] != null))
                    result.position = ReadVector(obj, name, result.position);
                return result;
            }
            throw new McpError("'" + name + "' takes {\"position\": [x, y, z], \"rotation\": [x, y, z]} (rotation in degrees).");
        }

        private static cEnum ReadEnum(JToken value, string name, cEnum template, Commands commands)
        {
            ShortGuid enumId = template.enumID;
            JToken entryToken = value;
            if (value is JObject obj)
            {
                if (obj["enum"] != null)
                {
                    CathodeEnumTable.EnumDescriptor named = commands.Utils.GetEnum((string)obj["enum"]);
                    if (named == null) throw new McpError("There is no enum called '" + obj["enum"] + "'.");
                    enumId = named.ID;
                }
                entryToken = obj["value"] ?? obj["index"];
                if (entryToken == null) throw new McpError("'" + name + "' takes an enum entry name, e.g. \"" + name + "\": \"ENTRY\".");
            }
            CathodeEnumTable.EnumDescriptor descriptor = commands.Utils.GetEnum(enumId);
            if (entryToken.Type == JTokenType.Integer)
            {
                int index = (int)(long)entryToken;
                if (descriptor != null && !descriptor.Entries.Any(o => o.Index == index))
                    throw new McpError("'" + index + "' is not a value of " + descriptor.Name + ". It takes: " + string.Join(", ", descriptor.Entries.Select(o => o.Name)) + ".");
                return new cEnum(enumId, index);
            }
            string text = ReadString(entryToken).Trim();
            if (descriptor == null)
                throw new McpError("'" + name + "' is an enum OpenCAGE has no names for: give its number.");
            string bare = text.Contains(".") ? text.Substring(text.LastIndexOf('.') + 1) : text;
            CathodeEnumTable.EnumDescriptor.Entry entry = descriptor.Entries.FirstOrDefault(o => string.Equals(o.Name, bare, StringComparison.OrdinalIgnoreCase));
            if (entry == null && int.TryParse(bare, out int numeric))
                entry = descriptor.Entries.FirstOrDefault(o => o.Index == numeric);
            if (entry == null)
                throw new McpError("'" + text + "' is not a value of " + descriptor.Name + ". It takes: " + string.Join(", ", descriptor.Entries.Select(o => o.Name)) + ".");
            return new cEnum(enumId, entry.Index);
        }

        private static cSpline ReadSpline(JToken value, string name)
        {
            JToken points = value is JObject obj ? obj["points"] : value;
            if (!(points is JArray array))
                throw new McpError("'" + name + "' takes {\"points\": [{\"position\": [x,y,z], \"rotation\": [x,y,z]}, ...]}.");
            return new cSpline(array.Select((o, i) => ReadTransform(o, name + "[" + i + "]", null)).ToList());
        }
    }
}
