using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE.Undo
{
    /// <summary>The pins a node shows, and where the node sits (pin changes re-centre it).</summary>
    public sealed class PinSet
    {
        public List<PinSnapshot> Pins = new List<PinSnapshot>();
        public Point Location;

        public static bool Same(PinSet a, PinSet b)
        {
            if (a == null || b == null)
                return ReferenceEquals(a, b);
            if (a.Location != b.Location || a.Pins.Count != b.Pins.Count)
                return false;
            List<string> keysA = a.Pins.Select(Key).OrderBy(o => o).ToList();
            List<string> keysB = b.Pins.Select(Key).OrderBy(o => o).ToList();
            return keysA.SequenceEqual(keysB);
        }

        private static string Key(PinSnapshot pin)
        {
            return pin.Parameter.ToByteString() + ":" + pin.Location + ":" + pin.Style;
        }
    }

    /// <summary>A node's pin set changed: add all, remove unused, the manage-pins dialog, a pin made for a new connection.</summary>
    public sealed class PinSetEdit : IEdit
    {
        private readonly ShortGuid _composite;
        private readonly string _page;
        private readonly NodeRef _node;
        private readonly PinSet _before;
        private readonly PinSet _after;

        public string Label { get; }
        public ShortGuid CompositeId => _composite;
        public ShortGuid EntityId => ShortGuid.Invalid;

        public PinSetEdit(Composite composite, string page, NodeRef node, PinSet before, PinSet after, string label)
        {
            _composite = composite.shortGUID;
            _page = page;
            _node = node;
            _before = before;
            _after = after;
            Label = label;
        }

        public void Apply(UndoContext context) => Set(context, _before, _after);
        public void Revert(UndoContext context) => Set(context, _after, _before);

        private void Set(UndoContext context, PinSet from, PinSet to)
        {
            Flowgraph page = FlowgraphEdits.RequirePage(context, _composite, _page);
            STNode node = page.ResolveNode(_node, from.Location);
            if (node == null)
                throw new InvalidOperationException("The node whose pins changed is no longer on the page");
            page.SetNodePins(node, to);
            _node.Node = node;
            _node.Location = to.Location;
        }

        public bool TryMerge(IEdit next) => false;
    }

    /// <summary>
    /// A flowgraph page created or deleted. Deleting keeps the page as it last was, and undo brings
    /// it back along with any links it carried that the composite had not been given yet.
    /// </summary>
    public sealed class PagePresenceEdit : IEdit
    {
        private readonly ShortGuid _composite;
        private FlowgraphMeta _meta;
        private readonly bool _presentAfter;

        public string Label { get; }
        public ShortGuid CompositeId => _composite;
        public ShortGuid EntityId => ShortGuid.Invalid;

        public PagePresenceEdit(Composite composite, FlowgraphMeta meta, bool presentAfter, string label)
        {
            _composite = composite.shortGUID;
            _meta = meta;
            _presentAfter = presentAfter;
            Label = label;
        }

        public void Apply(UndoContext context) => Set(context, _presentAfter);
        public void Revert(UndoContext context) => Set(context, !_presentAfter);

        private void Set(UndoContext context, bool present)
        {
            Composite composite = context.RequireComposite(_composite);
            if (present)
            {
                if (context.Ui?.Page(composite, _meta.Name) != null)
                    return;
                EnsureLinksForLayout(composite, _meta);
                FlowgraphLayoutManager.AddLayout(_meta);
                context.Ui?.OpenPage(composite, _meta);
            }
            else
            {
                Flowgraph page = context.Ui?.Page(composite, _meta.Name);
                if (page != null)
                {
                    _meta = page.CaptureLayout();
                    page.Close();
                }
                FlowgraphLayoutManager.RemoveLayout(composite, _meta.Name);
            }
            DirtyTracker.MarkLevelDataModified();
        }

        /* A page's connections only reach the composite's links when it is compiled, on leaving the
           composite. A page brought back after that compile would show connections the data no longer
           has, which the loader treats as a broken layout - so the links are put back first. The next
           compile regenerates them from the pages anyway. */
        private static void EnsureLinksForLayout(Composite composite, FlowgraphMeta meta)
        {
            if (meta?.Nodes == null)
                return;
            foreach (FlowgraphMeta.NodeMeta node in meta.Nodes)
            {
                Entity entity = composite.GetEntityByID(node.EntityGUID);
                if (entity == null)
                    continue;
                foreach (FlowgraphMeta.NodeMeta.ConnectionMeta connection in node.ConnectionsOut)
                {
                    if (composite.GetEntityByID(connection.ConnectedEntityGUID) == null)
                        continue;
                    bool exists = entity.childLinks.Any(o => o.thisParamID == connection.ParameterGUID
                        && o.linkedEntityID == connection.ConnectedEntityGUID
                        && o.linkedParamID == connection.ConnectedParameterGUID);
                    if (!exists)
                        entity.AddParameterLink(connection.ParameterGUID, connection.ConnectedEntityGUID, connection.ConnectedParameterGUID);
                }
            }
        }

        public bool TryMerge(IEdit next) => false;
    }

    /// <summary>A flowgraph page renamed.</summary>
    public sealed class PageRenameEdit : IEdit
    {
        private readonly ShortGuid _composite;
        private readonly string _before;
        private readonly string _after;

        public string Label { get; }
        public ShortGuid CompositeId => _composite;
        public ShortGuid EntityId => ShortGuid.Invalid;

        public PageRenameEdit(Composite composite, string before, string after)
        {
            _composite = composite.shortGUID;
            _before = before;
            _after = after;
            Label = "Rename page " + before;
        }

        public void Apply(UndoContext context) => Rename(context, _before, _after);
        public void Revert(UndoContext context) => Rename(context, _after, _before);

        private void Rename(UndoContext context, string from, string to)
        {
            Composite composite = context.RequireComposite(_composite);
            Flowgraph page = context.Ui?.Page(composite, from);
            if (page != null)
            {
                page.RenameForUndo(to);
            }
            else
            {
                FlowgraphMeta layout = FlowgraphLayoutManager.GetLayouts(composite).FirstOrDefault(o => o.Name == from);
                if (layout != null)
                    layout.Name = to;
            }
            DirtyTracker.MarkLevelDataModified();
        }

        public bool TryMerge(IEdit next) => false;
    }
}
