using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.Scripting.Refactor;
using CathodeLib;
using CathodeLib.ObjectExtensions;
using Newtonsoft.Json.Linq;
using OpenCAGE.DockPanels;
using OpenCAGE.Undo;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// A composite's script pages and the nodes on them, changed the way the flowgraph editor changes them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every edit works on the composite on screen (it is opened first), with its live pages compiled into
    /// the page table beforehand - the pages are the truth about their nodes while they are open. Where the
    /// editor has an undo record for the change it is used as the editor uses it (a page added, renamed or
    /// deleted; nodes added, moved or removed; a node's pins changed), so the tool's change is one undo step
    /// that behaves exactly like the user's. Changes with no editor equivalent (reordering pages, merging one
    /// into another, redrawing connections elsewhere) replace the composite's pages whole, as one step.
    /// </para>
    /// <para>
    /// A node's id is its place on its page, as the editor numbers nodes when it saves a page: get_flowgraph
    /// reports the same numbers. A composite shown as a plain list of links has no pages to edit.
    /// </para>
    /// </remarks>
    internal static class McpPageTools
    {
        private static readonly string[] _pageActions = { "create", "rename", "delete", "duplicate", "reorder", "show", "arrange" };
        private static readonly string[] _pageArgs = { "page", "name", "merge_into", "drop_links", "order" };
        private static readonly Dictionary<string, string[]> _pageArgsFor = new Dictionary<string, string[]>()
        {
            ["create"] = new[] { "name" },
            ["rename"] = new[] { "page", "name" },
            ["delete"] = new[] { "page", "merge_into", "drop_links" },
            ["duplicate"] = new[] { "page", "name" },
            ["reorder"] = new[] { "order" },
            ["show"] = new[] { "page" },
            ["arrange"] = new[] { "page" },
        };

        private static readonly string[] _nodeActions = { "add", "move", "remove", "pins" };
        private static readonly Dictionary<string, string[]> _nodeKeysFor = new Dictionary<string, string[]>()
        {
            ["add"] = new[] { "entity", "x", "y", "pins" },
            ["move"] = new[] { "entity", "node", "x", "y", "dx", "dy" },
            ["remove"] = new[] { "entity", "node" },
            ["pins"] = new[] { "entity", "node", "pins", "add_pins", "remove_pins" },
        };

        private const int MaxNodesPerCall = 500;

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "edit_flowgraph_pages",
                Title = "Edit flowgraph pages",
                Description = "Create, rename, delete, duplicate, reorder, show or arrange a composite's flowgraph pages, as the page tabs' menu does. delete refuses a page that draws links unless merge_into (its nodes and connections move to another page) or drop_links (those links leave the script) is given. duplicate copies nodes, not connections. arrange lays out a page's nodes so its links read left to right with few crossings (data nodes above or below what they feed), giving an entity another node where a data link would otherwise run across the page - the links themselves are untouched; use it after adding nodes, or on a tangled page. One undo step each (show is not an edit); saved with save_level.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id; 'root' for the level's root).", required: true),
                    McpSchema.String("action", "create, rename, delete, duplicate, reorder, show (bring a page to the front; new links are drawn on it), or arrange (lay out the page's nodes).", required: true, options: _pageActions),
                    McpSchema.String("page", "rename/delete/duplicate/show: the page (its name). arrange: the page to lay out (default: every page)."),
                    McpSchema.String("name", "create/rename/duplicate: the new name (duplicate defaults to '<page> (copy)')."),
                    McpSchema.String("merge_into", "delete: move the page's nodes and connections onto this page, below what it holds."),
                    McpSchema.Boolean("drop_links", "delete: also take the links only this page draws out of the script."),
                    McpSchema.Strings("order", "reorder: page names in the new tab order; pages left out follow in their current order.")),
                Destructive = true,
                Run = EditPages,
            };

            yield return new McpTool()
            {
                Name = "edit_flowgraph_nodes",
                Title = "Edit flowgraph nodes",
                Description = "Add nodes for existing entities to a flowgraph page, move nodes, take nodes off a page (the entity stays), or set the pins a node shows. Name nodes by entity, or by page + node id (get_flowgraph) when an entity has several. remove needs links 'drop' or 'redraw' when the nodes carry connections. One undo step; saved with save_level.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id; 'root' for the level's root).", required: true),
                    McpSchema.String("action", "add, move, remove, or pins (change the pins nodes show).", required: true, options: _nodeActions),
                    McpSchema.String("page", "The page (name). add: where the nodes go (default: the page on screen). Others: where to look (default: every page)."),
                    McpSchema.Array("nodes", "The nodes: one object each.", NodeSpecSchema(), required: true),
                    McpSchema.String("links", "remove: what happens to connections the nodes carry: 'drop' (the links leave the script) or 'redraw' (drawn again at other nodes).", options: new[] { "drop", "redraw" })),
                Destructive = true,
                Run = EditNodes,
            };

            yield return new McpTool()
            {
                Name = "capture_flowgraph",
                Title = "Picture a flowgraph page",
                Description = "A PNG of a flowgraph page as the editor draws it: every node and connection on the page, or the area around one entity's nodes. Opens the composite and brings the page to the front; changes nothing.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id; 'root' for the level's root).", required: true),
                    McpSchema.String("page", "The page (default: the one on screen)."),
                    McpSchema.String("entity", "Picture only the area around this entity's nodes on the page (id or name)."),
                    McpSchema.Integer("max_width", "Scale the picture down to at most this many pixels wide (default 1600, up to 4096).")),
                ReadOnly = true,
                Idempotent = true,
                Run = Capture,
            };
        }

        private static JObject NodeSpecSchema()
        {
            JObject Prop(string type, string description) => new JObject() { ["type"] = type, ["description"] = description };
            return new JObject()
            {
                ["type"] = "object",
                ["properties"] = new JObject()
                {
                    ["entity"] = Prop("string", "The entity (id or name)."),
                    ["node"] = Prop("integer", "move/remove/pins: the node's id on 'page' (get_flowgraph), when its entity has several nodes."),
                    ["x"] = Prop("number", "add/move: canvas position of the node's top-left corner (add: default beside the page's nodes)."),
                    ["y"] = Prop("number", "add/move: canvas position (with x)."),
                    ["dx"] = Prop("number", "move: shift right by this much (instead of x/y)."),
                    ["dy"] = Prop("number", "move: shift down by this much (instead of x/y)."),
                    ["pins"] = new JObject() { ["description"] = "add/pins: 'all', 'linked' (only connected pins), or a list of pin names. Connected pins always stay. add defaults to the editor's option." },
                    ["add_pins"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" }, ["description"] = "pins: pin names to show as well." },
                    ["remove_pins"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string" }, ["description"] = "pins: pin names to stop showing (not connected ones)." },
                },
                ["additionalProperties"] = false,
            };
        }

        #region Opening a composite's pages
        /// <summary>A composite on screen with its live pages compiled into the page table.</summary>
        private sealed class Session
        {
            public Commands Commands;
            public Composite Composite;
            public CompositeDisplay Display;

            public List<FlowgraphMeta> Layouts => FlowgraphLayoutManager.GetLayouts(Composite);

            public Flowgraph Live(string page)
            {
                Flowgraph flowgraph = Display.FindFlowgraph(page);
                if (flowgraph == null)
                    throw new McpError("The page '" + page + "' of " + Composite.name + " is not open in the editor.");
                return flowgraph;
            }

            /// <summary>The open pages, in tab order.</summary>
            public List<Flowgraph> LivePages() => Layouts.Select(o => Display.FindFlowgraph(o.Name)).Where(o => o != null).ToList();
        }

        /// <summary>Bring the composite on screen, compile its pages, and refuse one that has no pages to edit. UI thread.</summary>
        private static Session Open(Commands commands, Composite composite, bool edit)
        {
            if (edit)
                McpEditor.RequireUndoIdle();
            CompositeDisplay display = Singleton.Editor.CompositeDisplay;
            bool shown = display != null && !display.IsDisposed && display.Populated && display.Composite == composite;
            if (!shown)
            {
                McpScriptEdit.Show(composite, null);
                display = Singleton.Editor.CompositeDisplay;
                if (display == null || display.IsDisposed || !display.Populated || display.Composite != composite)
                    throw new McpError("Could not open " + composite.name + " in the editor.");
            }
            //The live pages hold the truth about their nodes: into the page table (and the links) first
            display.SaveAllFlowgraphs();
            if (!FlowgraphLayoutManager.IsCompatible(composite))
                throw new McpError(composite.name + " is shown as a plain list of links, not as flowgraph pages: " +
                    (FlowgraphLayoutManager.HasLayout(composite) ? "its pages do not draw exactly its links" : "it has no pages") +
                    ", so there are no pages to edit. layout_flowgraph gives it pages that draw every link (mode 'complete' keeps what its pages already draw); then they can be edited here.");
            return new Session() { Commands = commands, Composite = composite, Display = display };
        }

        /// <summary>A page by its name (exactly, else ignoring case when that picks out one).</summary>
        internal static FlowgraphMeta FindPage(Composite composite, IList<FlowgraphMeta> pages, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new McpError("Say which page of " + composite.name + (pages.Count == 0 ? "." : ": " + PageList(pages) + "."));
            FlowgraphMeta exact = pages.FirstOrDefault(o => o.Name == name) ?? pages.FirstOrDefault(o => o.Name == name.Trim());
            if (exact != null)
                return exact;
            List<FlowgraphMeta> loose = pages.Where(o => string.Equals(o.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (loose.Count == 1)
                return loose[0];
            throw new McpError(composite.name + " has no page called '" + name.Trim() + "'. " + (pages.Count == 0 ? "It has no pages." : "Its pages: " + PageList(pages) + "."));
        }

        private static string PageList(IEnumerable<FlowgraphMeta> pages) => string.Join(", ", pages.Select(o => "'" + o.Name + "'"));

        /// <summary>The page on screen, else the one last shown, else the first.</summary>
        private static FlowgraphMeta DefaultPage(Session session)
        {
            List<FlowgraphMeta> pages = session.Layouts;
            if (pages.Count == 0)
                throw new McpError(session.Composite.name + " has no flowgraph pages. Make one with edit_flowgraph_pages (action 'create').");
            string visible = session.Display.Flowgraphs.FirstOrDefault(o => o != null && !o.IsDisposed && o.Visible)?.FlowgraphName;
            string remembered = FlowgraphLayoutManager.GetSelectedPage(session.Composite);
            return pages.FirstOrDefault(o => o.Name == visible) ?? pages.FirstOrDefault(o => o.Name == remembered) ?? pages[0];
        }
        #endregion

        #region Reading pages (shared with get_flowgraph)
        /// <summary>The nodes of a page the editor draws (those whose entity the composite has), in order: a node's id is its place here.</summary>
        internal static List<FlowgraphMeta.NodeMeta> DrawnNodes(Composite composite, FlowgraphMeta page) => page.Nodes.Where(o => composite.GetEntityByID(o.EntityGUID) != null).ToList();

        /// <summary>Where each node a connection names sits in <see cref="DrawnNodes"/>.</summary>
        internal static Dictionary<(int, ShortGuid), int> NodeIds(List<FlowgraphMeta.NodeMeta> drawn)
        {
            Dictionary<(int, ShortGuid), int> ids = new Dictionary<(int, ShortGuid), int>();
            for (int i = 0; i < drawn.Count; i++)
                if (!ids.ContainsKey((drawn[i].NodeID, drawn[i].EntityGUID)))
                    ids.Add((drawn[i].NodeID, drawn[i].EntityGUID), i);
            return ids;
        }

        /// <summary>Every pin the flowgraph editor can show on an entity's node, where it shows it.</summary>
        internal sealed class PinCatalogue
        {
            private readonly Commands _commands;
            private readonly Composite _composite;
            private readonly Dictionary<Entity, List<NodeUtils.PinPositionInfo>> _pins = new Dictionary<Entity, List<NodeUtils.PinPositionInfo>>();

            public PinCatalogue(Commands commands, Composite composite)
            {
                _commands = commands;
                _composite = composite;
            }

            public List<NodeUtils.PinPositionInfo> All(Entity entity)
            {
                if (_pins.TryGetValue(entity, out List<NodeUtils.PinPositionInfo> known))
                    return known;
                List<NodeUtils.PinPositionInfo> pins;
                try { pins = new STNode() { Entity = entity }.GetAllPinPositions(_composite, _commands); }
                catch { pins = new List<NodeUtils.PinPositionInfo>(); }
                _pins[entity] = pins;
                return pins;
            }

            public NodeUtils.PinPositionInfo Find(Entity entity, ShortGuid pin) => All(entity).FirstOrDefault(o => o.ParameterGUID == pin);

            public PinLocation? SideOf(Entity entity, ShortGuid pin) => Find(entity, pin)?.Location;

            public string Names(Entity entity)
            {
                List<string> names = All(entity).Select(o => McpScript.ParamName(o.ParameterGUID)).ToList();
                return names.Count == 0 ? "none" : string.Join(", ", names.Take(60)) + (names.Count > 60 ? ", ..." : "");
            }
        }

        /// <summary>
        /// The pins a saved node shows, as the editor draws it: the variable's own pin, the pins its connections
        /// use (sides from its schema, else right going out and left coming in), and the unlinked pins it keeps.
        /// </summary>
        internal static List<(ShortGuid pin, PinLocation side)> ShownPins(Entity entity, FlowgraphMeta.NodeMeta node, IEnumerable<ShortGuid> connectedOut, IEnumerable<ShortGuid> connectedIn, PinCatalogue catalogue)
        {
            List<(ShortGuid, PinLocation)> pins = new List<(ShortGuid, PinLocation)>();
            HashSet<ShortGuid> seen = new HashSet<ShortGuid>();
            void Add(ShortGuid pin, PinLocation side)
            {
                if (seen.Add(pin)) pins.Add((pin, side));
            }
            if (entity is VariableEntity)
                foreach (NodeUtils.PinPositionInfo info in catalogue.All(entity))
                    Add(info.ParameterGUID, info.Location);
            foreach (ShortGuid pin in connectedOut)
                Add(pin, catalogue.SideOf(entity, pin) ?? PinLocation.Right);
            foreach (ShortGuid pin in connectedIn)
                Add(pin, catalogue.SideOf(entity, pin) ?? PinLocation.Left);
            foreach (FlowgraphMeta.NodeMeta.UnlinkedPinMeta pin in node.UnlinkedPins)
            {
                if (EntityParameterVisibility.IsHiddenFromEditor(entity, pin.ParameterGUID)) continue;
                Add(pin.ParameterGUID, (PinLocation)pin.PinLocation);
            }
            return pins;
        }

        internal static string Side(PinLocation side) => side.ToString().ToLowerInvariant();

        /// <summary>"Owner.pin -> Target.pin".</summary>
        internal static string LinkText(Commands commands, Composite composite, ShortGuid owner, ShortGuid pin, ShortGuid target, ShortGuid targetPin)
        {
            Entity from = composite.GetEntityByID(owner);
            Entity to = composite.GetEntityByID(target);
            return (from == null ? McpScript.Id(owner) : McpScript.EntityName(commands, composite, from)) + "." + McpScript.ParamName(pin) + " -> " +
                (to == null ? McpScript.Id(target) : McpScript.EntityName(commands, composite, to)) + "." + McpScript.ParamName(targetPin);
        }

        /// <summary>The links a saved page draws, as text.</summary>
        private static List<string> DrawnLinks(Session session, FlowgraphMeta page)
        {
            List<FlowgraphMeta.NodeMeta> drawn = DrawnNodes(session.Composite, page);
            Dictionary<(int, ShortGuid), int> ids = NodeIds(drawn);
            List<string> links = new List<string>();
            foreach (FlowgraphMeta.NodeMeta node in drawn)
                foreach (FlowgraphMeta.NodeMeta.ConnectionMeta connection in node.ConnectionsOut)
                    if (ids.ContainsKey((connection.ConnectedNodeID, connection.ConnectedEntityGUID)))
                        links.Add(LinkText(session.Commands, session.Composite, node.EntityGUID, connection.ParameterGUID, connection.ConnectedEntityGUID, connection.ConnectedParameterGUID));
            return links;
        }

        internal static JArray Capped(IEnumerable<string> items, int cap)
        {
            List<string> all = items.ToList();
            JArray array = new JArray(all.Take(cap));
            if (all.Count > cap)
                array.Add("... and " + (all.Count - cap) + " more");
            return array;
        }

        /// <summary>Entities of the composite with no node on any of its pages.</summary>
        internal static List<Entity> Unplaced(Composite composite, IEnumerable<FlowgraphMeta> pages)
        {
            HashSet<ShortGuid> placed = new HashSet<ShortGuid>(pages.SelectMany(o => o.Nodes).Select(o => o.EntityGUID));
            return composite.GetEntities().Where(o => !placed.Contains(o.shortGUID)).ToList();
        }
        #endregion

        #region Pages
        private static object EditPages(McpCall call)
        {
            string action = Choice(call, "action", _pageActions);
            foreach (string arg in _pageArgs)
                if (call.Has(arg) && !_pageArgsFor[action].Contains(arg))
                    throw new McpError("'" + arg + "' does not go with action '" + action + "' (it takes: " + string.Join(", ", _pageArgsFor[action]) + ").");

            return McpEditor.UI<object>(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                Session session = Open(commands, composite, edit: action != "show");
                switch (action)
                {
                    case "create": return CreatePage(call, session);
                    case "rename": return RenamePage(call, session);
                    case "delete": return DeletePage(call, session);
                    case "duplicate": return DuplicatePage(call, session);
                    case "reorder": return ReorderPages(call, session);
                    case "arrange": return ArrangePages(call, session);
                    default: return ShowPage(call, session);
                }
            });
        }

        /// <summary>The Arrange menu item, for one page or each in turn: nodes laid out as the editor draws them, positions only.</summary>
        private static object ArrangePages(McpCall call, Session session)
        {
            List<FlowgraphMeta> pages = call.Has("page") ? new List<FlowgraphMeta>() { FindPage(session.Composite, session.Layouts, call.Str("page")) } : session.Layouts.ToList();
            JArray arranged = new JArray();
            string label = "AI: Arrange " + (pages.Count == 1 ? "page " + pages[0].Name : UndoLabels.Count(pages.Count, "page", "pages") + " of " + McpScript.CompositeLeaf(session.Composite));
            using (UndoStack.Current.BeginGroup(label))
            {
                foreach (FlowgraphMeta page in pages)
                {
                    Flowgraph live = session.Live(page.Name);
                    int before = live.Nodegraph.Nodes.Count;
                    int changed = live.ArrangeAll("AI: Arrange page " + page.Name);
                    int after = live.Nodegraph.Nodes.Count;
                    JObject entry = new JObject() { ["page"] = page.Name, ["nodes"] = after, ["changed"] = changed != 0 };
                    if (after != before)
                        entry["nodes_added"] = after - before;
                    arranged.Add(entry);
                }
            }
            //Into the page table, so get_flowgraph reads the new positions
            session.Display.SaveAllFlowgraphs();
            JObject result = PageSummary(session);
            result["arranged"] = arranged;
            return result;
        }

        private static JObject PageSummary(Session session)
        {
            return new JObject()
            {
                ["composite"] = session.Composite.name,
                ["pages"] = new JArray(session.Layouts.Select(o => o.Name)),
            };
        }

        /// <summary>A name for a new or renamed page: not blank, and not another page's (in any case).</summary>
        private static string NewPageName(Session session, string name, FlowgraphMeta renaming)
        {
            string trimmed = (name ?? "").Trim();
            if (trimmed.Length == 0)
                throw new McpError("A page needs a name.");
            if (trimmed.Length > 128)
                throw new McpError("A page name can be at most 128 characters.");
            FlowgraphMeta taken = session.Layouts.FirstOrDefault(o => o != renaming && string.Equals(o.Name, trimmed, StringComparison.OrdinalIgnoreCase));
            if (taken != null)
                throw new McpError(session.Composite.name + " already has a page called '" + taken.Name + "'. Pick another name.");
            return trimmed;
        }

        private static string UniquePageName(Session session, string stem)
        {
            HashSet<string> taken = new HashSet<string>(session.Layouts.Select(o => o.Name), StringComparer.OrdinalIgnoreCase);
            if (!taken.Contains(stem)) return stem;
            for (int i = 2; ; i++)
                if (!taken.Contains(stem + " " + i)) return stem + " " + i;
        }

        private static object CreatePage(McpCall call, Session session)
        {
            string name = NewPageName(session, call.Str("name", required: true), null);
            //What the Create Flowgraph button does: an empty page, opened
            FlowgraphMeta meta = new FlowgraphMeta() { CompositeGUID = session.Composite.shortGUID, Name = name, CanvasScale = 1f };
            UndoStack.Current.Apply(new PagePresenceEdit(session.Composite, meta, true, "AI: Add page " + name));
            session.Display.FindFlowgraph(name)?.Show();
            JObject result = PageSummary(session);
            result["created"] = name;
            return result;
        }

        private static object RenamePage(McpCall call, Session session)
        {
            FlowgraphMeta page = FindPage(session.Composite, session.Layouts, call.Str("page", required: true));
            string name = NewPageName(session, call.Str("name", required: true), page);
            JObject result;
            if (name == page.Name)
            {
                result = PageSummary(session);
                result["unchanged"] = "The page is already called '" + name + "'.";
                return result;
            }
            string old = page.Name;
            UndoStack.Current.Apply(new PageRenameEdit(session.Composite, old, name));
            if (FlowgraphLayoutManager.GetSelectedPage(session.Composite) == old)
                FlowgraphLayoutManager.SetSelectedPage(session.Composite, name);
            result = PageSummary(session);
            result["renamed"] = old;
            result["to"] = name;
            return result;
        }

        private static object DeletePage(McpCall call, Session session)
        {
            FlowgraphMeta page = FindPage(session.Composite, session.Layouts, call.Str("page", required: true));
            string into = call.Str("merge_into");
            bool drop = call.Bool("drop_links");
            if (into != null && drop)
                throw new McpError("Give merge_into (keep the page's links on another page) or drop_links (remove them), not both.");
            if (into != null)
                return MergePage(session, page, FindPage(session.Composite, session.Layouts, into));

            List<string> links = DrawnLinks(session, page);
            if (links.Count != 0 && !drop)
                throw new McpError("Page '" + page.Name + "' draws " + links.Count + " link(s), which would leave the script with it:\n- " + string.Join("\n- ", Capped(links, 30).Select(o => (string)o)) +
                    "\nPass merge_into (a page to move its nodes and connections to) to keep them, or drop_links: true to remove them. Nothing was changed.");

            HashSet<ShortGuid> onPage = new HashSet<ShortGuid>(DrawnNodes(session.Composite, page).Select(o => o.EntityGUID));
            string name = page.Name;
            //The page's Delete: the page goes, and the links it drew go when the pages are next compiled - which is now
            UndoStack.Current.Apply(new PagePresenceEdit(session.Composite, page, false, "AI: Delete page " + name));
            if (links.Count != 0)
                session.Display.SaveAllFlowgraphs();

            JObject result = PageSummary(session);
            result["deleted"] = name;
            if (links.Count != 0)
                result["dropped_links"] = Capped(links, 50);
            List<Entity> orphans = Unplaced(session.Composite, session.Layouts).Where(o => onPage.Contains(o.shortGUID)).ToList();
            if (orphans.Count != 0)
                result["entities_without_nodes"] = Capped(orphans.Select(o => McpScript.EntityName(session.Commands, session.Composite, o) + " (" + McpScript.Id(o.shortGUID) + ")"), 50);
            if (session.Layouts.Count == 0)
                call.Note(session.Composite.name + " has no pages now: create one (edit_flowgraph_pages action 'create') to draw on.");
            return result;
        }

        /// <summary>A page's nodes and connections moved onto another page (below what it holds), and the page gone: one step.</summary>
        private static object MergePage(Session session, FlowgraphMeta page, FlowgraphMeta target)
        {
            if (target == page)
                throw new McpError("A page cannot be merged into itself.");
            List<FlowgraphMeta> pages = session.Layouts.Select(o => o.Copy()).ToList();
            FlowgraphLayoutManager.TrimToComposite(pages, session.Composite, out int _, out int _);
            FlowgraphMeta from = pages.First(o => o.Name == page.Name);
            FlowgraphMeta into = pages.First(o => o.Name == target.Name);

            int dx = 0, dy = 0;
            if (from.Nodes.Count != 0 && into.Nodes.Count != 0)
            {
                dx = into.Nodes.Min(o => o.Position.X) - from.Nodes.Min(o => o.Position.X);
                dy = into.Nodes.Max(o => o.Position.Y) + 400 - from.Nodes.Min(o => o.Position.Y);
            }
            int next = into.Nodes.Count == 0 ? 0 : into.Nodes.Max(o => o.NodeID) + 1;
            Dictionary<(int, ShortGuid), int> ids = new Dictionary<(int, ShortGuid), int>();
            foreach (FlowgraphMeta.NodeMeta node in from.Nodes)
                if (!ids.ContainsKey((node.NodeID, node.EntityGUID)))
                    ids.Add((node.NodeID, node.EntityGUID), next++);
            int moved = 0, connections = 0;
            foreach (FlowgraphMeta.NodeMeta node in from.Nodes)
            {
                if (!ids.TryGetValue((node.NodeID, node.EntityGUID), out int id)) continue;
                FlowgraphMeta.NodeMeta copy = new FlowgraphMeta.NodeMeta()
                {
                    EntityGUID = node.EntityGUID,
                    NodeID = id,
                    Position = new Point(node.Position.X + dx, node.Position.Y + dy),
                    UnlinkedPins = node.UnlinkedPins.Select(o => new FlowgraphMeta.NodeMeta.UnlinkedPinMeta() { ParameterGUID = o.ParameterGUID, PinLocation = o.PinLocation, PinStyle = o.PinStyle }).ToList(),
                };
                foreach (FlowgraphMeta.NodeMeta.ConnectionMeta connection in node.ConnectionsOut)
                {
                    if (!ids.TryGetValue((connection.ConnectedNodeID, connection.ConnectedEntityGUID), out int to)) continue;
                    copy.ConnectionsOut.Add(new FlowgraphMeta.NodeMeta.ConnectionMeta()
                    {
                        ParameterGUID = connection.ParameterGUID,
                        ConnectedEntityGUID = connection.ConnectedEntityGUID,
                        ConnectedParameterGUID = connection.ConnectedParameterGUID,
                        ConnectedNodeID = to,
                    });
                    connections++;
                }
                into.Nodes.Add(copy);
                moved++;
            }
            pages.Remove(from);
            if (!RefactorPages.PagesMatchLinks(session.Composite, pages))
                throw new McpError("The pages of " + session.Composite.name + " would no longer draw exactly its links after the merge, so nothing was changed. layout_flowgraph (mode 'complete') brings them back in step.");

            FlowgraphLayoutManager.SetSelectedPage(session.Composite, into.Name);
            UndoStack.Current.Apply(new McpFlowgraphTools.PageLayoutEdit(session.Composite, pages, "AI: Merge page " + page.Name + " into " + into.Name));
            JObject result = PageSummary(session);
            result["deleted"] = page.Name;
            result["merged_into"] = into.Name;
            result["nodes_moved"] = moved;
            result["connections_moved"] = connections;
            if (moved != 0)
                result["offset"] = new JArray(dx, dy);
            return result;
        }

        private static object DuplicatePage(McpCall call, Session session)
        {
            FlowgraphMeta page = FindPage(session.Composite, session.Layouts, call.Str("page", required: true));
            string name = call.Has("name") ? NewPageName(session, call.Str("name"), null) : UniquePageName(session, page.Name + " (copy)");

            //Nodes only: a link is drawn exactly once across the pages, so the copy shows the pins its connections used, unconnected
            PinCatalogue catalogue = new PinCatalogue(session.Commands, session.Composite);
            List<FlowgraphMeta.NodeMeta> drawn = DrawnNodes(session.Composite, page);
            Dictionary<(int, ShortGuid), int> ids = NodeIds(drawn);
            List<ShortGuid>[] incoming = drawn.Select(o => new List<ShortGuid>()).ToArray();
            foreach (FlowgraphMeta.NodeMeta node in drawn)
                foreach (FlowgraphMeta.NodeMeta.ConnectionMeta connection in node.ConnectionsOut)
                    if (ids.TryGetValue((connection.ConnectedNodeID, connection.ConnectedEntityGUID), out int to))
                        incoming[to].Add(connection.ConnectedParameterGUID);

            FlowgraphMeta copy = new FlowgraphMeta()
            {
                CompositeGUID = session.Composite.shortGUID,
                Name = name,
                CanvasPosition = page.CanvasPosition,
                CanvasScale = page.CanvasScale <= 0 ? 1f : page.CanvasScale,
            };
            for (int i = 0; i < drawn.Count; i++)
            {
                Entity entity = session.Composite.GetEntityByID(drawn[i].EntityGUID);
                List<ShortGuid> outgoing = drawn[i].ConnectionsOut.Where(o => ids.ContainsKey((o.ConnectedNodeID, o.ConnectedEntityGUID))).Select(o => o.ParameterGUID).ToList();
                copy.Nodes.Add(new FlowgraphMeta.NodeMeta()
                {
                    EntityGUID = drawn[i].EntityGUID,
                    NodeID = i,
                    Position = drawn[i].Position,
                    UnlinkedPins = ShownPins(entity, drawn[i], outgoing, incoming[i], catalogue).Select(o => new FlowgraphMeta.NodeMeta.UnlinkedPinMeta()
                    {
                        ParameterGUID = o.pin,
                        PinLocation = (byte)o.side,
                        PinStyle = StyleOf(catalogue, entity, drawn[i], o.pin),
                    }).ToList(),
                });
            }
            UndoStack.Current.Apply(new PagePresenceEdit(session.Composite, copy, true, "AI: Duplicate page " + page.Name));
            session.Display.FindFlowgraph(name)?.Show();
            JObject result = PageSummary(session);
            result["duplicated"] = page.Name;
            result["as"] = name;
            result["nodes"] = copy.Nodes.Count;
            if (drawn.Any(o => o.ConnectionsOut.Count != 0))
                call.Note("The copy has the page's nodes but none of its connections: every link is drawn once, on the original page.");
            return result;
        }

        /// <summary>How a pin is drawn: as its schema has it, else as the node kept it, else an arrow.</summary>
        private static byte StyleOf(PinCatalogue catalogue, Entity entity, FlowgraphMeta.NodeMeta node, ShortGuid pin)
        {
            NodeUtils.PinPositionInfo info = catalogue.Find(entity, pin);
            if (info != null)
                return (byte)info.Style;
            FlowgraphMeta.NodeMeta.UnlinkedPinMeta kept = node.UnlinkedPins.FirstOrDefault(o => o.ParameterGUID == pin);
            return kept != null ? kept.PinStyle : (byte)PinStyle.ArrowDown;
        }

        private static object ReorderPages(McpCall call, Session session)
        {
            List<FlowgraphMeta> current = session.Layouts;
            List<string> order = call.StrList("order", required: true);
            if (order.Count == 0)
                throw new McpError("'order' is empty: list page names in the order wanted.");
            List<FlowgraphMeta> listed = new List<FlowgraphMeta>();
            foreach (string name in order)
            {
                FlowgraphMeta page = FindPage(session.Composite, current, name);
                if (listed.Contains(page))
                    throw new McpError("'" + page.Name + "' is in 'order' twice.");
                listed.Add(page);
            }
            List<FlowgraphMeta> reordered = listed.Concat(current.Where(o => !listed.Contains(o))).ToList();
            JObject result;
            if (reordered.SequenceEqual(current))
            {
                result = PageSummary(session);
                result["unchanged"] = "The pages are already in that order.";
                return result;
            }
            List<FlowgraphMeta> pages = reordered.Select(o => o.Copy()).ToList();
            if (!RefactorPages.PagesMatchLinks(session.Composite, pages))
                throw new McpError("The pages of " + session.Composite.name + " do not draw exactly its links right now, so nothing was changed. layout_flowgraph (mode 'complete') brings them back in step.");
            UndoStack.Current.Apply(new McpFlowgraphTools.PageLayoutEdit(session.Composite, pages, "AI: Reorder pages of " + McpScript.CompositeLeaf(session.Composite)));
            result = PageSummary(session);
            result["reordered"] = true;
            return result;
        }

        private static object ShowPage(McpCall call, Session session)
        {
            FlowgraphMeta page = FindPage(session.Composite, session.Layouts, call.Str("page", required: true));
            session.Live(page.Name).Show();
            FlowgraphLayoutManager.SetSelectedPage(session.Composite, page.Name);
            JObject result = PageSummary(session);
            result["shown"] = page.Name;
            return result;
        }
        #endregion

        #region Nodes
        /// <summary>A node on an open page, and its id there.</summary>
        private sealed class LiveNode
        {
            public Flowgraph Page;
            public STNode Node;
            public int Id;
            public Entity Entity;
        }

        private static object EditNodes(McpCall call)
        {
            string action = Choice(call, "action", _nodeActions);
            string links = call.Str("links");
            if (links != null && action != "remove")
                throw new McpError("'links' only goes with action 'remove'.");
            if (links != null)
                links = Choice(call, "links", new[] { "drop", "redraw" });
            JArray specs = call.Array("nodes", required: true);
            if (specs.Count == 0)
                throw new McpError("'nodes' is empty.");
            if (specs.Count > MaxNodesPerCall)
                throw new McpError("At most " + MaxNodesPerCall + " nodes per call.");
            List<JObject> items = new List<JObject>();
            for (int i = 0; i < specs.Count; i++)
            {
                if (!(specs[i] is JObject spec))
                    throw new McpError("nodes[" + i + "] must be an object, e.g. {\"entity\": \"Trigger\"}.");
                foreach (JProperty key in spec.Properties())
                    if (!_nodeKeysFor[action].Contains(key.Name))
                        throw new McpError("nodes[" + i + "]: '" + key.Name + "' does not go with action '" + action + "' (a node takes: " + string.Join(", ", _nodeKeysFor[action]) + ").");
                items.Add(spec);
            }

            return McpEditor.UI<object>(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                Session session = Open(commands, composite, edit: true);
                switch (action)
                {
                    case "add": return AddNodes(call, session, items);
                    case "move": return MoveNodes(call, session, items);
                    case "remove": return RemoveNodes(call, session, items, links);
                    default: return SetPins(call, session, items);
                }
            });
        }

        private static string Text(JToken token) => token.Type == JTokenType.String ? (string)token : token.ToString(Newtonsoft.Json.Formatting.None);

        private static double? Number(JObject spec, string key, int index)
        {
            JToken token = spec[key];
            if (token == null || token.Type == JTokenType.Null)
                return null;
            if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
                throw new McpError("nodes[" + index + "]." + key + " must be a number.");
            double value = (double)token;
            if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > 10000000)
                throw new McpError("nodes[" + index + "]." + key + " is out of range.");
            return value;
        }

        private static string Label(Session session, Entity entity) => McpScript.EntityName(session.Commands, session.Composite, entity);

        /// <summary>The node a spec names: by page + id, or by entity (which must then have one node where it is looked for).</summary>
        private static LiveNode FindNode(Session session, FlowgraphMeta page, JObject spec, int index)
        {
            Entity entity = spec["entity"] != null && spec["entity"].Type != JTokenType.Null ? McpScript.FindEntity(session.Commands, session.Composite, Text(spec["entity"])) : null;
            JToken idToken = spec["node"];
            if (entity == null && (idToken == null || idToken.Type == JTokenType.Null))
                throw new McpError("nodes[" + index + "] needs 'entity' or 'node'.");

            if (idToken != null && idToken.Type != JTokenType.Null)
            {
                if (idToken.Type != JTokenType.Integer)
                    throw new McpError("nodes[" + index + "].node must be a whole number (a node id from get_flowgraph).");
                if (page == null)
                    throw new McpError("nodes[" + index + "]: node ids are numbered per page, so give 'page' too.");
                Flowgraph live = session.Live(page.Name);
                long id = (long)idToken;
                if (id < 0 || id >= live.Nodegraph.Nodes.Count)
                    throw new McpError("Page '" + page.Name + "' has no node " + id + " (its nodes are 0 to " + (live.Nodegraph.Nodes.Count - 1) + "; get_flowgraph lists them).");
                STNode node = live.Nodegraph.Nodes[(int)id];
                if (node?.Entity == null)
                    throw new McpError("Node " + id + " on page '" + page.Name + "' has no entity.");
                if (entity != null && node.ShortGUID != entity.shortGUID)
                    throw new McpError("Node " + id + " on page '" + page.Name + "' is " + Label(session, node.Entity) + ", not " + Label(session, entity) + ". The page has changed since it was read: read it again with get_flowgraph.");
                return new LiveNode() { Page = live, Node = node, Id = (int)id, Entity = node.Entity };
            }

            List<Flowgraph> pages = page != null ? new List<Flowgraph>() { session.Live(page.Name) } : session.LivePages();
            List<LiveNode> found = new List<LiveNode>();
            foreach (Flowgraph live in pages)
            {
                for (int i = 0; i < live.Nodegraph.Nodes.Count; i++)
                {
                    STNode node = live.Nodegraph.Nodes[i];
                    if (node?.Entity != null && node.ShortGUID == entity.shortGUID)
                        found.Add(new LiveNode() { Page = live, Node = node, Id = i, Entity = node.Entity });
                }
            }
            if (found.Count == 0)
                throw new McpError(Label(session, entity) + " has no node " + (page != null ? "on page '" + page.Name + "'" : "on any page of " + session.Composite.name) + ". (action 'add' gives it one.)");
            if (found.Count > 1)
                throw new McpError(Label(session, entity) + " has " + found.Count + " nodes: " + string.Join(", ", found.Take(20).Select(o => "page '" + o.Page.FlowgraphName + "' node " + o.Id)) + ". Pick one with 'page' and 'node'.");
            return found[0];
        }

        private static List<LiveNode> FindNodes(Session session, McpCall call, List<JObject> items)
        {
            FlowgraphMeta page = call.Has("page") ? FindPage(session.Composite, session.Layouts, call.Str("page")) : null;
            List<LiveNode> nodes = new List<LiveNode>();
            for (int i = 0; i < items.Count; i++)
            {
                LiveNode node = FindNode(session, page, items[i], i);
                if (nodes.Any(o => o.Node == node.Node))
                    throw new McpError("nodes[" + i + "] names the same node as an earlier entry (page '" + node.Page.FlowgraphName + "' node " + node.Id + ").");
                nodes.Add(node);
            }
            return nodes;
        }

        private static JObject Describe(Session session, LiveNode node)
        {
            return new JObject()
            {
                ["page"] = node.Page.FlowgraphName,
                ["node"] = node.Page.Nodegraph.Nodes.IndexOf(node.Node) is int at && at >= 0 ? at : node.Id,
                ["entity"] = McpScript.Id(node.Entity.shortGUID),
                ["name"] = Label(session, node.Entity),
                ["x"] = node.Node.Location.X,
                ["y"] = node.Node.Location.Y,
            };
        }

        /// <summary>The pins a spec's 'pins' value picks from the entity's pin catalogue ('all', 'linked', or names).</summary>
        private static List<PinSnapshot> PickPins(JToken choice, Entity entity, PinCatalogue catalogue, Session session, int index)
        {
            if (choice.Type == JTokenType.String)
            {
                string word = ((string)choice).Trim().ToLowerInvariant();
                if (word == "all")
                    return catalogue.All(entity).Select(o => new PinSnapshot() { Parameter = o.ParameterGUID, Location = o.Location, Style = o.Style }).ToList();
                if (word == "linked" || word == "none")
                    return new List<PinSnapshot>();
            }
            if (!(choice is JArray names))
                throw new McpError("nodes[" + index + "].pins must be 'all', 'linked', or a list of pin names.");
            List<PinSnapshot> pins = new List<PinSnapshot>();
            foreach (JToken name in names)
            {
                NodeUtils.PinPositionInfo info = CataloguePin(catalogue, entity, Text(name), session, index);
                if (!pins.Any(o => o.Parameter == info.ParameterGUID))
                    pins.Add(new PinSnapshot() { Parameter = info.ParameterGUID, Location = info.Location, Style = info.Style });
            }
            return pins;
        }

        private static NodeUtils.PinPositionInfo CataloguePin(PinCatalogue catalogue, Entity entity, string name, Session session, int index)
        {
            NodeUtils.PinPositionInfo info = catalogue.Find(entity, McpScript.ParamId(name));
            if (info == null)
                throw new McpError("nodes[" + index + "]: " + Label(session, entity) + " (" + McpScript.TypeName(session.Commands, session.Composite, entity) + ") has no pin '" + name.Trim() + "' the flowgraph can show. Its pins: " + catalogue.Names(entity) + ".");
            return info;
        }

        private static object AddNodes(McpCall call, Session session, List<JObject> items)
        {
            FlowgraphMeta page = call.Has("page") ? FindPage(session.Composite, session.Layouts, call.Str("page")) : DefaultPage(session);
            Flowgraph live = session.Live(page.Name);
            PinCatalogue catalogue = new PinCatalogue(session.Commands, session.Composite);
            //The editor's own choice for a node dropped on the canvas
            JToken defaultPins = SettingsManager.GetBool(Settings.PopulateAllPinsOnCreateNode) ? "all" : "linked";

            //Everything checked before anything changes
            List<(Entity entity, Point? at, List<PinSnapshot> pins)> plan = new List<(Entity, Point?, List<PinSnapshot>)>();
            for (int i = 0; i < items.Count; i++)
            {
                JObject spec = items[i];
                if (spec["entity"] == null || spec["entity"].Type == JTokenType.Null)
                    throw new McpError("nodes[" + i + "] needs 'entity': the entity to give a node.");
                Entity entity = McpScript.FindEntity(session.Commands, session.Composite, Text(spec["entity"]));
                double? x = Number(spec, "x", i), y = Number(spec, "y", i);
                if ((x == null) != (y == null))
                    throw new McpError("nodes[" + i + "]: give both x and y, or neither (to place it beside the page's nodes).");
                Point? at = x == null ? (Point?)null : new Point((int)Math.Round(x.Value), (int)Math.Round(y.Value));
                JToken choice = spec["pins"] == null || spec["pins"].Type == JTokenType.Null ? defaultPins : spec["pins"];
                plan.Add((entity, at, PickPins(choice, entity, catalogue, session, i)));
            }

            //Unplaced nodes go in a column to the right of what the page holds
            List<STNode> existing = live.Nodegraph.Nodes.Cast<STNode>().Where(o => o != null).ToList();
            int columnX = existing.Count == 0 ? 0 : existing.Max(o => o.Left + o.Width) + 150;
            int nextY = existing.Count == 0 ? 0 : existing.Min(o => o.Top);

            JArray added = new JArray();
            List<string> already = new List<string>();
            using (UndoStack.Current.BeginGroup("AI: Add " + UndoLabels.Count(plan.Count, "node", "nodes") + " to " + page.Name))
            {
                foreach ((Entity entity, Point? at, List<PinSnapshot> pins) in plan)
                {
                    if (live.Nodegraph.Nodes.Cast<STNode>().Any(o => o?.Entity != null && o.ShortGUID == entity.shortGUID))
                        already.Add(Label(session, entity));
                    Point where = at ?? new Point(columnX, nextY);
                    //A node the page does not have yet: the undo record puts it on the page from this snapshot, as it would on a redo
                    NodeRef self = new NodeRef(new STNode() { Entity = entity }) { Node = null, Location = where };
                    NodeSnapshot snapshot = new NodeSnapshot()
                    {
                        Page = page.Name,
                        Self = self,
                        NodeID = live.Nodegraph.Nodes.Count,
                        Location = where,
                        Pins = pins,
                    };
                    UndoStack.Current.Apply(new NodePresenceEdit(session.Composite, page.Name, snapshot, true, "Add node for " + UndoLabels.Entity(session.Composite, entity)));
                    STNode made = snapshot.Self?.Node;
                    if (made == null || live.Nodegraph.Nodes.IndexOf(made) < 0)
                        throw new McpError("The page did not take a node for " + Label(session, entity) + ".");
                    if (at == null)
                        nextY = made.Top + Math.Max(made.Height, 40) + 40;
                    JObject line = Describe(session, new LiveNode() { Page = live, Node = made, Entity = entity });
                    line["pins"] = made.GetAllOptions().Count(o => o != null && o != STNodeOption.Empty);
                    added.Add(line);
                }
            }
            live.Show();
            JObject result = new JObject() { ["composite"] = session.Composite.name, ["page"] = page.Name, ["added"] = added };
            if (already.Count != 0)
                call.Note("Already on the page, and now there twice (the editor marks such nodes): " + string.Join(", ", already.Distinct()) + ".");
            return result;
        }

        private static object MoveNodes(McpCall call, Session session, List<JObject> items)
        {
            List<LiveNode> nodes = FindNodes(session, call, items);
            Dictionary<Flowgraph, List<NodeMoveEdit.Movement>> moves = new Dictionary<Flowgraph, List<NodeMoveEdit.Movement>>();
            List<Flowgraph> order = new List<Flowgraph>();
            for (int i = 0; i < items.Count; i++)
            {
                double? x = Number(items[i], "x", i), y = Number(items[i], "y", i), dx = Number(items[i], "dx", i), dy = Number(items[i], "dy", i);
                bool absolute = x != null || y != null, relative = dx != null || dy != null;
                if (absolute && relative)
                    throw new McpError("nodes[" + i + "]: give x and y (where it goes) or dx/dy (how far it moves), not both.");
                if (!absolute && !relative)
                    throw new McpError("nodes[" + i + "]: say where it goes (x and y) or how far it moves (dx, dy).");
                if (absolute && (x == null || y == null))
                    throw new McpError("nodes[" + i + "]: give both x and y.");
                Point from = nodes[i].Node.Location;
                Point to = absolute ? new Point((int)Math.Round(x.Value), (int)Math.Round(y.Value)) : new Point(from.X + (int)Math.Round(dx ?? 0), from.Y + (int)Math.Round(dy ?? 0));
                if (to == from) continue;
                if (!moves.TryGetValue(nodes[i].Page, out List<NodeMoveEdit.Movement> list))
                {
                    moves.Add(nodes[i].Page, list = new List<NodeMoveEdit.Movement>());
                    order.Add(nodes[i].Page);
                }
                list.Add(new NodeMoveEdit.Movement() { Node = new NodeRef(nodes[i].Node), From = from, To = to });
            }

            JObject result = new JObject() { ["composite"] = session.Composite.name };
            int count = moves.Values.Sum(o => o.Count);
            if (count == 0)
            {
                result["unchanged"] = "Every node is already there.";
                return result;
            }
            //Dragging nodes on the canvas: one record per page, as one step
            using (UndoStack.Current.BeginGroup("AI: Move " + UndoLabels.Count(count, "node", "nodes")))
                foreach (Flowgraph page in order)
                    UndoStack.Current.Apply(new NodeMoveEdit(session.Composite, page.FlowgraphName, moves[page]));
            result["moved"] = new JArray(nodes.Select(o => Describe(session, o)));
            return result;
        }

        private static object RemoveNodes(McpCall call, Session session, List<JObject> items, string links)
        {
            List<LiveNode> nodes = FindNodes(session, call, items);
            HashSet<STNode> going = new HashSet<STNode>(nodes.Select(o => o.Node));

            //What the nodes carry: each connection once, named from its feeding end (right or top)
            List<string> carried = new List<string>();
            HashSet<(STNodeOption, STNodeOption)> counted = new HashSet<(STNodeOption, STNodeOption)>();
            foreach (LiveNode node in nodes)
            {
                foreach (STNodeOption option in node.Node.GetAllOptions())
                {
                    if (option == null || option == STNodeOption.Empty) continue;
                    foreach (STNodeOption peer in option.GetConnectedOption() ?? new List<STNodeOption>())
                    {
                        if (peer?.Owner?.Entity == null) continue;
                        bool feeds = option.Location == PinLocation.Right || option.Location == PinLocation.Top;
                        STNodeOption source = feeds ? option : peer, target = feeds ? peer : option;
                        if (!counted.Add((source, target))) continue;
                        carried.Add(LinkText(session.Commands, session.Composite, source.Owner.ShortGUID, source.ShortGUID, target.Owner.ShortGUID, target.ShortGUID));
                    }
                }
            }
            if (carried.Count != 0 && links == null)
                throw new McpError("The node(s) carry " + carried.Count + " connection(s):\n- " + string.Join("\n- ", Capped(carried, 30).Select(o => (string)o)) +
                    "\nPass links: 'drop' to take those links out of the script with the nodes, or 'redraw' to draw them again at other nodes (a node is made beside the other end where none exists). Nothing was changed.");

            JArray removed = new JArray(nodes.Select(o => Describe(session, o)));
            HashSet<ShortGuid> entities = new HashSet<ShortGuid>(nodes.Select(o => o.Entity.shortGUID));
            JObject result = new JObject() { ["composite"] = session.Composite.name, ["removed"] = removed };

            if (carried.Count != 0 && links == "redraw")
            {
                //No such thing in the editor: the pages without the nodes, with what they carried drawn again elsewhere
                List<FlowgraphMeta> pages = session.Layouts.Select(o => o.Copy()).ToList();
                foreach (IGrouping<Flowgraph, LiveNode> group in nodes.GroupBy(o => o.Page))
                {
                    FlowgraphMeta layout = pages.FirstOrDefault(o => o.Name == group.Key.FlowgraphName);
                    if (layout == null)
                        throw new McpError("The page '" + group.Key.FlowgraphName + "' is out of step with the editor. Try again.");
                    HashSet<int> gone = new HashSet<int>();
                    foreach (LiveNode node in group)
                    {
                        FlowgraphMeta.NodeMeta meta = node.Id < layout.Nodes.Count ? layout.Nodes[node.Id] : null;
                        if (meta == null || meta.EntityGUID != node.Entity.shortGUID || meta.NodeID != node.Id)
                            throw new McpError("The page '" + layout.Name + "' is out of step with the editor. Try again.");
                        gone.Add(meta.NodeID);
                    }
                    layout.Nodes.RemoveAll(o => gone.Contains(o.NodeID));
                    foreach (FlowgraphMeta.NodeMeta other in layout.Nodes)
                        other.ConnectionsOut.RemoveAll(o => gone.Contains(o.ConnectedNodeID));
                }
                List<FlowgraphMeta> redrawn = RefactorPages.DrawLinks(session.Composite, pages, nodes[0].Page.FlowgraphName, session.Commands);
                if (!RefactorPages.PagesMatchLinks(session.Composite, redrawn))
                    throw new McpError("The connections could not all be drawn again, so nothing was changed. Use links: 'drop', or move the nodes instead.");
                UndoStack.Current.Apply(new McpFlowgraphTools.PageLayoutEdit(session.Composite, redrawn, "AI: Remove " + UndoLabels.Count(nodes.Count, "node", "nodes")));
                result["redrawn_links"] = Capped(carried, 50);
                result["entities_without_nodes"] = Capped(Unplaced(session.Composite, redrawn).Where(o => entities.Contains(o.shortGUID)).Select(o => Label(session, o) + " (" + McpScript.Id(o.shortGUID) + ")"), 50);
                call.Note("The connections were drawn again beside other nodes of their ends; get_flowgraph shows where.");
            }
            else
            {
                //The canvas's Delete Node: each node snapshotted and taken off, as one step (the entities stay)
                using (UndoStack.Current.BeginGroup("AI: Remove " + UndoLabels.Count(nodes.Count, "node", "nodes")))
                {
                    foreach (LiveNode node in nodes)
                    {
                        NodeSnapshot snapshot = node.Page.SnapshotNode(node.Node);
                        UndoStack.Current.Apply(new NodePresenceEdit(session.Composite, node.Page.FlowgraphName, snapshot, false, "Remove node for " + UndoLabels.Entity(session.Composite, node.Entity)));
                    }
                }
                //The links the nodes drew leave the script when the pages are next compiled: now
                session.Display.SaveAllFlowgraphs();
                if (carried.Count != 0)
                    result["dropped_links"] = Capped(carried, 50);
                result["entities_without_nodes"] = Capped(Unplaced(session.Composite, session.Layouts).Where(o => entities.Contains(o.shortGUID)).Select(o => Label(session, o) + " (" + McpScript.Id(o.shortGUID) + ")"), 50);
            }
            if (((JArray)result["entities_without_nodes"]).Count == 0)
                result.Remove("entities_without_nodes");
            else
                call.Note("Entities with no node left stay in the composite (delete_entities removes one); edit_flowgraph_nodes action 'add' gives one a node again.");
            return result;
        }

        private static object SetPins(McpCall call, Session session, List<JObject> items)
        {
            List<LiveNode> nodes = FindNodes(session, call, items);
            PinCatalogue catalogue = new PinCatalogue(session.Commands, session.Composite);
            List<(LiveNode node, PinSet before, PinSet after)> changes = new List<(LiveNode, PinSet, PinSet)>();
            List<string> kept = new List<string>();
            for (int i = 0; i < items.Count; i++)
            {
                JObject spec = items[i];
                LiveNode node = nodes[i];
                if (spec["pins"] == null && spec["add_pins"] == null && spec["remove_pins"] == null)
                    throw new McpError("nodes[" + i + "]: say which pins with 'pins', 'add_pins' or 'remove_pins'.");
                bool variable = node.Entity is VariableEntity;
                PinSet before = node.Page.SnapshotPins(node.Node);
                HashSet<ShortGuid> connected = new HashSet<ShortGuid>(node.Node.GetAllOptions()
                    .Where(o => o != null && o != STNodeOption.Empty && (o.GetConnectedOption()?.Count ?? 0) != 0).Select(o => o.ShortGUID));
                //A variable's node always shows its one pin, as the editor draws it
                HashSet<ShortGuid> fixedPins = new HashSet<ShortGuid>(connected);
                if (variable)
                    fixedPins.UnionWith(catalogue.All(node.Entity).Select(o => o.ParameterGUID));

                List<PinSnapshot> pins = before.Pins.Select(o => new PinSnapshot() { Parameter = o.Parameter, Location = o.Location, Style = o.Style }).ToList();
                void Show(NodeUtils.PinPositionInfo info)
                {
                    if (!pins.Any(o => o.Parameter == info.ParameterGUID))
                        pins.Add(new PinSnapshot() { Parameter = info.ParameterGUID, Location = info.Location, Style = info.Style });
                }

                JToken choice = spec["pins"];
                if (choice != null && choice.Type != JTokenType.Null)
                {
                    string word = choice.Type == JTokenType.String ? ((string)choice).Trim().ToLowerInvariant() : null;
                    if (word == "all")
                        catalogue.All(node.Entity).ForEach(Show);
                    else if (word == "linked" || word == "none")
                        pins.RemoveAll(o => !fixedPins.Contains(o.Parameter));
                    else if (choice is JArray names)
                    {
                        List<ShortGuid> wanted = new List<ShortGuid>();
                        foreach (JToken name in names)
                        {
                            ShortGuid id = McpScript.ParamId(Text(name));
                            if (!pins.Any(o => o.Parameter == id))
                                Show(CataloguePin(catalogue, node.Entity, Text(name), session, i));
                            wanted.Add(id);
                        }
                        List<string> stay = pins.Where(o => !wanted.Contains(o.Parameter) && fixedPins.Contains(o.Parameter) && connected.Contains(o.Parameter)).Select(o => McpScript.ParamName(o.Parameter)).ToList();
                        if (stay.Count != 0)
                            kept.Add(Label(session, node.Entity) + ": " + string.Join(", ", stay));
                        pins.RemoveAll(o => !wanted.Contains(o.Parameter) && !fixedPins.Contains(o.Parameter));
                    }
                    else
                        throw new McpError("nodes[" + i + "].pins must be 'all', 'linked', or a list of pin names.");
                }
                foreach (JToken name in spec["add_pins"] as JArray ?? (spec["add_pins"] == null || spec["add_pins"].Type == JTokenType.Null ? new JArray() : new JArray(spec["add_pins"])))
                {
                    ShortGuid id = McpScript.ParamId(Text(name));
                    if (!pins.Any(o => o.Parameter == id))
                        Show(CataloguePin(catalogue, node.Entity, Text(name), session, i));
                }
                foreach (JToken name in spec["remove_pins"] as JArray ?? (spec["remove_pins"] == null || spec["remove_pins"].Type == JTokenType.Null ? new JArray() : new JArray(spec["remove_pins"])))
                {
                    ShortGuid id = McpScript.ParamId(Text(name));
                    if (connected.Contains(id))
                        throw new McpError("nodes[" + i + "]: '" + Text(name) + "' on " + Label(session, node.Entity) + " carries a connection, so it stays. Take the link off first (remove_links) to hide it.");
                    if (fixedPins.Contains(id))
                        throw new McpError("nodes[" + i + "]: a variable's node always shows its pin '" + Text(name) + "'.");
                    pins.RemoveAll(o => o.Parameter == id);
                }

                PinSet after = new PinSet() { Location = before.Location, Pins = pins };
                if (!PinSet.Same(before, after))
                    changes.Add((node, before, after));
            }

            JObject result = new JObject() { ["composite"] = session.Composite.name };
            if (kept.Count != 0)
                call.Note("Connected pins always stay on their node: " + string.Join("; ", kept) + ".");
            if (changes.Count == 0)
            {
                result["unchanged"] = "The nodes already show those pins.";
                return result;
            }
            //The node menu's Add All Pins / Remove Unused Pins / Manage Pins: one record per node, as one step
            using (UndoStack.Current.BeginGroup("AI: Change pins on " + UndoLabels.Count(changes.Count, "node", "nodes")))
                foreach ((LiveNode node, PinSet before, PinSet after) in changes)
                    UndoStack.Current.Apply(new PinSetEdit(session.Composite, node.Page.FlowgraphName, new NodeRef(node.Node), before, after, "Change pins on " + UndoLabels.Entity(session.Composite, node.Entity)));
            result["changed"] = new JArray(changes.Select(o =>
            {
                JObject line = Describe(session, o.node);
                line["pins"] = new JArray(o.node.Node.GetAllOptions().Where(p => p != null && p != STNodeOption.Empty).Select(p => Side(p.Location) + ":" + McpScript.ParamName(p.ShortGUID)));
                return line;
            }));
            return result;
        }
        #endregion

        #region Picture
        private static object Capture(McpCall call)
        {
            int maxWidth = Math.Min(4096, Math.Max(200, call.Int("max_width", 1600)));
            string caption = null;
            byte[] png;
            using (McpEditorTools.Heartbeat(call, "Drawing the page"))
            {
                png = McpEditor.UI(() =>
                {
                    Commands commands = McpEditor.RequireCommands();
                    Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                    Session session = Open(commands, composite, edit: false);
                    FlowgraphMeta page = call.Has("page") ? FindPage(composite, session.Layouts, call.Str("page")) : DefaultPage(session);
                    Flowgraph live = session.Live(page.Name);
                    live.Show();
                    STNodeEditor editor = live.Nodegraph;
                    List<STNode> nodes = editor.Nodes.Cast<STNode>().Where(o => o != null).ToList();
                    if (nodes.Count == 0)
                        throw new McpError("Page '" + page.Name + "' has no nodes to picture.");
                    if (!editor.Created || editor.Width < 50 || editor.Height < 50)
                        throw new McpError("The page '" + page.Name + "' is not showing in the editor (it may be too small, or not drawn yet). Try again.");

                    Rectangle bounds = Bounds(nodes);
                    string around = null;
                    if (call.Has("entity"))
                    {
                        Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity"));
                        List<STNode> own = nodes.Where(o => o.Entity != null && o.ShortGUID == entity.shortGUID).ToList();
                        if (own.Count == 0)
                            throw new McpError(McpScript.EntityName(commands, composite, entity) + " has no node on page '" + page.Name + "'.");
                        Rectangle near = Bounds(own);
                        near.Inflate(1400, 900);
                        bounds = near;
                        around = McpScript.EntityName(commands, composite, entity);
                    }
                    bounds.Inflate(60, 60);
                    bool cropped = false;
                    const int MaxSide = 40000;
                    if (bounds.Width > MaxSide || bounds.Height > MaxSide)
                    {
                        bounds = new Rectangle(bounds.X, bounds.Y, Math.Min(bounds.Width, MaxSide), Math.Min(bounds.Height, MaxSide));
                        cropped = true;
                    }

                    double scale = Math.Min(1.0, Math.Min(maxWidth / (double)bounds.Width, maxWidth * 2.0 / bounds.Height));
                    byte[] data = Render(editor, bounds, scale);
                    caption = "Page '" + page.Name + "' of " + composite.name + (around != null ? ", around " + around : "") + ": " + nodes.Count + " node(s); canvas " + bounds.X + "," + bounds.Y + " to " + bounds.Right + "," + bounds.Bottom +
                        " drawn at " + Math.Round(scale * 100) + "%." + (cropped ? " The page is larger than " + MaxSide + " units, so only its top-left part is shown: pass 'entity' to picture another area." : "");
                    return data;
                });
            }
            return new McpImage() { Data = png, MimeType = "image/png", Caption = caption };
        }

        private static Rectangle Bounds(IEnumerable<STNode> nodes)
        {
            Rectangle bounds = Rectangle.Empty;
            foreach (STNode node in nodes)
                bounds = bounds.IsEmpty ? node.Rectangle : Rectangle.Union(bounds, node.Rectangle);
            return bounds;
        }

        /// <summary>
        /// The canvas area drawn into a picture. The editor only draws the connections that cross the area it has
        /// on screen, so it is zoomed out as far as it goes and walked across the area tile by tile; its view,
        /// grid and edge markers are put back afterwards.
        /// </summary>
        private static byte[] Render(STNodeEditor editor, Rectangle bounds, double scale)
        {
            int width = Math.Max(1, (int)(bounds.Width * scale)), height = Math.Max(1, (int)(bounds.Height * scale));
            //The editor never draws a tile below half size: smaller pictures are drawn at half and shrunk
            float tileScale = (float)Math.Max(0.5, scale);
            float viewScale = editor.CanvasScale;
            PointF viewCentre = editor.CanvasCenter;
            bool grid = editor.ShowGrid, markers = editor.ShowLocation;
            using (Bitmap picture = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                using (Graphics g = Graphics.FromImage(picture))
                {
                    g.Clear(editor.BackColor);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    try
                    {
                        editor.ShowGrid = false;
                        editor.ShowLocation = false;
                        editor.ScaleCanvas(0.2f, 0, 0);
                        float view = editor.CanvasScale;
                        int tileWidth = Math.Max(200, (int)(editor.Width / view) - 120);
                        int tileHeight = Math.Max(200, (int)(editor.Height / view) - 120);
                        for (int top = bounds.Top; top < bounds.Bottom; top += tileHeight)
                        {
                            for (int left = bounds.Left; left < bounds.Right; left += tileWidth)
                            {
                                Rectangle tile = new Rectangle(left, top, Math.Min(tileWidth, bounds.Right - left), Math.Min(tileHeight, bounds.Bottom - top));
                                editor.CenterCanvasOn(tile.X + tile.Width / 2f, tile.Y + tile.Height / 2f, false);
                                using (Image part = editor.GetCanvasImage(tile, tileScale))
                                {
                                    RectangleF into = new RectangleF((float)((tile.X - bounds.X) * scale), (float)((tile.Y - bounds.Y) * scale), (float)(tile.Width * scale), (float)(tile.Height * scale));
                                    g.DrawImage(part, into);
                                }
                            }
                        }
                    }
                    finally
                    {
                        editor.ShowGrid = grid;
                        editor.ShowLocation = markers;
                        editor.ScaleCanvas(viewScale, 0, 0);
                        editor.CenterCanvasOn(viewCentre.X, viewCentre.Y, false);
                    }
                }
                using (MemoryStream stream = new MemoryStream())
                {
                    picture.Save(stream, ImageFormat.Png);
                    return stream.ToArray();
                }
            }
        }
        #endregion

        private static string Choice(McpCall call, string name, string[] options)
        {
            string value = call.Str(name, required: true).Trim();
            string match = options.FirstOrDefault(o => string.Equals(o, value, StringComparison.OrdinalIgnoreCase));
            if (match == null)
                throw new McpError("'" + name + "' must be one of: " + string.Join(", ", options) + ".");
            return match;
        }
    }
}
