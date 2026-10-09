using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Sound events beyond the asset tools: whether an event's bank is loaded in the open level and what loads it,
    /// the lines a dialogue event can say, placing a sound in the script, and events that are free to take new audio.
    /// The loading and usage checks are public for the other sound tools.
    /// </summary>
    internal static class McpSoundTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "list_dialogue_lines",
                Title = "List dialogue lines",
                Description = "The lines a dynamic dialogue event (a SOUND_EVENT describe_sound_event reports as DialogueEvent) can play: its arguments (which a Sound entity's argument_1..5 set), " +
                    "then each line's argument values and its audio (source ids, container path, file). export_sound auditions a line by source_id, replace_sound replaces one. " +
                    "Argument values are named where the open level's script or the game's sound data spells them out; otherwise they show as hashed ids.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("event", "The dialogue event's name.", required: true),
                    McpSchema.String("filter", "Words an argument value of the line must contain (named values only)."),
                    McpSchema.Limit(100, "lines", 2000),
                    McpSchema.Offset("lines")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListDialogueLines,
            };

            yield return new McpTool()
            {
                Name = "place_sound",
                Title = "Place sound",
                Description = "Put a sound in the script, the way retail does it, as one undo step: one_shot (a Sound started by a trigger link, or on reset), loop (a Sound that starts on reset or " +
                    "on a trigger, stopped by its Stop_ event), or room_ambience (a SoundEnvironmentMarker playing the event on entering the room, its stop on leaving, with a reverb). " +
                    "Position in world metres (at, converted into the composite through its one placement) or composite-local (position). Says whether the event's bank is loaded there and what to add if not.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("event", "The SOUND_EVENT to play (describe_sound_event shows one).", required: true),
                    McpSchema.String("composite", "The composite to put it in (a room's or the level's).", required: true),
                    McpSchema.String("mode", "one_shot (default), loop or room_ambience.", options: new[] { "one_shot", "loop", "room_ambience" }),
                    McpSchema.Vector("at", "World position [x, y, z] in metres, Y up."),
                    McpSchema.Vector("position", "Position [x, y, z] inside the composite, instead of at."),
                    McpSchema.String("near", "An entity of the composite (id or name) to put it at, instead of at or position."),
                    McpSchema.String("trigger", "An entity of the composite whose pin starts it (one_shot, loop), e.g. a TriggerBox."),
                    McpSchema.String("trigger_pin", "The trigger's pin that starts it (default on_entered for a trigger volume, else triggered)."),
                    McpSchema.String("stop_event", "loop / room_ambience: the event that stops it (default: its Stop_ event, when one exists)."),
                    McpSchema.String("reverb", "room_ambience: a SOUND_REVERB value (default: the one most retail markers use)."),
                    McpSchema.String("room_size", "room_ambience: Small_Room, Medium_Room (default) or Large_Room."),
                    McpSchema.String("name", "The new entity's name.")),
                Destructive = false,
                Run = PlaceSound,
            };

            yield return new McpTool()
            {
                Name = "find_spare_sound_events",
                Title = "Find spare sound events",
                Description = "Sound events no entity of the open level plays and whose audio no other event shares - so replace_sound on one gives your own sound an event without touching anything " +
                    "the level uses (new events can't be added). Events in permanent banks (always loaded, any level) come first; banks the game's code plays itself (foley, footsteps, weapons, UI, AI dialogue) " +
                    "are left out unless include_code_driven. Other levels' scripts aren't checked: search_level for one first.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Words the event or bank name must all contain."),
                    McpSchema.Boolean("permanent_only", "Only events in permanently loaded banks."),
                    McpSchema.Boolean("include_code_driven", "Also list events in banks the game's code plays without script (foley, footsteps, weapons, UI and AI dialogue): replacing those changes what the game does itself."),
                    McpSchema.Limit(50, "events", 1000),
                    McpSchema.Offset("events")),
                ReadOnly = true,
                Idempotent = true,
                Run = FindSpareSoundEvents,
            };
        }

        #region Helpers
        private static bool Same(string a, string b) => string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);
        private static string[] Words(string text) => (text ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        private static bool AllWords(string[] words, params string[] texts) => words.All(w => texts.Any(t => t != null && t.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0));
        #endregion

        #region Loading
        /// <summary>The banks the game keeps loaded everywhere (DATA/LIST_OF_PERMANENT_SOUND_BANKS.TXT; get_config_record permanent_soundbanks).</summary>
        public static List<string> PermanentBanks()
        {
            try
            {
                string file = Path.Combine(Singleton.PathToAI ?? "", "DATA", "LIST_OF_PERMANENT_SOUND_BANKS.TXT");
                return File.Exists(file) ? File.ReadAllLines(file).Select(o => o.Trim()).Where(o => o.Length != 0).ToList() : new List<string>();
            }
            catch { return new List<string>(); }
        }

        /// <summary>
        /// The banks the level's WORLD/SOUNDLOADZONES.DAT lists (12-byte header, then 68-byte entries with the name from byte 4).
        /// Whether the game loads a bank because it is listed there is not established. Empty, found false, when the file is missing.
        /// </summary>
        public static List<string> LoadZoneBanks(Level level, out bool found)
        {
            found = false;
            List<string> banks = new List<string>();
            try
            {
                string file = Path.Combine(Path.GetDirectoryName(level.CommandsFilepath) ?? "", "SOUNDLOADZONES.DAT");
                if (!File.Exists(file)) return banks;
                byte[] data = File.ReadAllBytes(file);
                if (data.Length < 12) return banks;
                found = true;
                int count = BitConverter.ToInt32(data, 4);
                for (int i = 0; i < count && 12 + (i + 1) * 68 <= data.Length; i++)
                {
                    int at = 12 + i * 68 + 4, end = at;
                    while (end < at + 64 && data[end] != 0) end++;
                    string name = System.Text.Encoding.ASCII.GetString(data, at, end - at);
                    if (name.Length != 0) banks.Add(name);
                }
            }
            catch { }
            return banks;
        }

        /// <summary>A SoundLoadBank entity: the bank it loads and when.</summary>
        public sealed class BankLoader
        {
            public Composite Composite;
            public FunctionEntity Entity;
            public string Bank;
            /// <summary>It loads when the level starts (trigger_via_pin is off); otherwise only when its load_bank method is triggered.</summary>
            public bool OnReset;
            public List<string> TriggeredBy = new List<string>();
        }

        /// <summary>Every SoundLoadBank in the open level's script.</summary>
        public static List<BankLoader> BankLoaders(Commands commands)
        {
            List<BankLoader> loaders = new List<BankLoader>();
            if (commands == null) return loaders;
            ShortGuid loadBank = ShortGuidUtils.Generate("load_bank");
            foreach (Composite composite in commands.Entries)
                foreach (FunctionEntity function in composite.functions)
                {
                    if (!function.function.IsFunctionType || function.function.AsFunctionType != FunctionType.SoundLoadBank) continue;
                    BankLoader loader = new BankLoader() { Composite = composite, Entity = function, Bank = McpGlobalAssetChecks.TextOf(function, "sound_bank") ?? "" };
                    loader.OnReset = !(function.GetParameter("trigger_via_pin")?.content is cBool viaPin && viaPin.value);
                    foreach (Entity source in composite.GetEntities())
                        foreach (EntityConnector link in source.childLinks)
                            if (link.linkedEntityID == function.shortGUID && link.linkedParamID == loadBank)
                                loader.TriggeredBy.Add(McpScript.EntityName(commands, composite, source) + "." + ShortGuidUtils.FindString(link.thisParamID));
                    loaders.Add(loader);
                }
            return loaders;
        }

        /// <summary>
        /// Whether one of the banks declaring an event is loaded in the open level, and by what: a permanent bank, a SoundLoadBank
        /// (when the level starts, or when triggered), or the level's SOUNDLOADZONES list; with a verdict and, when nothing loads it, what to add.
        /// </summary>
        public static JObject LoadingOf(Commands commands, Level level, List<string> declaredIn)
        {
            JObject loading = new JObject();
            List<string> permanent = PermanentBanks().Where(p => declaredIn.Any(b => Same(b, p))).ToList();
            List<BankLoader> loaders = BankLoaders(commands).Where(o => declaredIn.Any(b => Same(b, o.Bank))).ToList();
            List<string> zones = LoadZoneBanks(level, out bool zonesFound).Where(z => declaredIn.Any(b => Same(b, z))).ToList();
            loading["permanent_banks"] = new JArray(permanent);
            loading["loaded_by"] = new JArray(loaders.Take(10).Select(o =>
            {
                JObject row = new JObject() { ["composite"] = o.Composite.name, ["id"] = McpScript.Id(o.Entity.shortGUID), ["name"] = McpScript.EntityName(commands, o.Composite, o.Entity), ["bank"] = o.Bank };
                row["when"] = o.OnReset ? "level start" : "when load_bank is triggered";
                if (o.TriggeredBy.Count != 0) row["triggered_by"] = new JArray(o.TriggeredBy.Take(5));
                return row;
            }));
            if (zonesFound) loading["in_level_load_zones"] = new JArray(zones);

            string verdict;
            if (permanent.Count != 0) verdict = "loaded everywhere: " + permanent[0] + " is a permanent bank.";
            else if (loaders.Any(o => o.OnReset)) verdict = "loaded when the level starts, by a SoundLoadBank of " + loaders.First(o => o.OnReset).Bank + ".";
            else if (loaders.Count != 0) verdict = "loaded only once a SoundLoadBank's load_bank is triggered (" + loaders[0].Bank + "); before that it plays nothing.";
            else if (zones.Count != 0) verdict = "listed in the level's SOUNDLOADZONES.DAT (" + zones[0] + "); whether that alone loads it is not established - add a SoundLoadBank to be sure.";
            else if (declaredIn.Count == 0) verdict = "no bank declares it, so it never plays.";
            else verdict = "nothing in this level loads " + string.Join(" or ", declaredIn.Take(3)) + ": add a SoundLoadBank {sound_bank: '" + declaredIn[0] + "'} (it loads when the level starts unless trigger_via_pin), or pick an event in a loaded bank.";
            loading["verdict"] = verdict;
            return loading;
        }

        /// <summary>The event that stops a looping one, by retail's naming (Play_X / Stop_X), when some bank declares it; else null.</summary>
        public static string StopEventFor(WwiseSoundLibrary library, string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return null;
            List<string> candidates = new List<string>();
            if (eventName.StartsWith("Play_", StringComparison.OrdinalIgnoreCase))
            {
                string rest = eventName.Substring(5);
                candidates.Add("Stop_" + rest);
                candidates.Add("stop_" + rest);
            }
            else if (!eventName.StartsWith("Stop_", StringComparison.OrdinalIgnoreCase))
                candidates.Add("Stop_" + eventName);
            foreach (string candidate in candidates)
                if (SoundEventMetadata.BanksFor(candidate).Count != 0 || (library != null && library.HasEvent(candidate)))
                    return SoundEventMetadata.InfoFor(candidate)?.name ?? candidate;
            return null;
        }

        /// <summary>Entities of the open level whose parameters name an event (Sound.sound_event, SoundEnvironmentMarker.on_enter_event...).</summary>
        public static List<JObject> PlayedBy(Commands commands, string eventName, int limit, out int total)
        {
            total = 0;
            List<JObject> found = new List<JObject>();
            if (commands == null || string.IsNullOrEmpty(eventName)) return found;
            foreach (Composite composite in commands.Entries)
                foreach (FunctionEntity function in composite.functions)
                    foreach (Parameter parameter in function.parameters)
                    {
                        if (!(parameter.content is cString text) || !Same(text.value, eventName) || parameter.name == ShortGuids.name) continue;
                        total++;
                        if (found.Count >= limit) continue;
                        JObject row = new JObject()
                        {
                            ["composite"] = composite.name,
                            ["id"] = McpScript.Id(function.shortGUID),
                            ["name"] = McpScript.EntityName(commands, composite, function),
                            ["type"] = McpScript.TypeName(commands, composite, function),
                            ["parameter"] = ShortGuidUtils.FindString(parameter.name),
                        };
                        //What makes a retail sound loop or stop is worth copying
                        if (function.function.IsFunctionType && function.function.AsFunctionType == FunctionType.Sound)
                        {
                            string stop = McpGlobalAssetChecks.TextOf(function, "stop_event");
                            if (!string.IsNullOrEmpty(stop)) row["stop_event"] = stop;
                            if (function.GetParameter("start_on_reset")?.content is cBool onReset) row["start_on_reset"] = onReset.value;
                        }
                        found.Add(row);
                    }
            return found;
        }
        #endregion

        #region list_dialogue_lines
        private static object ListDialogueLines(McpCall call)
        {
            string eventName = call.Str("event", required: true).Trim();
            string[] words = Words(call.Str("filter"));
            //Names to read the hashed argument values back with: every string the open level's script and sound data hold
            Dictionary<uint, string> names = new Dictionary<uint, string>();
            McpEditor.UI(() =>
            {
                LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
                if (content?.Level == null || !content.IsLevelDataLoaded) return;
                void Add(string text)
                {
                    if (string.IsNullOrEmpty(text) || text.Length > 128) return;
                    uint id = Utilities.SoundHashedString(text);
                    if (!names.ContainsKey(id)) names[id] = text;
                }
                foreach (Composite composite in content.Level.Commands.Entries)
                    foreach (FunctionEntity function in composite.functions)
                        foreach (Parameter parameter in function.parameters)
                            if (parameter.content is cString text) Add(text.value);
                foreach (string name in SoundEventMetadata.AllEvents()) Add(name);
                foreach (SoundBankData.SoundBank bank in content.Level.SoundBankData?.Entries ?? new List<SoundBankData.SoundBank>()) Add(bank.Name);
            });

            WwiseSoundLibrary library = McpAnimationAssetTools.SoundLibrary(call);
            List<WwiseSoundLibrary.DialogueLine> lines = library.ResolveDialogue(eventName, out uint[] arguments, out bool parsed);
            if (lines == null)
            {
                WwiseEventResolution resolution = library.Resolve(eventName);
                throw new McpError("'" + eventName + "' is not a dialogue event (" + (resolution.Outcome == WwiseEventOutcome.NotInBanks
                    ? "no bank declares it" + McpGlobalAssetChecks.DidYouMean(SoundEventMetadata.AllEvents(), eventName, "")
                    : "it is a " + resolution.Outcome + " event: describe_sound_event shows its variations") + ").");
            }
            if (!parsed)
                throw new McpError("'" + eventName + "' is a dialogue event, but its decision tree could not be read from the banks, so its lines can't be listed.");

            string Named(uint id) => id == 0 ? "*" : names.TryGetValue(id, out string text) ? text : null;
            //A branch that plays node 0 is the tree's way of saying nothing: those are counted, not listed
            int silent = lines.Count(o => o.AudioNodeId == 0);
            List<WwiseSoundLibrary.DialogueLine> matched = lines.Where(o => o.AudioNodeId != 0 && (words.Length == 0 || AllWords(words, o.Keys.Select(Named).Where(n => n != null).ToArray()))).ToList();
            JObject result = new JObject()
            {
                ["event"] = eventName,
                ["arguments"] = new JArray(arguments.Select((o, i) => new JObject() { ["index"] = i, ["id"] = o.ToString(), ["name"] = Named(o) })),
                ["silent_branches"] = silent,
            };
            McpPaging.Page(call, matched, result, "lines", line =>
                {
                    JObject row = new JObject()
                    {
                        ["values"] = new JArray(line.Keys.Select(k => Named(k) != null ? (JToken)Named(k) : k.ToString())),
                        ["plays"] = line.AudioNodeId.ToString(),
                    };
                    if (line.Weight != 0 && line.Weight != 50) row["weight"] = line.Weight;
                    row["audio"] = new JArray(line.Variations.Take(10).Select(v =>
                    {
                        JObject audio = new JObject() { ["source_id"] = v.SourceId.ToString() };
                        if (!string.IsNullOrEmpty(v.Path)) audio["container_path"] = v.Path;
                        if (v.Media != null) audio["file"] = McpAnimationAssetTools.Relative(v.Media.File);
                        audio["streamed"] = v.IsStreamed;
                        return audio;
                    }));
                    if (line.Variations.Count == 0) row["note"] = "its audio isn't shipped";
                    return row;
                }, 100, 2000);
            if (arguments.Any(o => Named(o) == null) || lines.Any(o => o.Keys.Any(k => Named(k) == null)))
                call.Note("Values shown as numbers are hashed names this level doesn't spell out anywhere; a Sound entity's argument_N parameter takes the name, not the number. '*' means any value.");
            return result;
        }
        #endregion

        #region place_sound
        private static object PlaceSound(McpCall call)
        {
            string eventName = call.Str("event", required: true).Trim();
            string mode = (call.Str("mode") ?? "one_shot").Trim().ToLowerInvariant();
            if (mode != "one_shot" && mode != "loop" && mode != "room_ambience") throw new McpError("'mode' is one_shot, loop or room_ambience.");
            int placesGiven = (call.Has("at") ? 1 : 0) + (call.Has("position") ? 1 : 0) + (call.Has("near") ? 1 : 0);
            if (placesGiven != 1) throw new McpError("Give one of at (world position), position (inside the composite) or near (an entity to put it at).");
            if (mode == "room_ambience" && call.Has("trigger")) throw new McpError("A room ambience plays as the player enters the marker's room: it takes no trigger.");
            string roomSize = call.Str("room_size") ?? "Medium_Room";
            if (!new[] { "Small_Room", "Medium_Room", "Large_Room" }.Any(o => Same(o, roomSize))) throw new McpError("'room_size' is Small_Room, Medium_Room or Large_Room.");

            //What the event is and whether it loads, worked out before the edit so the result can say so
            WwiseSoundLibrary library = McpAnimationAssetTools.SoundLibrary(call);
            WwiseEventResolution resolution = library.Resolve(eventName);
            if (resolution.Outcome == WwiseEventOutcome.NotInBanks && SoundEventMetadata.BanksFor(eventName).Count == 0)
                throw new McpError("No bank declares a sound event '" + eventName + "'" + McpGlobalAssetChecks.DidYouMean(SoundEventMetadata.AllEvents(), eventName, " (list_enum_string_values type SOUND_EVENT lists them)."));
            string stopEvent = call.Str("stop_event")?.Trim() ?? (mode == "one_shot" ? null : StopEventFor(library, eventName));

            JObject result = null;
            McpScriptEdit.Outcome outcome = null;
            Composite composite = null;
            Vector3 local = Vector3.Zero;
            Entity trigger = null;
            string triggerPin = null;
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: true);
                Commands commands = content.Level.Commands;
                composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                if (call.Has("position"))
                    local = McpValues.ReadVector(call.Token("position"), "position", null);
                else if (call.Has("near"))
                {
                    Entity near = McpScript.FindEntity(commands, composite, call.Str("near"));
                    local = InstanceTransform.TransformOf(near)?.position ?? throw new McpError(McpScript.EntityName(commands, composite, near) + " has no position of its own to put the sound at.");
                }
                else
                {
                    //World to composite space through the composite's one placement under the root
                    Vector3 world = McpValues.ReadVector(call.Token("at"), "at", null);
                    Composite root = commands.EntryPoints?.FirstOrDefault();
                    List<McpPlacements.Placement> placements = new List<McpPlacements.Placement>();
                    int total = root == null || composite == root ? 0 : new McpPlacements(commands).PlacementsOf(root, composite, null, placements, 2, call.Cancel);
                    if (composite == root) local = world;
                    else if (total == 0) throw new McpError(composite.name + " is not placed under the level's root, so a world position can't be put in it: give position (inside the composite) instead.");
                    else if (total > 1) throw new McpError(composite.name + " is placed " + total + " times, so one world position is a different spot in each: give position (inside the composite), or put it in a composite placed once (the room's own, or the level's root).");
                    else
                    {
                        cTransform placed = placements[0].World ?? new cTransform(Vector3.Zero, Vector3.Zero);
                        Quaternion turn = InstanceTransform.ToQuaternion(placed.rotation);
                        local = Vector3.Transform(world - placed.position, Quaternion.Inverse(turn));
                    }
                }
                if (call.Has("trigger"))
                {
                    trigger = McpScript.FindEntity(commands, composite, call.Str("trigger"));
                    bool volume = trigger is FunctionEntity triggerFunction && triggerFunction.function.IsFunctionType && triggerFunction.function.AsFunctionType.ToString().Contains("Trigger") && commands.Utils.GetAllParameters(trigger, composite).Any(o => o.Item1 == ShortGuidUtils.Generate("on_entered"));
                    triggerPin = call.Str("trigger_pin") ?? (volume ? "on_entered" : "triggered");
                }
            });

            string label = "Place sound " + eventName;
            outcome = McpScriptEdit.Run(label, composite, edit =>
            {
                Commands commands = edit.Commands;
                FunctionEntity entity;
                JObject transform = new JObject() { ["position"] = new JArray(local.X, local.Y, local.Z), ["rotation"] = new JArray(0, 0, 0) };
                if (mode == "room_ambience")
                {
                    entity = edit.AddFunction(composite, FunctionType.SoundEnvironmentMarker, call.Str("name") ?? eventName);
                    edit.SetParameter(composite, entity, "on_enter_event", eventName);
                    if (stopEvent != null) edit.SetParameter(composite, entity, "on_exit_event", stopEvent);
                    edit.SetParameter(composite, entity, "room_size", new[] { "Small_Room", "Medium_Room", "Large_Room" }.First(o => Same(o, roomSize)));
                    string reverb = call.Str("reverb") ?? CommonReverb(commands);
                    if (!string.IsNullOrEmpty(reverb)) edit.SetParameter(composite, entity, "reverb_name", reverb);
                }
                else
                {
                    entity = edit.AddFunction(composite, FunctionType.Sound, call.Str("name") ?? eventName);
                    edit.SetParameter(composite, entity, "sound_event", eventName);
                    //A loop with nothing to start it starts with the level; a one-shot with nothing to start it plays once as the level starts
                    edit.SetParameter(composite, entity, "start_on_reset", trigger == null);
                    edit.SetParameter(composite, entity, "is_static_ambience", mode == "loop");
                    if (mode == "loop" && stopEvent != null) edit.SetParameter(composite, entity, "stop_event", stopEvent);
                    if (trigger != null) edit.AddLink(composite, trigger, triggerPin, entity, "start");
                }
                edit.SetParameter(composite, entity, "position", transform);
            });
            McpScriptEdit.Outcome done = outcome;

            return McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                Commands commands = content.Level.Commands;
                Entity made = done.Edit.Created.Select(o => o.Entity).FirstOrDefault();
                result = new JObject()
                {
                    ["composite"] = composite.name,
                    ["entity"] = made == null ? null : McpScript.Brief(commands, composite, made),
                    ["mode"] = mode,
                    ["event"] = eventName,
                    ["position"] = new JArray(Math.Round(local.X, 4), Math.Round(local.Y, 4), Math.Round(local.Z, 4)),
                };
                if (stopEvent != null) result["stop_event"] = stopEvent;
                if (trigger != null) result["started_by"] = McpScript.EntityName(commands, composite, trigger) + "." + triggerPin;
                if (!resolution.HasAudio) call.Note("'" + eventName + "' plays nothing itself: " + resolution.Explanation);
                if (mode == "loop" && stopEvent == null) call.Note("No Stop_ event matches '" + eventName + "', so nothing stops it but its own end; give stop_event if one exists.");
                result["loading"] = LoadingOf(commands, content.Level, SoundEventMetadata.BanksFor(eventName));
                return result;
            });
        }

        /// <summary>The reverb most of the level's SoundEnvironmentMarkers use, or null.</summary>
        private static string CommonReverb(Commands commands)
        {
            return commands.Entries.SelectMany(o => o.functions)
                .Where(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.SoundEnvironmentMarker)
                .Select(o => McpGlobalAssetChecks.TextOf(o, "reverb_name")).Where(o => !string.IsNullOrEmpty(o))
                .GroupBy(o => o, StringComparer.OrdinalIgnoreCase).OrderByDescending(o => o.Count()).Select(o => o.Key).FirstOrDefault();
        }
        #endregion

        #region find_spare_sound_events
        private static object FindSpareSoundEvents(McpCall call)
        {
            string[] words = Words(call.Str("filter"));
            bool permanentOnly = call.Bool("permanent_only");
            bool codeDriven = call.Bool("include_code_driven");
            //Banks whose events the game fires from code (no script names them, yet they play): foley, footsteps, weapons, UI, AI barks
            System.Text.RegularExpressions.Regex byCode = new System.Text.RegularExpressions.Regex("Foley|Footstep|Dialogue|Vocalisation|Projectile|Impact|Explosion|MotionTracker|Torch|Weapon|Physics|_UI|^UI|Frontend|Alien$|^Android$|^Human$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            Dictionary<string, List<string>> banksOf = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                foreach (string name in SoundEventMetadata.AllEvents()) banksOf[name] = SoundEventMetadata.BanksFor(name);
                foreach (Composite composite in content.Level.Commands.Entries)
                    foreach (FunctionEntity function in composite.functions)
                        foreach (Parameter parameter in function.parameters)
                            if (parameter.content is cString text && !string.IsNullOrEmpty(text.value)) used.Add(text.value);
            });
            List<string> permanent = PermanentBanks();

            WwiseSoundLibrary library = McpAnimationAssetTools.SoundLibrary(call);
            //How many events play each piece of audio: replacing audio another event shares changes that one too
            Dictionary<uint, int> sharing = new Dictionary<uint, int>();
            Dictionary<string, List<WwiseSoundVariation>> resolved = new Dictionary<string, List<WwiseSoundVariation>>(StringComparer.OrdinalIgnoreCase);
            using (McpEditorTools.Heartbeat(call, "Resolving every sound event"))
                foreach (string name in banksOf.Keys)
                {
                    call.ThrowIfCancelled();
                    List<WwiseSoundVariation> variations = library.Resolve(name).Variations;
                    resolved[name] = variations;
                    foreach (uint source in variations.Select(o => o.SourceId).Distinct())
                        sharing[source] = (sharing.TryGetValue(source, out int n) ? n : 0) + 1;
                }

            List<JObject> rows = new List<JObject>();
            foreach (KeyValuePair<string, List<WwiseSoundVariation>> pair in resolved)
            {
                if (pair.Value.Count == 0 || used.Contains(pair.Key)) continue;
                if (pair.Value.Any(o => sharing[o.SourceId] > 1)) continue;
                List<string> banks = banksOf[pair.Key];
                bool always = banks.Any(b => permanent.Any(p => Same(p, b)));
                if (permanentOnly && !always) continue;
                bool code = banks.Any(b => byCode.IsMatch(b)) || byCode.IsMatch(pair.Key);
                if (code && !codeDriven) continue;
                if (!AllWords(words, pair.Key, string.Join(" ", banks))) continue;
                JObject row = new JObject() { ["event"] = pair.Key, ["banks"] = new JArray(banks), ["always_loaded"] = always, ["variations"] = pair.Value.Count };
                if (code) row["game_code_may_play_it"] = true;
                rows.Add(row);
            }
            List<JObject> ordered = rows.OrderByDescending(o => (bool)o["always_loaded"]).ThenBy(o => (int)o["variations"]).ThenBy(o => (string)o["event"], StringComparer.OrdinalIgnoreCase).ToList();
            call.Note("replace_sound {event, variations:'all', wav_path} puts your audio on one; place_sound then plays it. Other levels' scripts may still use it: check with search_level first.");
            JObject result = new JObject();
            McpPaging.Page(call, ordered, result, "events", o => o, 50, 1000);
            return result;
        }
        #endregion
    }
}
