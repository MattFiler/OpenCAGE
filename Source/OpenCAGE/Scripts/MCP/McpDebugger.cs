using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using Newtonsoft.Json.Linq;
using OpenCAGE.RuntimeUtilsConnection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// runtime_utils debugger: the script debugger (LiveLinkDebugger) for assistants - breakpoints on entities of the level
    /// open here (set, remove, list, clear), where the scripts are held (state: the entity, the method and the calls that led
    /// to it, named as the level here names them), continue, step, step_frame, pause, what the game's crosshair is on (pick,
    /// selecting it here), and the live state of a composite instance's entities (states). The game must be launched with
    /// Live Link and the Debug Menu on.
    /// </summary>
    internal static class McpDebugger
    {
        public static readonly string[] Ops = { "state", "set", "remove", "list", "clear", "continue", "step", "step_frame", "pause", "pick", "states" };

        public static object Run(McpCall call)
        {
            McpLevelTools.RequireLiveLink();
            string op = (call.Str("debugger") ?? "state").Trim().ToLowerInvariant();
            switch (op)
            {
                case "state":
                    {
                        ScriptHit hit = LiveLinkDebugger.ReadState().Result;
                        if (hit == null)
                            throw new McpError(Unavailable());
                        JObject result = HitJson(hit);
                        result["breakpoints"] = McpEditor.UI(() => BreakpointsJson());
                        return result;
                    }
                case "list":
                    LiveLinkDebugger.RefreshList().Wait();
                    return new JObject() { ["breakpoints"] = McpEditor.UI(() => BreakpointsJson()) };
                case "set":
                case "remove":
                    {
                        Commands commands = null;
                        Composite composite = null;
                        Entity entity = null;
                        List<ShortGuid> path = null;
                        List<ShortGuid> methods = null;
                        McpEditor.UI(() =>
                        {
                            commands = McpEditor.RequireCommands(false);
                            composite = McpLevelTools.LiveLinkComposite(call, commands);
                            entity = McpScript.FindEntity(commands, composite, call.Str("entity", required: true));
                            if (call.Bool("every_instance"))
                                path = null;
                            else if (call.Has("instance_path"))
                                path = McpLevelTools.ReadInstancePath(call, commands, composite);
                            else if (Singleton.Editor?.CompositeDisplay?.Composite == composite)
                                path = LiveLink.InstancePath(Singleton.Editor.CompositeDisplay, commands);
                            if (call.Has("method"))
                            {
                                ShortGuid method = ShortGuidUtils.Generate(call.Str("method").Trim());
                                if (op == "set" && !LiveLinkDebugger.BreakableMethods(commands, composite, entity).Contains(method))
                                    call.Note("'" + call.Str("method") + "' is not one of this entity's method pins; the breakpoint is set on it anyway.");
                                methods = new List<ShortGuid>() { method };
                            }
                        });
                        string answer = op == "set"
                            ? McpEditor.UI(() => LiveLinkDebugger.Set(commands, composite, path, entity, methods)).Result
                            : McpEditor.UI(() => LiveLinkDebugger.Clear(composite, path, entity, methods)).Result;
                        JObject result = new JObject()
                        {
                            ["result"] = answer,
                            ["entity"] = McpEditor.UI(() => LiveLinkDebugger.EntityText(commands, composite, entity)),
                            ["composite"] = composite.name,
                            ["instances"] = path == null ? "every running instance of " + composite.name : (path.Count == 0 ? "the root" : string.Join(" > ", path.Select(o => McpScript.Id(o)))),
                            ["breakpoints"] = McpEditor.UI(() => BreakpointsJson()),
                        };
                        if (op == "set")
                            call.Note("The game stops its scripts (and itself) when one of those methods is about to be called: runtime_utils {action: 'debugger', debugger: 'state'} says where, 'continue' or 'step' go on.");
                        return result;
                    }
                case "clear":
                    return new JObject() { ["result"] = McpEditor.UI(() => LiveLinkDebugger.RemoveAll()).Result };
                case "continue":
                    return Control("debugger.continue", "");
                case "step":
                    return Control("debugger.step", "cycle");
                case "step_frame":
                    return Control("debugger.step", "frame");
                case "pause":
                    return Control("debugger.pause", "");
                case "pick":
                    {
                        Commands commands = McpEditor.UI(() => McpEditor.RequireCommands(false));
                        LiveLinkDebugger.Pick pick = McpEditor.UI(() => LiveLinkDebugger.PickFromGame(commands)).Result;
                        if (!pick.Ok)
                            throw new McpError(pick.Message);
                        bool select = call.Bool("select", true);
                        bool shown = select && McpEditor.UI(() => LiveLinkDebugger.ShowEntity(commands, pick.Path, pick.Entity.shortGUID));
                        JObject result = new JObject()
                        {
                            ["entity"] = McpEditor.UI(() => LiveLinkDebugger.EntityText(commands, pick.Composite, pick.Entity)),
                            ["id"] = McpScript.Id(pick.Entity.shortGUID),
                            ["composite"] = pick.Composite.name,
                            ["instance_path"] = new JArray(pick.Path.Select(o => McpScript.Id(o))),
                            ["description"] = pick.Description,
                            ["selected"] = shown,
                        };
                        if (pick.Position.HasValue)
                            result["position"] = new JArray(pick.Position.Value.X, pick.Position.Value.Y, pick.Position.Value.Z);
                        return result;
                    }
                case "states":
                    {
                        Commands commands = null;
                        Composite composite = null;
                        List<ShortGuid> path = null;
                        McpEditor.UI(() =>
                        {
                            commands = McpEditor.RequireCommands(false);
                            composite = McpLevelTools.LiveLinkComposite(call, commands);
                            if (call.Has("instance_path"))
                                path = McpLevelTools.ReadInstancePath(call, commands, composite);
                            else if (Singleton.Editor?.CompositeDisplay?.Composite == composite)
                                path = LiveLink.InstancePath(Singleton.Editor.CompositeDisplay, commands);
                        });
                        string place = "composite=" + composite.shortGUID.AsUInt32.ToString("X8") + " path=" + (path == null ? "*" : string.Join(",", path.Select(o => o.AsUInt32.ToString("X8"))));
                        LiveLink.Reply reply = LiveLink.DebugMenuCommand("debugger.entities", place, 5.0).Result;
                        if (!reply.Ok)
                            throw new McpError(LiveLinkDebugger.DescribeRefusal(reply));
                        Dictionary<string, string> fields = LiveLinkDebugger.Fields(reply.Message);
                        if (fields.TryGetValue("problem", out string problem))
                            throw new McpError(problem);
                        int limit = Math.Max(1, call.Int("limit", 200));
                        JArray entities = McpEditor.UI(() =>
                        {
                            JArray list = new JArray();
                            foreach (KeyValuePair<string, string> pair in fields)
                            {
                                if (list.Count >= limit)
                                    break;
                                if (pair.Key.Length != 8 || !uint.TryParse(pair.Key, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint id)
                                    || !uint.TryParse(pair.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint state))
                                    continue;
                                Entity entity = composite.GetEntityByID(new ShortGuid(id));
                                list.Add(new JObject()
                                {
                                    ["entity"] = entity != null ? LiveLinkDebugger.EntityText(commands, composite, entity) : "[" + new ShortGuid(id).ToByteString() + "] (not in this composite here)",
                                    ["state"] = Flowgraph.StateText(state),
                                    ["flags"] = "0x" + state.ToString("X8"),
                                });
                            }
                            return list;
                        });
                        return new JObject() { ["composite"] = composite.name, ["instance"] = fields.TryGetValue("instance", out string instance) ? instance : "", ["entities"] = entities };
                    }
            }
            throw new McpError("'debugger' must be one of: " + string.Join(", ", Ops) + ".");
        }

        private static string Unavailable()
        {
            return "The game's script debugger can't be used: " + (LiveLinkDebugger.Unavailable ?? "the game did not answer") + ".";
        }

        //One of the debugger's controls; a refusal from the game (or no answer) is an error
        private static JObject Control(string command, string argument)
        {
            (bool Ok, string Text) answer = LiveLinkDebugger.ControlChecked(command, argument).Result;
            if (!answer.Ok)
                throw new McpError(answer.Text);
            return new JObject() { ["result"] = answer.Text };
        }

        private static JObject HitJson(ScriptHit hit)
        {
            JObject result = new JObject()
            {
                ["held"] = hit.Held,
                ["at_breakpoint"] = hit.AtBreakpoint,
                ["hits"] = hit.Sequence,
            };
            if (hit.Sequence == 0)
                return result;
            McpEditor.UI(() =>
            {
                Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
                result["last_hit"] = new JObject()
                {
                    ["breakpoint"] = hit.Breakpoint,
                    ["where"] = LiveLinkDebugger.HitText(hit),
                    ["method"] = new ShortGuid(hit.Method).ToString(),
                    ["entity_id"] = McpScript.Id(new ShortGuid(hit.Entity)),
                    ["composite"] = commands?.GetComposite(new ShortGuid(hit.Composite))?.name ?? hit.CompositeName,
                    ["instance_path"] = new JArray(hit.Path.Select(o => McpScript.Id(new ShortGuid(o)))),
                    ["script_time"] = Math.Round(hit.Time, 3),
                    ["steps_since"] = hit.Steps,
                    ["called_because_of"] = new JArray(hit.Chain.Select(link =>
                    {
                        Composite holder = commands?.GetComposite(new ShortGuid(link.composite));
                        Entity found = holder?.GetEntityByID(new ShortGuid(link.entity));
                        return "'" + new ShortGuid(link.method).ToString() + "' on " + (found != null ? LiveLinkDebugger.EntityText(commands, holder, found) + " in " + holder.name : McpScript.Id(new ShortGuid(link.entity)));
                    })),
                };
            });
            return result;
        }

        private static JArray BreakpointsJson()
        {
            JArray list = new JArray();
            foreach (ScriptBreakpoint bp in LiveLinkDebugger.Breakpoints)
            {
                list.Add(new JObject()
                {
                    ["id"] = bp.GameId,
                    ["label"] = bp.Label,
                    ["composite_id"] = McpScript.Id(bp.Composite),
                    ["entity_id"] = McpScript.Id(bp.Entity),
                    ["instance_path"] = bp.Path == null ? (JToken)"every instance" : new JArray(bp.Path.Select(o => McpScript.Id(o))),
                    ["methods"] = new JArray(bp.Methods.Select(o => o.ToString())),
                    ["enabled"] = bp.Enabled,
                    ["stopping_the_game"] = bp.Armed,
                    ["running_instances"] = bp.Instances,
                    ["hits"] = bp.Hits,
                    ["problem"] = bp.Problem ?? "",
                });
            }
            return list;
        }
    }
}
