using CATHODE.Scripting;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OpenCAGE.Undo
{
    /// <summary>
    /// A node on a flowgraph page, named in a way that survives the page being rebuilt. The live
    /// STNode is used while it is on the page; after a rebuild the node is found again by its entity,
    /// preferring the one at the remembered position.
    /// </summary>
    public sealed class NodeRef
    {
        public STNode Node;
        public ShortGuid Entity;
        public Point? Location;

        public NodeRef(STNode node)
        {
            Node = node;
            Entity = node.ShortGUID;
            Location = node.Location;
        }

        /// <summary>A node that is not on the page yet: its entity, and where it is to go.</summary>
        public NodeRef(ShortGuid entity, Point location)
        {
            Entity = entity;
            Location = location;
        }
    }

    public sealed class PinSnapshot
    {
        public ShortGuid Parameter;
        public PinLocation Location;
        public PinStyle Style;
    }

    /// <summary>One connection on a node. Outgoing: this node's pin feeds the peer's; otherwise the peer feeds this one.</summary>
    public sealed class ConnectionSnapshot
    {
        public NodeRef Peer;
        public ShortGuid ThisPin;
        public PinLocation ThisSide;
        public ShortGuid PeerPin;
        public PinLocation PeerSide;
        public bool Outgoing;
    }

    /// <summary>Everything needed to put a node back on its page: position, pins, and both directions of connection.</summary>
    public sealed class NodeSnapshot
    {
        public string Page;
        public NodeRef Self;
        public int NodeID;
        public Point Location;
        public List<PinSnapshot> Pins = new List<PinSnapshot>();
        public List<ConnectionSnapshot> Connections = new List<ConnectionSnapshot>();
    }

    internal static class FlowgraphEdits
    {
        public static Flowgraph RequirePage(UndoContext context, ShortGuid composite, string page)
        {
            Flowgraph flowgraph = context.Ui?.Page(context.RequireComposite(composite), page);
            if (flowgraph == null)
                throw new InvalidOperationException("The flowgraph page '" + page + "' is not open");
            return flowgraph;
        }
    }

    /// <summary>Nodes dragged (or arranged) to new positions.</summary>
    public sealed class NodeMoveEdit : IEdit, IChangesComposites
    {
        public sealed class Movement
        {
            public NodeRef Node;
            public Point From;
            public Point To;
        }

        private readonly ShortGuid _composite;
        private readonly string _page;
        private readonly List<Movement> _moves;

        public string Label { get; }
        public ShortGuid CompositeId => _composite;
        //Where nodes sit on a page, not what the scripts say
        public IEnumerable<ShortGuid> ChangedComposites => new ShortGuid[0];
        public ShortGuid EntityId => ShortGuid.Invalid;

        public NodeMoveEdit(Composite composite, string page, List<Movement> moves, string label = null)
        {
            _composite = composite.shortGUID;
            _page = page;
            _moves = moves;
            Label = label ?? "Move " + UndoLabels.Count(moves.Count, "node", "nodes");
        }

        public void Apply(UndoContext context) => Move(context, true);
        public void Revert(UndoContext context) => Move(context, false);

        private void Move(UndoContext context, bool forward)
        {
            Flowgraph page = FlowgraphEdits.RequirePage(context, _composite, _page);
            foreach (Movement move in _moves)
            {
                STNode node = page.ResolveNode(move.Node, forward ? move.From : move.To);
                if (node == null)
                    continue;
                node.SetPosition(forward ? move.To : move.From);
            }
            page.Nodegraph.Invalidate();
            DirtyTracker.MarkLevelDataModified();
        }

        public bool TryMerge(IEdit next) => false;
    }

    /// <summary>
    /// A node added to or removed from a page. Removal refreshes the snapshot first, so a redo restores the node as it last was.
    /// After a page rebuild the node is a new object: removal takes only a node it can tell is this one (see
    /// Flowgraph.ResolveNodeToRemove), and otherwise leaves the page alone - the entity's other nodes carry their own links.
    /// </summary>
    public sealed class NodePresenceEdit : IEdit
    {
        private readonly ShortGuid _composite;
        private readonly string _page;
        private NodeSnapshot _snapshot;
        private readonly bool _presentAfter;
        //The last removal found no node it could be sure of and took nothing, so there is nothing to put back
        private bool _leftInPlace = false;

        public string Label { get; }
        public ShortGuid CompositeId => _composite;
        public ShortGuid EntityId => ShortGuid.Invalid;

        public NodePresenceEdit(Composite composite, string page, NodeSnapshot snapshot, bool presentAfter, string label)
        {
            _composite = composite.shortGUID;
            _page = page;
            _snapshot = snapshot;
            _presentAfter = presentAfter;
            Label = label;
        }

        public void Apply(UndoContext context) => Set(context, _presentAfter);
        public void Revert(UndoContext context) => Set(context, !_presentAfter);

        private void Set(UndoContext context, bool present)
        {
            Flowgraph page = FlowgraphEdits.RequirePage(context, _composite, _page);
            if (present)
            {
                if (_leftInPlace)
                {
                    _leftInPlace = false;
                    return; //never taken away
                }
                STNode node = page.ResolveNode(_snapshot.Self, _snapshot.Location);
                if (node != null && ReferenceEquals(node, _snapshot.Self.Node))
                    return; //still there
                page.RestoreNode(_snapshot);
            }
            else
            {
                STNode node = page.ResolveNodeToRemove(_snapshot, out bool unsure);
                if (node != null)
                {
                    _snapshot = page.SnapshotNode(node);
                    page.RemoveNodeForUndo(node);
                }
                _leftInPlace = unsure;
            }
            context.Ui?.RefreshNodeMarkers();
        }

        public bool TryMerge(IEdit next) => false;
    }

    /// <summary>
    /// A connection made or broken on a page. While a page is open its connections are the truth -
    /// the composite's links are compiled from them when the composite is left - so the edit works on
    /// the page, and undo opens the composite first to make sure there is one.
    /// </summary>
    public sealed class LinkEdit : IEdit
    {
        private readonly ShortGuid _composite;
        private readonly string _page;
        private readonly NodeRef _source;
        private readonly ShortGuid _sourcePin;
        private readonly PinLocation _sourceSide;
        private readonly NodeRef _target;
        private readonly ShortGuid _targetPin;
        private readonly PinLocation _targetSide;
        private readonly bool _connectedAfter;

        public string Label { get; }
        public ShortGuid CompositeId => _composite;
        public ShortGuid EntityId => ShortGuid.Invalid;

        public LinkEdit(Composite composite, string page, STNodeOption source, STNodeOption target, bool connectedAfter)
        {
            _composite = composite.shortGUID;
            _page = page;
            _source = new NodeRef(source.Owner);
            _sourcePin = source.ShortGUID;
            _sourceSide = source.Location;
            _target = new NodeRef(target.Owner);
            _targetPin = target.ShortGUID;
            _targetSide = target.Location;
            _connectedAfter = connectedAfter;

            string from = UndoLabels.Entity(composite, source.Owner.Entity);
            string to = UndoLabels.Entity(composite, target.Owner.Entity);
            Label = (connectedAfter ? "Connect " : "Disconnect ") + from + " " + (connectedAfter ? "to " : "from ") + to;
        }

        public void Apply(UndoContext context) => Set(context, _connectedAfter);
        public void Revert(UndoContext context) => Set(context, !_connectedAfter);

        private void Set(UndoContext context, bool connected)
        {
            Flowgraph page = FlowgraphEdits.RequirePage(context, _composite, _page);
            STNode source = page.ResolveNode(_source);
            STNode target = page.ResolveNode(_target);
            if (source == null || target == null)
                throw new InvalidOperationException("The nodes this link joined are no longer on the page");
            page.SetConnection(source, _sourcePin, _sourceSide, target, _targetPin, _targetSide, connected);
        }

        public bool TryMerge(IEdit next) => false;
    }
}
