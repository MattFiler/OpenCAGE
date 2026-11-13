using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using Newtonsoft.Json.Linq;
using OpenCAGE.DockPanels;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE.MCP
{
    /// <summary>A flowgraph page's wire ties (STNodeEditor.WireTies.cs), changed the way the editor changes them.</summary>
    internal static class McpWireTieTools
    {
        private static readonly string[] _actions = { "list", "tie", "untie", "untie_link", "move", "straighten" };

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "edit_wire_ties",
                Title = "Edit wire ties",
                Description = "List, add or remove a flowgraph page's wire ties: points several links are tied together at, so links running the same way draw as one bundle (two ties on the same links make a conduit of parallel wires). Only how the page is drawn - the links themselves never change. tie ties the given links at x/y (default: the middle of their lines), lanes in the order of the pins they come from; untie removes a tie; untie_link takes one link out of one; move puts a tie at x/y; straighten puts a tie back on its links' straight path. One undo step each; saved with save_level.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id; 'root' for the level's root).", required: true),
                    McpSchema.String("action", "list, tie, untie, untie_link, move or straighten.", required: true, options: _actions),
                    McpSchema.String("page", "The page (name; default: the page on screen)."),
                    McpSchema.Integer("tie", "untie/untie_link/move/straighten: the tie's id (from list)."),
                    McpSchema.Array("links", "tie/untie_link: the links, one object each: from + to entities (id or name), and optionally from_pin + to_pin to pick out one link between them (without pins: every link from one to the other).", LinkSchema()),
                    McpSchema.Number("x", "tie/move: canvas position of the tie's middle."),
                    McpSchema.Number("y", "tie/move: canvas position (with x).")),
                Destructive = true,
                Run = Edit,
            };
        }

        private static JObject LinkSchema()
        {
            JObject Prop(string description) => new JObject() { ["type"] = "string", ["description"] = description };
            return new JObject()
            {
                ["type"] = "object",
                ["properties"] = new JObject()
                {
                    ["from"] = Prop("The entity the link goes out of (id or name)."),
                    ["from_pin"] = Prop("Its pin (name)."),
                    ["to"] = Prop("The entity the link goes into (id or name)."),
                    ["to_pin"] = Prop("Its pin (name)."),
                },
                ["required"] = new JArray("from", "to"),
            };
        }

        private static object Edit(McpCall call)
        {
            string action = (call.Str("action", required: true) ?? "").Trim().ToLowerInvariant();
            if (!_actions.Contains(action))
                throw new McpError("'action' is one of: " + string.Join(", ", _actions) + ".");

            return McpEditor.UI<object>(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                if (action != "list")
                    McpEditor.RequireUndoIdle();
                CompositeDisplay display = Singleton.Editor.CompositeDisplay;
                if (display == null || display.IsDisposed || !display.Populated || display.Composite != composite)
                {
                    McpScriptEdit.Show(composite, null);
                    display = Singleton.Editor.CompositeDisplay;
                    if (display == null || display.IsDisposed || !display.Populated || display.Composite != composite)
                        throw new McpError("Could not open " + composite.name + " in the editor.");
                }
                Flowgraph page = FindPage(display, composite, call.Has("page") ? call.Str("page") : null);
                STNodeEditor editor = page.Nodegraph;

                int tie = -1;
                if (action != "list" && action != "tie")
                {
                    if (!call.Has("tie"))
                        throw new McpError("'tie' (the tie's id, from action 'list') is needed for " + action + ".");
                    tie = call.Int("tie");
                    if (editor.GetWireTie(tie) == null)
                        throw new McpError("Page '" + page.FlowgraphName + "' has no wire tie " + tie + ". " + TieList(editor));
                }
                PointF? at = null;
                if (call.Has("x") || call.Has("y"))
                {
                    if (!call.Has("x") || !call.Has("y"))
                        throw new McpError("Give both x and y.");
                    at = new PointF((float)call.Num("x"), (float)call.Num("y"));
                }

                JObject result = new JObject() { ["composite"] = composite.name, ["page"] = page.FlowgraphName };
                switch (action)
                {
                    case "tie":
                        {
                            List<(STNodeOption, STNodeOption)> links = FindLinks(commands, composite, editor, call.Array("links", required: true));
                            int id = editor.TieLinks(links, at);
                            if (id < 0)
                                throw new McpError("None of those links is on page '" + page.FlowgraphName + "'.");
                            result["tied"] = id;
                            break;
                        }
                    case "untie":
                        editor.UntieWire(tie);
                        result["untied"] = tie;
                        break;
                    case "untie_link":
                        {
                            int taken = 0;
                            foreach ((STNodeOption from, STNodeOption to) in FindLinks(commands, composite, editor, call.Array("links", required: true)))
                                if (editor.GetWireTie(tie) != null && editor.UntieWireLink(tie, from, to))
                                    taken++;
                            if (taken == 0)
                                throw new McpError("None of those links is tied at tie " + tie + ".");
                            result["links_untied"] = taken;
                            break;
                        }
                    case "move":
                        if (at == null)
                            throw new McpError("'x' and 'y' are needed for move.");
                        editor.MoveWireTie(tie, at.Value);
                        break;
                    case "straighten":
                        editor.StraightenWireTie(tie);
                        break;
                }
                if (action != "list")
                {
                    editor.Invalidate();
                    display.SaveAllFlowgraphs();
                }
                result["ties"] = Describe(commands, composite, editor);
                return result;
            });
        }

        private static Flowgraph FindPage(CompositeDisplay display, Composite composite, string name)
        {
            List<Flowgraph> pages = display.Flowgraphs.Where(o => o != null && !o.IsDisposed).ToList();
            if (pages.Count == 0)
                throw new McpError(composite.name + " has no flowgraph pages open.");
            if (string.IsNullOrWhiteSpace(name))
                return pages.FirstOrDefault(o => o.Visible) ?? pages[0];
            Flowgraph page = pages.FirstOrDefault(o => o.FlowgraphName == name.Trim())
                ?? pages.FirstOrDefault(o => string.Equals(o.FlowgraphName, name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (page == null)
                throw new McpError(composite.name + " has no page called '" + name.Trim() + "'. Its pages: " + string.Join(", ", pages.Select(o => "'" + o.FlowgraphName + "'")) + ".");
            return page;
        }

        /// <summary>The page's links the specs name: by entities, and pins when given.</summary>
        private static List<(STNodeOption, STNodeOption)> FindLinks(Commands commands, Composite composite, STNodeEditor editor, JArray specs)
        {
            List<(STNodeOption, STNodeOption)> links = new List<(STNodeOption, STNodeOption)>();
            foreach (JToken spec in specs)
            {
                if (!(spec is JObject item))
                    throw new McpError("Each link is an object: { from, to, from_pin, to_pin }.");
                Entity from = McpScript.FindEntity(commands, composite, (string)item["from"]);
                Entity to = McpScript.FindEntity(commands, composite, (string)item["to"]);
                ShortGuid? fromPin = item["from_pin"] == null ? (ShortGuid?)null : McpScript.ParamId((string)item["from_pin"]);
                ShortGuid? toPin = item["to_pin"] == null ? (ShortGuid?)null : McpScript.ParamId((string)item["to_pin"]);
                int found = 0;
                foreach (STNode node in editor.Nodes.ToArray())
                {
                    if (node?.Entity != from)
                        continue;
                    foreach (STNodeOption pin in node.GetOutputOptions().Concat(node.GetTopOptions()))
                    {
                        if (pin == null || pin == STNodeOption.Empty || (fromPin != null && pin.ShortGUID != fromPin.Value))
                            continue;
                        foreach (STNodeOption other in pin.GetConnectedOption() ?? new List<STNodeOption>())
                        {
                            if (other?.Owner?.Entity != to || (toPin != null && other.ShortGUID != toPin.Value))
                                continue;
                            links.Add((pin, other));
                            found++;
                        }
                    }
                }
                if (found == 0)
                    throw new McpError("No link from " + McpScript.EntityName(commands, composite, from) + (fromPin == null ? "" : "." + item["from_pin"]) +
                        " to " + McpScript.EntityName(commands, composite, to) + (toPin == null ? "" : "." + item["to_pin"]) + " is drawn on this page.");
            }
            return links;
        }

        private static string TieList(STNodeEditor editor)
        {
            WireTieState[] ties = editor.GetWireTies();
            return ties.Length == 0 ? "It has none." : "Its ties: " + string.Join(", ", ties.Select(o => o.Id)) + ".";
        }

        private static JArray Describe(Commands commands, Composite composite, STNodeEditor editor)
        {
            string End(STNodeOption pin) => (pin.Owner?.Entity == null ? "?" : McpScript.EntityName(commands, composite, pin.Owner.Entity)) + "." + McpScript.ParamName(pin.ShortGUID);
            JArray ties = new JArray();
            foreach (WireTieState tie in editor.GetWireTies())
            {
                ties.Add(new JObject()
                {
                    ["tie"] = tie.Id,
                    ["x"] = tie.Position.X,
                    ["y"] = tie.Position.Y,
                    ["links"] = new JArray(tie.Members.Select(o => End(o.From) + " -> " + End(o.To))),
                });
            }
            return ties;
        }
    }
}
