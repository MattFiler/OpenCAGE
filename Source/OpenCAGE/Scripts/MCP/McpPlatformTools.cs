using Newtonsoft.Json.Linq;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.MCP
{
    /// <summary>Session-level tools: what the tools are doing, undo history and grouping, and the guard on undo and redo.</summary>
    internal static class McpPlatformTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "get_activity",
                Title = "Get activity",
                Description = "Answered at once, even while another call runs: which OpenCAGE is serving (process, open level, unsaved changes), the call running now (its tool, how long, its last progress message), how many wait, and recent calls with their outcome and the undo step each made. Use it when a call is slow, timed out on your side (it may still have finished: retrying the very same call returns that result instead of doing it twice), or to see what was done.",
                InputSchema = McpSchema.Object(
                    McpSchema.Limit(15, "recent calls (newest first)", 50),
                    McpSchema.Offset("recent calls"),
                    McpSchema.String("tool", "Only calls of this tool.")),
                ReadOnly = true,
                Idempotent = true,
                Concurrent = true,
                Run = Activity,
            };

            yield return new McpTool()
            {
                Name = "get_undo_history",
                Title = "Get undo history",
                Description = "The open level's undo history, next step first: each step's label, who made it ('assistant' for these tools, 'user' for the user) and when; the redo steps; the undo group open (undo_group); and the recent changes these tools made that are not undoable (ports, imports, asset and file edits; dry runs and calls that changed nothing are left out).",
                InputSchema = McpSchema.Object(McpSchema.Integer("limit", "Steps to list on each side (default 20, at most 200).")),
                ReadOnly = true,
                Idempotent = true,
                Concurrent = true,
                Run = History,
            };

            yield return new McpTool()
            {
                Name = "undo_group",
                Title = "Group undo steps",
                Description = "Make a task of several calls one undo step for the user. 'begin' before the first change (with a label), 'end' after the last: every undoable step these tools made in between becomes one step, called 'AI: <label>'. Steps the user made meanwhile are never folded in (the steps after the user's last one are). 'cancel' forgets the group, leaving the steps as they are. One group at a time; nothing is held open in the editor meanwhile.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("action", "begin, end or cancel.", required: true, options: new[] { "begin", "end", "cancel" }),
                    McpSchema.String("label", "What the task is, e.g. 'Camera fly-through of the canteen' (begin; end may rename it).")),
                Run = Group,
            };
        }

        #region Activity
        private static object Activity(McpCall call)
        {
            string only = call.Str("tool");
            if (only != null && McpTools.Find(only) == null)
                throw McpError.NotFound("tool", only, McpTools.All.Select(o => o.Name), "tools/list names them.");
            List<McpActivity.Entry> recent = McpActivity.Recent();
            JObject result = new JObject() { ["editor"] = McpSession.Describe() };

            McpActivity.Entry running = recent.FirstOrDefault(o => o.Outcome == "running" && o.Call != call && !(McpTools.Find(o.Tool)?.Concurrent ?? false));
            if (running != null)
            {
                JObject now = new JObject()
                {
                    ["tool"] = running.Tool,
                    ["arguments"] = running.Arguments,
                    ["for_s"] = Math.Round((DateTime.Now - (running.Started ?? running.Queued)).TotalSeconds, 1),
                };
                string progress = running.Call?.LastProgress;
                if (progress != null) now["progress"] = progress;
                if (running.Call != null && running.Call.Cancel.IsCancellationRequested) now["client_gave_up"] = true;
                result["running"] = now;
            }
            else
                result["running"] = null;
            result["queued"] = Math.Max(0, McpActivity.Queued);

            //Finished calls, newest first (a page of them)
            //(get_activity's own calls only when asked for)
            List<McpActivity.Entry> finished = recent.Where(o => o.Call != call && o.Outcome != "running" && o.Outcome != "queued"
                && (only == null ? o.Tool != "get_activity" : string.Equals(o.Tool, only, StringComparison.OrdinalIgnoreCase))).ToList();
            McpPaging.Page(call, finished, result, "recent", entry =>
            {
                JObject described = new JObject()
                {
                    ["tool"] = entry.Tool,
                    ["at"] = (entry.Started ?? entry.Queued).ToString("HH:mm:ss"),
                    ["s"] = entry.Finished == null ? (double?)null : Math.Round((entry.Finished.Value - (entry.Started ?? entry.Queued)).TotalSeconds, 1),
                    ["outcome"] = entry.Outcome,
                };
                if (entry.Arguments.Length != 0) described["arguments"] = entry.Arguments;
                if (entry.UndoStep != null) described["undo_step"] = entry.UndoStep;
                if (entry.ClientGaveUp && entry.Outcome != "cancelled") described["client_gave_up"] = true;
                if (entry.Error != null) described["error"] = entry.Error;
                return described;
            }, 15, 50);

            List<(string tool, DateTime at)> kept = McpActivity.UnansweredChanges();
            if (kept.Count != 0)
            {
                result["kept_for_retry"] = new JArray(kept.Select(o => new JObject() { ["tool"] = o.tool, ["finished"] = o.at.ToString("HH:mm:ss") }));
                call.Note("kept_for_retry: changes that finished after the client stopped waiting. Repeating the very same call returns its result instead of making the change again.");
            }
            return result;
        }
        #endregion

        #region Undo history and groups
        //Tools that change the editor's view, the game, other levels or files outside the level, not the level: no undo step is expected of them
        private static readonly HashSet<string> NotLevelChanges = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "undo", "redo", "undo_group", "open_composite", "select_entities", "set_viewport_view", "set_viewport_camera", "viewport_action",
            "launch_game", "close_game", "launch_options", "editor_options", "game_directories", "release_level_cache", "retry_shader_harvest",
            "load_level", "save_level", "create_backup", "delete_backups", "delete_level", "export_model", "export_textures", "export_sound", "export_animations",
            "export_collision_mesh", "export_composite_package", "export_region_collision", "runtime_utils", "preview_cage_animation",
        };

        //Families whose every change to the level is an undo step: a call of theirs that left none changed nothing
        private static readonly HashSet<string> UndoOnlyToolsets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "script", "flowgraph", "zones", "cameras", "characters", "lights",
        };

        //What a description says when a tool of such a family does write something no undo step holds
        private static readonly string[] NotUndoableWords = { "not undoable", "cannot be undone", "can't be undone", "no undo step", "straight away", "written at once" };

        /// <summary>
        /// Whether a finished call belongs in not_undoable: it succeeded in this level, is not read-only, and changed something
        /// no undo step holds - not a dry run or a call its result marks unchanged, nor a call of a family whose changes are all
        /// undo steps (no step there means nothing changed). <paramref name="partly"/>: it made a step, but one that takes back
        /// only part of it (port_composites' placement, import_model's instance: the port or import itself stays).
        /// </summary>
        private static bool LeftNoStep(McpActivity.Entry entry, DateTime opened, out bool partly)
        {
            partly = false;
            if (entry.Outcome != "ok" || entry.ReadOnly || entry.NoChange || NotLevelChanges.Contains(entry.Tool) || (entry.Finished ?? DateTime.Now) < opened)
                return false;
            McpTool tool = McpTools.Find(entry.Tool);
            bool undoOnly = tool != null && UndoOnlyToolsets.Contains(tool.Toolset ?? "");
            bool saysNot = tool != null && NotUndoableWords.Any(o => (tool.Description ?? "").IndexOf(o, StringComparison.OrdinalIgnoreCase) >= 0);
            if (entry.UndoStep != null)
            {
                partly = saysNot && !undoOnly;
                return partly;
            }
            return !(undoOnly && !saysNot);
        }

        private static object History(McpCall call)
        {
            int limit = Math.Max(1, Math.Min(200, call.Int("limit", 20)));
            JObject result = McpEditor.UI(() =>
            {
                McpEditor.RequireLevel(forEditing: false);
                UndoStack stack = UndoStack.Current;
                JObject history = new JObject()
                {
                    ["undo"] = new JArray(stack.History(false, limit).Select(Describe)),
                    ["redo"] = new JArray(stack.History(true, limit).Select(Describe)),
                    ["undo_steps"] = stack.UndoCount,
                    ["redo_steps"] = stack.RedoCount,
                };
                if (!stack.CanUndo && stack.UndoCount != 0)
                    history["note"] = "Undo is not available right now (OpenCAGE is saving or mid-edit).";
                if (_group != null)
                {
                    int since = stack.StepsSince(_group.Mark);
                    history["group"] = new JObject()
                    {
                        ["label"] = _group.Label,
                        ["begun"] = _group.Begun.ToString("HH:mm:ss"),
                        ["steps_so_far"] = since,
                    };
                }
                return history;
            });

            //What these tools changed in this level (since it was opened) that the history cannot take back
            JArray other = new JArray();
            DateTime opened = McpSession.LevelOpened;
            foreach (McpActivity.Entry entry in McpActivity.Recent())
            {
                if (!LeftNoStep(entry, opened, out bool partly)) continue;
                JObject described = new JObject() { ["tool"] = entry.Tool, ["at"] = (entry.Started ?? entry.Queued).ToString("HH:mm:ss") };
                if (entry.Arguments.Length != 0) described["arguments"] = entry.Arguments;
                if (partly)
                {
                    described["undo_step"] = entry.UndoStep;
                    described["partly"] = "that step takes back only part of the call; the rest stays";
                }
                other.Add(described);
                if (other.Count >= limit) break;
            }
            if (other.Count != 0)
            {
                result["not_undoable"] = other;
                call.Note("not_undoable: changes these tools made that no undo step takes back (their tools' descriptions say so), or only partly ('partly': a port's placement is a step, the port is not). Undo takes back the steps in 'undo' only.");
            }
            return result;
        }

        private static JObject Describe(UndoStack.HistoryEntry entry)
        {
            JObject described = new JObject() { ["label"] = entry.Label, ["by"] = IsAssistant(entry) ? "assistant" : "user" };
            if (entry.Time != null) described["at"] = entry.Time.Value.ToString("HH:mm:ss");
            return described;
        }

        /// <summary>Whether these tools made the step (stamped while a tool ran, or labelled 'AI: ...' as their steps are).</summary>
        private static bool IsAssistant(UndoStack.HistoryEntry entry) =>
            entry.Origin == McpSession.AssistantOrigin || (entry.Label ?? "").StartsWith("AI:", StringComparison.OrdinalIgnoreCase);

        /// <summary>Whether a step's label is the one 'expect' names: it, or the start of it, with or without its 'AI: '.</summary>
        private static bool IsExpected(string label, string wanted)
        {
            if (label == null)
                return false;
            string bare = label.StartsWith("AI:", StringComparison.OrdinalIgnoreCase) ? label.Substring(3).TrimStart() : label;
            return label.StartsWith(wanted, StringComparison.OrdinalIgnoreCase) || bare.StartsWith(wanted, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class OpenGroup
        {
            public object Mark;
            public string Label;
            public DateTime Begun;
            public string Level;
        }
        //UI thread only
        private static OpenGroup _group;

        private static string AiLabel(string label)
        {
            label = (label ?? "").Trim();
            if (label.Length == 0) label = "Assistant task";
            return label.StartsWith("AI:", StringComparison.OrdinalIgnoreCase) ? label : "AI: " + label;
        }

        private static object Group(McpCall call)
        {
            string action = (call.Str("action", required: true) ?? "").Trim().ToLowerInvariant();
            string label = call.Str("label");
            return McpEditor.UI(() =>
            {
                UndoStack stack = UndoStack.Current;
                switch (action)
                {
                    case "begin":
                        {
                            LevelContent content = McpEditor.RequireLevel();
                            if (_group != null)
                                call.Note("The group '" + _group.Label + "' (begun " + _group.Begun.ToString("HH:mm:ss") + ") was still open: it is replaced, and its steps stay as they are.");
                            _group = new OpenGroup() { Mark = stack.Mark(), Label = AiLabel(label), Begun = DateTime.Now, Level = content.Level.Name };
                            return new JObject() { ["begun"] = _group.Label, ["note"] = "Every undoable change these tools make from now until undo_group end becomes this one step." };
                        }
                    case "end":
                        {
                            McpEditor.RequireLevel();
                            McpEditor.RequireUndoIdle();
                            OpenGroup group = _group ?? throw new McpError(McpErrorCodes.Refused, "No undo group is open: undo_group begin starts one.");
                            _group = null;
                            string name = label == null ? group.Label : AiLabel(label);
                            int since = stack.StepsSince(group.Mark);
                            if (since < 0)
                                throw new McpError(McpErrorCodes.Conflict, "The undo history has been cleared since undo_group begin (the level was re-opened" + (group.Level != null ? " - the group was begun in " + group.Level : "") + ", or an undo failed), so there is nothing to group.");
                            if (since == 0)
                                return new JObject() { ["grouped"] = 0, ["note"] = "No undoable step was made since begin." };
                            //Only the run of the assistant's steps on top: a user's step stays theirs, and so does all below it
                            List<UndoStack.HistoryEntry> steps = stack.History(false, since);
                            int ours = 0;
                            while (ours < steps.Count && IsAssistant(steps[ours])) ours++;
                            if (ours == 0)
                                throw new McpError(McpErrorCodes.Conflict, "The latest step, '" + steps[0].Label + "', is the user's: steps cannot be grouped across it. The steps since begin stay separate.");
                            if (!stack.Collapse(ours, name))
                                throw McpError.Busy("OpenCAGE is in the middle of an edit or a save. Try undo_group end again in a moment (the group is still open).");
                            JObject result = new JObject() { ["grouped"] = ours, ["step"] = name };
                            if (ours < since)
                                result["note"] = (since - ours) + " earlier step(s) were left separate: the user's step '" + steps[ours].Label + "' came between.";
                            return result;
                        }
                    case "cancel":
                        {
                            OpenGroup group = _group;
                            _group = null;
                            return new JObject() { ["cancelled"] = group?.Label, ["note"] = group == null ? "No undo group was open." : "The steps stay as they are." };
                        }
                    default:
                        throw McpError.Invalid("'action' is begin, end or cancel.");
                }
            });
        }

        /// <summary>
        /// Gives undo and redo a guard: they refuse to take back (or redo) a step the user made unless 'force', and 'expect'
        /// names the step a caller means, so one made by someone else since is never undone instead.
        /// </summary>
        public static void Decorate(Dictionary<string, McpTool> tools)
        {
            foreach (string name in new[] { "undo", "redo" })
            {
                if (!tools.TryGetValue(name, out McpTool tool) || !(tool.InputSchema?["properties"] is JObject))
                    continue;
                bool undo = name == "undo";
                JObject properties = (JObject)tool.InputSchema["properties"];
                if (properties["force"] != null || properties["expect"] != null)
                    continue;
                properties["expect"] = new JObject()
                {
                    ["type"] = "string",
                    ["description"] = "The label (or the start of it) the next step must have, e.g. 'AI: Add 3 entities': refused if another step is next (someone else changed the level since).",
                };
                properties["force"] = new JObject()
                {
                    ["type"] = "boolean",
                    ["description"] = (undo ? "Also take back" : "Also redo") + " steps the user made (not these tools). Without it the call is refused before such a step.",
                };
                tool.Description = tool.Description.TrimEnd() + " Refuses a step the user made unless force=true; get_undo_history lists the steps and who made them.";
                tool.Run = call =>
                {
                    int steps = Math.Max(1, call.Int("steps", 1));
                    string expect = call.Str("expect");
                    bool force = call.Bool("force");
                    string wanted = expect?.Trim();
                    McpError NotExpected(string label) => new McpError(McpErrorCodes.Conflict, "The next step to " + name + " is " + (label == null ? "nothing" : "'" + label + "'") + ", not '" + wanted + "': nothing was done. " +
                        "get_undo_history lists the steps.");
                    McpEditor.UI(() =>
                    {
                        McpEditor.RequireLevel();
                        List<UndoStack.HistoryEntry> next = UndoStack.Current.History(!undo, steps);
                        if (expect != null && !IsExpected(next.Count == 0 ? null : next[0].Label ?? "", wanted))
                            throw NotExpected(next.Count == 0 ? null : next[0].Label ?? "");
                        if (!force)
                        {
                            int user = next.FindIndex(o => !IsAssistant(o));
                            if (user == 0)
                                throw new McpError(McpErrorCodes.Refused, "The next step to " + name + ", '" + next[0].Label + "', was made by the user, not by these tools: nothing was done. Ask the user, then call again with force=true to " + name + " it.");
                            if (user > 0)
                                throw new McpError(McpErrorCodes.Refused, "Only the next " + user + " of the " + next.Count + " steps were made by these tools; then comes the user's '" + next[user].Label + "'. Nothing was done: " + name + " with steps=" + user + ", or force=true to include the user's.");
                        }
                    });
                    /* ...and again as each step is taken: the steps run one UI step at a time, and the user can make one of
                       their own in between, which would then be next */
                    return McpEditorTools.Step(call, undo, index =>
                    {
                        List<UndoStack.HistoryEntry> next = UndoStack.Current.History(!undo, 1);
                        if (next.Count == 0)
                            return null;
                        if (index == 0 && expect != null && !IsExpected(next[0].Label ?? "", wanted))
                            return NotExpected(next[0].Label ?? "");
                        if (!force && !IsAssistant(next[0]))
                            return new McpError(McpErrorCodes.Refused, index == 0
                                ? "The next step to " + name + ", '" + next[0].Label + "', was made by the user, not by these tools: nothing was done. Ask the user, then call again with force=true to " + name + " it."
                                : "the next step to " + name + ", '" + next[0].Label + "', is one the user made since this call began, so it was left. Ask the user, then " + name + " again with force=true to include it.");
                        return null;
                    });
                };
            }
        }
        #endregion
    }

    /// <summary>
    /// The server's prompts: ready-made plans for common requests, offered by clients as commands (in Claude Code,
    /// /mcp__opencage__camera_flythrough and so on). They cost nothing in the tool list. The bridge serves them from
    /// the tool cache, so each is plain data: '{argument}' in the template is replaced by the argument's value.
    /// </summary>
    internal static class McpPrompts
    {
        public static JArray Definitions()
        {
            return new JArray(
                Prompt("camera_flythrough", "Camera fly-through of a room",
                    "Make a CAGEAnimation that moves a camera round a room, placed from the room as it is now in the editor, and wire it to play in game.",
                    new[] { Argument("room", "The room's name, as the user calls it (e.g. Canteen).", true), Argument("seconds", "How long one lap takes (default 20).", false, "20") },
                    "In OpenCAGE, make a CAGEAnimation that moves a camera round the room called '{room}' in the open level, one lap in {seconds} seconds, and make it play in game.\n" +
                    "1. get_editor_state.\n" +
                    "2. Find the room: find_places {name: '{room}'}. If several candidates fit about as well, ask the user which; if none, try the names it suggests.\n" +
                    "3. create_camera_animation {composite: the room's own composite (from the candidate) or 'root', region: the candidate's region, duration: {seconds}, end: 'loop' (or 'return' to hand the view back after one lap)}. It builds the camera, the animation, the start trigger and the links as one undo step, keeps the path off the room's collision as it is in the editor now, and reports path_check and will_it_play.\n" +
                    "4. If path_check shows passes_through, run animate_camera_path on that animation again with a larger inset or another height.\n" +
                    "5. preview_cage_animation {view: 'camera', times: [0, and a few more up to {seconds}]} to check the framing.\n" +
                    "6. Tell the user what was made (undo takes it all back in one step). A plain save_level makes it reach the game (camera CAGEAnimations need no build); save only if the user asks, and ask first on a campaign level."),
                Prompt("zone_level", "Zone the level",
                    "Split the open level into streaming zones, link them through their doors, and check the result.",
                    new JObject[0],
                    "Zone the open level in OpenCAGE so it streams as the player moves through it.\n" +
                    "1. get_editor_state, then analyse_zones: which zones, doors and links exist and what is missing.\n" +
                    "2. auto_zone with dry_run first; look over the proposed zones (merge, drop or name them by number), then run it.\n" +
                    "3. link_zones with dry_run, then for real; check_zones to confirm nothing is left out of a zone or unlinked.\n" +
                    "4. Tell the user what changed. Zones reach the game only at Save & Build (save_level with build=true)."),
                Prompt("port_and_place", "Port and place a composite",
                    "Bring composites in from another level, with everything they use, and place them in the open level.",
                    new[] { Argument("level", "The level to take them from, as list_levels names it.", true), Argument("composites", "The composites, by name or path.", true), Argument("where", "Where to place them (default: ask the user).", false, "where the user wants them") },
                    "Bring '{composites}' from {level} into the open level in OpenCAGE and place them: {where}.\n" +
                    "1. search_level in {level} to find the composite(s) by name; describe_level_composite shows what each holds and where it is placed there.\n" +
                    "2. port_composites from {level} with dry_run first: it brings their child composites, models, materials, textures and collision too. The port itself is not undoable.\n" +
                    "3. port_composites again for real with 'place' to put an instance in the level in the same call, as one undo step: a position where asked (metres, Y up, in the space of the composite it goes into; get_bounds or raycast find a spot), or like_source to put it where it sat in {level}. For more instances use create_entities.\n" +
                    "4. capture_viewport to show the user. Models and collision reach the game only at Save & Build (save_level build=true): save only if the user asks."));
        }

        private static JObject Prompt(string name, string title, string description, IEnumerable<JObject> arguments, string template) => new JObject()
        {
            ["name"] = name,
            ["title"] = title,
            ["description"] = description,
            ["arguments"] = new JArray(arguments),
            ["template"] = template,
        };

        private static JObject Argument(string name, string description, bool required, string fallback = null)
        {
            JObject argument = new JObject() { ["name"] = name, ["description"] = description, ["required"] = required };
            if (fallback != null) argument["default"] = fallback;
            return argument;
        }

        /// <summary>The prompts as prompts/list gives them (without their templates).</summary>
        public static JArray List(JArray definitions)
        {
            return new JArray(definitions.OfType<JObject>().Select(o => new JObject()
            {
                ["name"] = o["name"],
                ["title"] = o["title"],
                ["description"] = o["description"],
                ["arguments"] = new JArray(((JArray)o["arguments"]).OfType<JObject>().Select(a => new JObject() { ["name"] = a["name"], ["description"] = a["description"], ["required"] = a["required"] })),
            }));
        }

        /// <summary>prompts/get: the prompt with its arguments filled in, or null and why (an unknown prompt, a missing argument).</summary>
        public static JObject Get(JArray definitions, string name, JObject arguments, out string problem)
        {
            problem = null;
            JObject prompt = definitions.OfType<JObject>().FirstOrDefault(o => string.Equals((string)o["name"], name, StringComparison.OrdinalIgnoreCase));
            if (prompt == null)
            {
                problem = "Unknown prompt: " + name + ". There are: " + string.Join(", ", definitions.Select(o => (string)o["name"])) + ".";
                return null;
            }
            string text = (string)prompt["template"];
            foreach (JObject argument in ((JArray)prompt["arguments"]).OfType<JObject>())
            {
                string key = (string)argument["name"];
                JToken given = arguments?[key];
                string value = given == null || given.Type == JTokenType.Null ? null : given.Type == JTokenType.String ? (string)given : given.ToString(Newtonsoft.Json.Formatting.None);
                if (string.IsNullOrWhiteSpace(value))
                {
                    if ((bool?)argument["required"] == true)
                    {
                        problem = "The " + name + " prompt needs '" + key + "': " + (string)argument["description"];
                        return null;
                    }
                    value = (string)argument["default"] ?? "";
                }
                text = text.Replace("{" + key + "}", value.Trim());
            }
            return new JObject()
            {
                ["description"] = prompt["description"],
                ["messages"] = new JArray(new JObject() { ["role"] = "user", ["content"] = new JObject() { ["type"] = "text", ["text"] = text } }),
            };
        }
    }
}
