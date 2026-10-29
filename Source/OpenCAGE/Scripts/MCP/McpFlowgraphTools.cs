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
using System.Linq;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE.MCP
{
    /// <summary>A composite's script pages: reading them, and laying them out so they draw every link.</summary>
    internal static class McpFlowgraphTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "get_flowgraph",
                Title = "Get flowgraph pages",
                Description = "A composite's script pages as the flowgraph editor draws them: per page its canvas view and nodes (node id, entity, type, position, pins by side, pin delays, outgoing connections by node id, dead/multi-node flags) and its links as text; plus entities with no node. Node ids are per page (edit_flowgraph_nodes takes them). limit/offset page through the nodes. Like opening the composite in the editor, the first read tidies away aliases and instances that point at nothing.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id).", required: true),
                    McpSchema.String("page", "Only this page."),
                    McpSchema.Limit(150, "nodes, across the pages", 1000),
                    McpSchema.Offset("nodes, across the pages")),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() => Read(call)),
            };

            yield return new McpTool()
            {
                Name = "layout_flowgraph",
                Title = "Lay out flowgraph pages",
                Description = "Give a composite script pages that draw every one of its links, so it can be edited in the flowgraph editor: 'complete' keeps what its pages already draw and adds the rest; 'rebuild' lays everything out afresh on one page. Use it for a composite shown as a plain link list (its pages missing or out of step). Refuses (listing them) if some links cannot be drawn. One undo step.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id).", required: true),
                    McpSchema.String("mode", "'complete' (default) or 'rebuild'.", options: new[] { "complete", "rebuild" }),
                    McpSchema.String("page", "The page to draw new links on (default: the composite's name).")),
                Idempotent = true,
                Run = call => McpEditor.UI(() => Layout(call)),
            };
        }

        /// <summary>
        /// The pages as the editor would draw them: only nodes whose entity the composite has, numbered by their
        /// place on the page (the ids the editor gives them when it saves the page, and edit_flowgraph_nodes takes),
        /// with the pins each shows - its connections' pins and the unlinked ones it keeps.
        /// </summary>
        private static object Read(McpCall call)
        {
            Commands commands = McpEditor.RequireCommands(forEditing: false);
            Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
            McpBrowseTools.CompileIfShown(composite);

            //What the first open does (drop aliases and instances that point at nothing), so nodes are numbered as the
            //editor numbers them once the composite is open - which edit_flowgraph_nodes does. Never under a save.
            if (!commands.Utils.PurgedComposites.purged.Contains(composite.shortGUID) && !UndoStack.Current.Blocked)
            {
                commands.Utils.PurgeDeadLinks(composite);
                commands.Utils.PurgedComposites.purged.Add(composite.shortGUID);
            }

            int limit = McpPaging.Limit(call, 150, 1000);
            int offset = McpPaging.Offset(call);

            List<FlowgraphMeta> all = FlowgraphLayoutManager.GetLayouts(composite);
            List<FlowgraphMeta> chosen = call.Has("page") ? new List<FlowgraphMeta>() { McpPageTools.FindPage(composite, all, call.Str("page")) } : all;
            McpPageTools.PinCatalogue catalogue = new McpPageTools.PinCatalogue(commands, composite);

            //An entity drawn more than once across the pages gets the editor's multi-node marker
            Dictionary<ShortGuid, int> nodeCounts = new Dictionary<ShortGuid, int>();
            foreach (FlowgraphMeta page in all)
                foreach (FlowgraphMeta.NodeMeta node in McpPageTools.DrawnNodes(composite, page))
                    nodeCounts[node.EntityGUID] = (nodeCounts.TryGetValue(node.EntityGUID, out int count) ? count : 0) + 1;

            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            bool onScreen = display != null && !display.IsDisposed && display.Populated && display.Composite == composite;
            string visible = onScreen ? display.Flowgraphs.FirstOrDefault(o => o != null && !o.IsDisposed && o.Visible)?.FlowgraphName : null;
            string remembered = FlowgraphLayoutManager.GetSelectedPage(composite);

            JArray pages = new JArray();
            int seen = 0, listed = 0, total = 0;
            foreach (FlowgraphMeta page in chosen)
            {
                List<FlowgraphMeta.NodeMeta> drawn = McpPageTools.DrawnNodes(composite, page);
                Dictionary<(int, ShortGuid), int> ids = McpPageTools.NodeIds(drawn);
                total += drawn.Count;

                //Connections the page draws (both ends drawn, and the link there to draw), and the pins they come into
                List<(int from, FlowgraphMeta.NodeMeta.ConnectionMeta connection, int to)> connections = new List<(int, FlowgraphMeta.NodeMeta.ConnectionMeta, int)>();
                List<ShortGuid>[] incoming = drawn.Select(o => new List<ShortGuid>()).ToArray();
                for (int i = 0; i < drawn.Count; i++)
                {
                    Entity owner = composite.GetEntityByID(drawn[i].EntityGUID);
                    foreach (FlowgraphMeta.NodeMeta.ConnectionMeta connection in drawn[i].ConnectionsOut)
                    {
                        if (!ids.TryGetValue((connection.ConnectedNodeID, connection.ConnectedEntityGUID), out int to)) continue;
                        if (!owner.childLinks.Any(o => o.thisParamID == connection.ParameterGUID && o.linkedEntityID == connection.ConnectedEntityGUID && o.linkedParamID == connection.ConnectedParameterGUID)) continue;
                        connections.Add((i, connection, to));
                        incoming[to].Add(connection.ConnectedParameterGUID);
                    }
                }

                JObject json = new JObject()
                {
                    ["name"] = page.Name,
                    ["canvas"] = new JObject() { ["x"] = Math.Round(page.CanvasPosition.X, 1), ["y"] = Math.Round(page.CanvasPosition.Y, 1), ["scale"] = Math.Round(page.CanvasScale <= 0 ? 1 : page.CanvasScale, 3) },
                    ["node_count"] = drawn.Count,
                    ["connection_count"] = connections.Count,
                };
                if (page.Name == visible) json["on_screen"] = true;
                if (page.Name == remembered) json["remembered"] = true;
                if (page.Nodes.Count != drawn.Count)
                    json["missing_nodes"] = page.Nodes.Count - drawn.Count;

                JArray nodes = new JArray();
                JArray links = new JArray();
                int first = -1;
                for (int i = 0; i < drawn.Count; i++, seen++)
                {
                    if (seen < offset || listed >= limit) continue;
                    if (first < 0) first = i;
                    listed++;
                    FlowgraphMeta.NodeMeta node = drawn[i];
                    Entity entity = composite.GetEntityByID(node.EntityGUID);
                    List<(int from, FlowgraphMeta.NodeMeta.ConnectionMeta connection, int to)> outgoing = connections.Where(o => o.from == i).ToList();

                    JObject line = new JObject()
                    {
                        ["node"] = i,
                        ["entity"] = McpScript.Id(node.EntityGUID),
                        ["name"] = McpScript.EntityName(commands, composite, entity),
                        ["type"] = McpScript.TypeName(commands, composite, entity),
                        ["x"] = node.Position.X,
                        ["y"] = node.Position.Y,
                    };
                    List<(ShortGuid pin, ST.Library.UI.NodeEditor.PinLocation side)> shown = McpPageTools.ShownPins(entity, node, outgoing.Select(o => o.connection.ParameterGUID), incoming[i], catalogue);
                    JObject pins = new JObject();
                    foreach (IGrouping<ST.Library.UI.NodeEditor.PinLocation, (ShortGuid pin, ST.Library.UI.NodeEditor.PinLocation side)> side in shown.GroupBy(o => o.side).OrderBy(o => (int)o.Key))
                        pins[McpPageTools.Side(side.Key)] = new JArray(side.Select(o => McpScript.ParamName(o.pin)));
                    if (pins.Count != 0) line["pins"] = pins;

                    //A delay on a method or relay pin is a number parameter of the pin's name
                    JObject delays = new JObject();
                    foreach ((ShortGuid pin, ST.Library.UI.NodeEditor.PinLocation side) in shown)
                    {
                        if (side != ST.Library.UI.NodeEditor.PinLocation.Left && side != ST.Library.UI.NodeEditor.PinLocation.Right) continue;
                        ParameterData value = entity.GetParameter(pin)?.content;
                        double delay = value is cFloat f ? f.value : value is cInteger n ? n.value : 0;
                        if (delay != 0) delays[McpScript.ParamName(pin)] = Math.Round(delay, 4);
                    }
                    if (delays.Count != 0) line["delays"] = delays;

                    if (outgoing.Count != 0)
                        line["out"] = new JArray(outgoing.Select(o => new JObject()
                        {
                            ["pin"] = McpScript.ParamName(o.connection.ParameterGUID),
                            ["to_node"] = o.to,
                            ["to_pin"] = McpScript.ParamName(o.connection.ConnectedParameterGUID),
                        }));
                    if ((entity is ProxyEntity || entity is AliasEntity) && commands.Utils.GetResolvedTarget(commands.Utils.ResolveAliasOrProxy(entity, composite)).Item2 == null)
                        line["dead"] = true;
                    if (nodeCounts.TryGetValue(node.EntityGUID, out int many) && many > 1)
                        line["multi_node"] = true;
                    nodes.Add(line);

                    foreach ((int from, FlowgraphMeta.NodeMeta.ConnectionMeta connection, int to) in outgoing)
                        links.Add(McpPageTools.LinkText(commands, composite, node.EntityGUID, connection.ParameterGUID, connection.ConnectedEntityGUID, connection.ConnectedParameterGUID));
                }
                if (first < 0)
                    json["nodes_listed"] = "none (see offset)";
                else if (first != 0 || nodes.Count != drawn.Count)
                    json["nodes_listed"] = first + " to " + (first + nodes.Count - 1);
                json["nodes"] = nodes;
                json["connections"] = links;
                pages.Add(json);
            }

            JObject result = new JObject()
            {
                ["composite"] = composite.name,
                ["script_view"] = FlowgraphLayoutManager.HasCompatibilityInfo(composite) ? (FlowgraphLayoutManager.IsCompatible(composite) ? "pages" : "links (pages do not match)") : "not yet checked",
                ["pages"] = pages,
            };
            McpPaging.Describe(result, total, offset, listed);
            if (offset + listed < total)
                result["nodes_note"] = "Listed " + listed + " of " + total + " nodes: call again with offset " + (offset + listed) + " for more.";
            if (all.Count == 0 && composite.GetEntities().Any(o => o.childLinks.Count != 0))
                result["note"] = composite.name + " has no script pages yet, so the editor shows it as a link list: layout_flowgraph gives it pages that draw every link (get_composite links:true lists them meanwhile).";
            if (!call.Has("page"))
            {
                //What create_entities made without links, say: in the composite but on no page
                List<Entity> unplaced = McpPageTools.Unplaced(composite, all);
                if (unplaced.Count != 0 && all.Count != 0)
                    result["entities_without_nodes"] = new JObject()
                    {
                        ["count"] = unplaced.Count,
                        ["entities"] = McpPageTools.Capped(unplaced.Select(o => McpScript.EntityName(commands, composite, o) + " (" + McpScript.Id(o.shortGUID) + ")"), 40),
                        ["hint"] = "edit_flowgraph_nodes (action 'add') gives an entity a node.",
                    };
            }
            return result;
        }

        private static object Layout(McpCall call)
        {
            Commands commands = McpEditor.RequireCommands();
            McpEditor.RequireUndoIdle();
            Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
            if (McpScript.IsFolder(composite))
                throw new McpError(McpErrorCodes.Refused, composite.name + " is a folder placeholder, not a composite: it has no script to lay out.");
            string mode = (call.Str("mode") ?? "complete").Trim();
            if (!string.Equals(mode, "complete", StringComparison.OrdinalIgnoreCase) && !string.Equals(mode, "rebuild", StringComparison.OrdinalIgnoreCase))
                throw McpError.Invalid("'mode' must be 'complete' or 'rebuild'.");
            bool rebuild = string.Equals(mode, "rebuild", StringComparison.OrdinalIgnoreCase);
            string pageName = call.Str("page") ?? McpScript.CompositeLeaf(composite);

            CompositeDisplay display = Singleton.Editor.CompositeDisplay;
            bool onScreen = display != null && !display.IsDisposed && display.Populated && display.Composite == composite;
            if (onScreen)
                display.SaveAllFlowgraphs();

            //What the first open does, so the pages are drawn against the links that will stay
            if (!commands.Utils.PurgedComposites.purged.Contains(composite.shortGUID))
            {
                commands.Utils.PurgeDeadLinks(composite);
                commands.Utils.PurgedComposites.purged.Add(composite.shortGUID);
            }

            List<string> undrawable = Undrawable(commands, composite);
            if (undrawable.Count != 0)
                throw new McpError("These links cannot be drawn on flowgraph pages, so " + composite.name + " has to stay a link list:\n- " + string.Join("\n- ", undrawable.Take(30)) + (undrawable.Count > 30 ? "\n- ... and " + (undrawable.Count - 30) + " more" : ""));

            List<FlowgraphMeta> existing = rebuild ? new List<FlowgraphMeta>() : FlowgraphLayoutManager.GetLayouts(composite).Select(o => o.Copy()).ToList();
            List<FlowgraphMeta> pages = RefactorPages.DrawLinks(composite, existing, pageName, commands);
            if (pages.Count == 0)
                pages.Add(new FlowgraphMeta() { CompositeGUID = composite.shortGUID, Name = pageName, CanvasScale = 1f });
            if (!RefactorPages.PagesMatchLinks(composite, pages))
                throw new McpError("The pages could not be made to match " + composite.name + "'s links.");

            PageLayoutEdit edit = new PageLayoutEdit(composite, pages, "AI: Lay out " + McpScript.CompositeLeaf(composite));
            UndoStack.Current.Apply(edit);
            if (!onScreen)
                McpScriptEdit.Show(composite, null);
            return new JObject()
            {
                ["composite"] = composite.name,
                ["pages"] = new JArray(pages.Select(o => new JObject() { ["name"] = o.Name, ["nodes"] = o.Nodes.Count, ["connections"] = o.Nodes.Sum(n => n.ConnectionsOut.Count) })),
                ["script_view"] = FlowgraphLayoutManager.IsCompatible(composite) ? "pages" : "links",
            };
        }

        /// <summary>Links whose pins the flowgraph editor could not join (see McpPins).</summary>
        private static List<string> Undrawable(Commands commands, Composite composite) => new McpPins(commands, composite).UndrawableLinks();

        /// <summary>A composite's pages (and flowgraph verdict) replaced, as one undo step.</summary>
        internal sealed class PageLayoutEdit : IEdit
        {
            private readonly Composite _composite;
            private readonly List<FlowgraphMeta> _pages;
            private FlowgraphLayoutManager.CompositeLayoutState _before;
            private FlowgraphLayoutManager.CompositeLayoutState _after;

            public string Label { get; }
            public ShortGuid CompositeId => _composite.shortGUID;
            public ShortGuid EntityId => ShortGuid.Invalid;

            public PageLayoutEdit(Composite composite, List<FlowgraphMeta> pages, string label)
            {
                _composite = composite;
                _pages = pages;
                Label = label;
            }

            public void Apply(UndoContext context)
            {
                if (_before == null)
                {
                    _before = Copy(FlowgraphLayoutManager.RemoveCompositeState(_composite));
                    FlowgraphLayoutManager.ImportLayouts(_composite, _pages.Select(o => o.Copy()).ToList());
                    FlowgraphLayoutManager.EvaluateCompatibility(_composite);
                    FlowgraphLayoutManager.CompositeLayoutState now = FlowgraphLayoutManager.RemoveCompositeState(_composite);
                    FlowgraphLayoutManager.RestoreCompositeState(now);
                    _after = Copy(now);
                }
                else
                {
                    CompileLivePages();
                    FlowgraphLayoutManager.RemoveCompositeState(_composite);
                    FlowgraphLayoutManager.RestoreCompositeState(Copy(_after));
                }
                Refresh();
            }

            public void Revert(UndoContext context)
            {
                CompileLivePages();
                FlowgraphLayoutManager.RemoveCompositeState(_composite);
                FlowgraphLayoutManager.RestoreCompositeState(Copy(_before));
                Refresh();
            }

            public bool TryMerge(IEdit next) => false;

            /// <summary>
            /// Whatever the live pages hold goes into the links first, as <see cref="RefactorEdit"/> does: the
            /// reload below closes them without compiling, and page-only undo records (a node or connection put
            /// back) change nothing but the live page. The first apply needs none: its callers compiled just
            /// before building the pages (and a compile here would move the remembered page off the one they chose).
            /// </summary>
            private void CompileLivePages()
            {
                CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
                if (display != null && !display.IsDisposed && display.Populated && display.Composite == _composite)
                    display.SaveAllFlowgraphs();
            }

            private void Refresh()
            {
                DirtyTracker.MarkLevelDataModified();
                CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
                if (display != null && !display.IsDisposed && display.Populated && display.Composite == _composite)
                {
                    display.ClearEntitySelection();
                    display.Reload(true);
                }
            }

            private static FlowgraphLayoutManager.CompositeLayoutState Copy(FlowgraphLayoutManager.CompositeLayoutState state)
            {
                return new FlowgraphLayoutManager.CompositeLayoutState()
                {
                    Layouts = state.Layouts.Select(o => o.Copy()).ToList(),
                    Compatibility = state.Compatibility == null ? null : new CompositeFlowgraphCompatibilityTable.CompatibilityInfo()
                    {
                        composite_id = state.Compatibility.composite_id,
                        flowgraphs_supported = state.Compatibility.flowgraphs_supported,
                    },
                };
            }
        }
    }
}
