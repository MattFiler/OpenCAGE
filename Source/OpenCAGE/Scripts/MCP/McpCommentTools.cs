using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using Newtonsoft.Json.Linq;
using OpenCAGE.DockPanels;
using OpenCAGE.Undo;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace OpenCAGE.MCP
{
    /// <summary>A flowgraph page's comment boxes (Flowgraph.Comments.cs), changed the way the editor changes them.</summary>
    internal static class McpCommentTools
    {
        private static readonly string[] _actions = { "list", "add", "edit", "remove" };

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "edit_flowgraph_comments",
                Title = "Edit flowgraph comment boxes",
                Description = "List, add, change or remove a flowgraph page's comment boxes: titled boxes drawn behind the nodes to group and label them. " +
                    "Only how the page is presented - the script never changes (except remove with with_contents, which removes the nodes inside as Delete Node does). " +
                    "add fits the box round the nodes of the entities in 'around' (or takes x/y/width/height); Arrange keeps each box's nodes together and refits the box. " +
                    "One undo step each; saved with save_level.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id; 'root' for the level's root).", required: true),
                    McpSchema.String("action", "list, add, edit or remove.", required: true, options: _actions),
                    McpSchema.String("page", "The page (name; default: the page on screen)."),
                    McpSchema.Integer("comment", "edit/remove: the box's index (from list)."),
                    McpSchema.String("title", "add/edit: the box's title."),
                    McpSchema.String("colour", "add/edit: one of " + string.Join(", ", FlowgraphComments.Presets.Select(o => o.Name.ToLowerInvariant())) + ", or #RRGGBB."),
                    McpSchema.Strings("around", "add/edit: entities (id or name) whose nodes on the page the box is fitted round."),
                    McpSchema.Integer("x", "add/edit: the box's left edge (canvas units)."),
                    McpSchema.Integer("y", "add/edit: its top edge."),
                    McpSchema.Integer("width", "add/edit: its width."),
                    McpSchema.Integer("height", "add/edit: its height."),
                    McpSchema.Boolean("with_contents", "remove: also remove the nodes and boxes inside it.")),
                Destructive = true,
                Run = Edit,
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

                STNodeComment box = null;
                if (action == "edit" || action == "remove")
                {
                    if (!call.Has("comment"))
                        throw new McpError("'comment' (the box's index, from action 'list') is needed for " + action + ".");
                    int index = call.Int("comment");
                    if (index < 0 || index >= editor.Comments.Count)
                        throw new McpError("Page '" + page.FlowgraphName + "' has no comment box " + index + " (it has " + editor.Comments.Count + ").");
                    box = editor.Comments[index];
                }

                JObject result = new JObject() { ["composite"] = composite.name, ["page"] = page.FlowgraphName };
                switch (action)
                {
                    case "add":
                        {
                            Rectangle bounds = BoundsFrom(call, commands, composite, editor, null)
                                ?? throw new McpError("Give 'around' (entities to fit the box round) or x, y, width and height.");
                            using (UndoStack.Current.BeginGroup("AI: Add comment '" + (call.Str("title") ?? "Comment") + "' to " + page.FlowgraphName))
                                box = page.AddCommentRecorded(bounds, call.Str("title") ?? "Comment", call.Has("colour") ? ColourFrom(call.Str("colour")) : Color.Empty);
                            result["added"] = editor.Comments.ToList().IndexOf(box);
                            break;
                        }
                    case "edit":
                        using (UndoStack.Current.BeginGroup("AI: Edit comment '" + box.Title + "' on " + page.FlowgraphName))
                        {
                            if (call.Has("title"))
                                page.RenameComment(box, call.Str("title"));
                            if (call.Has("colour"))
                                page.SetCommentColour(box, ColourFrom(call.Str("colour")));
                            Rectangle? bounds = BoundsFrom(call, commands, composite, editor, box.Bounds);
                            if (bounds != null)
                                page.SetCommentBounds(box, bounds.Value);
                        }
                        result["edited"] = editor.Comments.ToList().IndexOf(box);
                        break;
                    case "remove":
                        int nodes = call.Bool("with_contents") ? editor.GetNodesInComment(box).Length : 0;
                        using (UndoStack.Current.BeginGroup("AI: Remove comment '" + box.Title + "' from " + page.FlowgraphName))
                            page.DeleteComment(box, call.Bool("with_contents"), ask: false);
                        result["removed"] = box.Title;
                        if (nodes != 0)
                            result["nodes_removed"] = nodes;
                        break;
                }
                if (action != "list")
                {
                    editor.Invalidate();
                    display.SaveAllFlowgraphs();
                }
                result["comments"] = Describe(commands, composite, editor);
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

        //The box's bounds from 'around' (fitted round those entities' nodes, as Arrange fits a box) or from x/y/width/height
        //over the current ones; null when the call gives neither
        private static Rectangle? BoundsFrom(McpCall call, Commands commands, Composite composite, STNodeEditor editor, Rectangle? current)
        {
            JArray around = call.Array("around");
            if (around != null && around.Count != 0)
            {
                HashSet<Entity> entities = new HashSet<Entity>(around.Select(o => McpScript.FindEntity(commands, composite, (string)o)));
                List<STNode> nodes = editor.Nodes.ToArray().Where(o => o?.Entity != null && entities.Contains(o.Entity)).ToList();
                if (nodes.Count == 0)
                    throw new McpError("None of those entities has a node on this page.");
                int pad = CATHODE.Scripting.Refactor.PageArranger.GroupPadding;
                return Rectangle.FromLTRB(nodes.Min(o => o.Left) - pad, nodes.Min(o => o.Top) - pad - STNodeEditor.CommentTitleHeight,
                    nodes.Max(o => o.Left + o.Width) + pad, nodes.Max(o => o.Top + o.Height) + pad);
            }
            if (!call.Has("x") && !call.Has("y") && !call.Has("width") && !call.Has("height"))
                return null;
            if (current == null && !(call.Has("x") && call.Has("y") && call.Has("width") && call.Has("height")))
                throw new McpError("A new box needs all of x, y, width and height (or 'around').");
            Rectangle from = current ?? Rectangle.Empty;
            return new Rectangle(call.Has("x") ? call.Int("x") : from.X, call.Has("y") ? call.Int("y") : from.Y,
                Math.Max(STNodeEditor.CommentMinWidth, call.Has("width") ? call.Int("width") : from.Width),
                Math.Max(STNodeEditor.CommentMinHeight, call.Has("height") ? call.Int("height") : from.Height));
        }

        private static Color ColourFrom(string text)
        {
            string name = (text ?? "").Trim();
            foreach ((string preset, Color colour) in FlowgraphComments.Presets)
                if (string.Equals(preset, name, StringComparison.OrdinalIgnoreCase))
                    return colour;
            if (name.StartsWith("#") && name.Length == 7 && int.TryParse(name.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            throw new McpError("'colour' is one of " + string.Join(", ", FlowgraphComments.Presets.Select(o => o.Name.ToLowerInvariant())) + ", or #RRGGBB.");
        }

        private static JArray Describe(Commands commands, Composite composite, STNodeEditor editor)
        {
            JArray boxes = new JArray();
            for (int i = 0; i < editor.Comments.Count; i++)
            {
                STNodeComment box = editor.Comments[i];
                boxes.Add(new JObject()
                {
                    ["comment"] = i,
                    ["title"] = box.Title,
                    ["x"] = box.Bounds.X,
                    ["y"] = box.Bounds.Y,
                    ["width"] = box.Bounds.Width,
                    ["height"] = box.Bounds.Height,
                    ["colour"] = box.Colour.IsEmpty ? "default" : (FlowgraphComments.PresetName(box.Colour)?.ToLowerInvariant() ?? "#" + (box.Colour.ToArgb() & 0xFFFFFF).ToString("X6")),
                    ["nodes"] = new JArray(editor.GetNodesInComment(box).Where(o => o.Entity != null).Select(o => McpScript.EntityName(commands, composite, o.Entity)).Distinct()),
                });
            }
            return boxes;
        }
    }
}
