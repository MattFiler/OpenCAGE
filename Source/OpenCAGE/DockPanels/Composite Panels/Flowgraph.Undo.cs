using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.Undo;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE
{
    /// <summary>
    /// The page as the undo stack sees it. Recording happens where the graph changes - a node
    /// added, nodes moved, a pin connected or disconnected - and the replay side rebuilds nodes and
    /// connections from snapshots without being recorded again.
    /// </summary>
    public partial class Flowgraph
    {
        /// <summary>Graph changes made inside the scope are neither recorded nor counted as user edits.</summary>
        internal IDisposable SuppressRecording()
        {
            _populatingDepth++;
            return new RecordingScope(this);
        }

        private sealed class RecordingScope : IDisposable
        {
            private Flowgraph _page;
            public RecordingScope(Flowgraph page) { _page = page; }
            public void Dispose()
            {
                if (_page == null)
                    return;
                _page._populatingDepth--;
                _page = null;
            }
        }

        private bool CanRecord => !IsPopulating && _composite != null && !UndoStack.Current.IsSuspended;

        private void RecordNodesMoved(STNodesMovedEventArgs e)
        {
            if (!CanRecord || e?.Movements == null)
                return;

            List<NodeMoveEdit.Movement> moves = new List<NodeMoveEdit.Movement>();
            foreach (NodeMovement movement in e.Movements)
            {
                if (movement.Node == null || movement.OldLocation == movement.NewLocation)
                    continue;
                moves.Add(new NodeMoveEdit.Movement()
                {
                    Node = new NodeRef(movement.Node),
                    From = movement.OldLocation,
                    To = movement.NewLocation,
                });
            }
            //The reroute points that went along with both their line's nodes
            List<NodeMoveEdit.RouteMovement> routes = new List<NodeMoveEdit.RouteMovement>();
            foreach (RouteMovement route in e.RouteMovements ?? new RouteMovement[0])
            {
                if (route.From?.Owner?.Entity == null || route.To?.Owner?.Entity == null)
                    continue;
                routes.Add(new NodeMoveEdit.RouteMovement() { Line = new LineRef(route.From, route.To), From = route.Before, To = route.After });
            }
            if (moves.Count > 0)
                UndoStack.Current.Record(new NodeMoveEdit(_composite, _flowgraphName, moves, null, routes));
        }

        /* Reroute points the user added to a line, moved or took off it (STNodeEditor.Reroutes.cs) */
        private void RecordRerouteChanged(STNodeEditorRerouteEventArgs e)
        {
            if (!CanRecord || e?.From?.Owner?.Entity == null || e.To?.Owner?.Entity == null)
                return;

            string label;
            switch (e.Change)
            {
                case RerouteChange.Added: label = "Add reroute point"; break;
                case RerouteChange.Moved: label = "Move reroute point"; break;
                case RerouteChange.Removed: label = "Remove reroute point"; break;
                default: label = "Remove reroute points"; break;
            }
            label += " on the link from " + UndoLabels.Entity(_composite, e.From.Owner.Entity) + " to " + UndoLabels.Entity(_composite, e.To.Owner.Entity);
            UndoStack.Current.Record(new RouteEdit(_composite, _flowgraphName, e.From, e.To, e.Before, e.After, label));
        }

        /* Connections raise the same event from whichever end started it; the edit always names the
           feeding pin (right or top) as the source */
        private void RecordConnectionChanged(STNodeEditorOptionEventArgs e)
        {
            if (!CanRecord || e == null)
                return;

            bool connected;
            switch (e.Status)
            {
                case ConnectionStatus.Connected:
                    connected = true;
                    break;
                case ConnectionStatus.Disconnected:
                    connected = false;
                    break;
                default:
                    return;
            }

            STNodeOption a = e.CurrentOption;
            STNodeOption b = e.TargetOption;
            if (a?.Owner?.Entity == null || b?.Owner?.Entity == null)
                return;

            bool aFeeds = a.Location == PinLocation.Right || a.Location == PinLocation.Top;
            STNodeOption source = aFeeds ? a : b;
            STNodeOption target = aFeeds ? b : a;
            //A line broken takes its reroute points with it: putting it back puts them back
            UndoStack.Current.Record(new LinkEdit(_composite, _flowgraphName, source, target, connected, connected ? null : e.Route));
        }

        private void RecordNodeAdded(STNode node)
        {
            if (!CanRecord || node?.Entity == null)
                return;
            UndoStack.Current.Record(new NodePresenceEdit(_composite, _flowgraphName, SnapshotNode(node), true,
                "Add node for " + UndoLabels.Entity(_composite, node.Entity)));
        }

        /// <summary>
        /// Take every node the entity has on this page, as one undo step. Just the nodes: unlike the
        /// page's own Delete this never offers to delete the entity with them.
        /// </summary>
        internal int RemoveNodesForEntity(Entity entity)
        {
            if (entity == null)
                return 0;

            List<STNode> nodes = new List<STNode>();
            foreach (STNode node in stNodeEditor1.Nodes.ToArray())
            {
                if (node?.Entity != null && node.ShortGUID == entity.shortGUID)
                    nodes.Add(node);
            }
            if (nodes.Count == 0)
                return 0;

            using (UndoStack.Current.BeginGroup("Remove " + UndoLabels.Count(nodes.Count, "node", "nodes") + " for " + UndoLabels.Entity(_composite, entity) + " from " + _flowgraphName))
                RemoveNodesRecorded(nodes);
            RefreshNodeMarkers();
            return nodes.Count;
        }

        /// <summary>Remove nodes as the user asked: each is snapshotted first, connections and all.</summary>
        private void RemoveNodesRecorded(List<STNode> nodes)
        {
            foreach (STNode node in nodes)
            {
                if (node == null || stNodeEditor1.Nodes.IndexOf(node) < 0)
                    continue;

                NodeSnapshot snapshot = SnapshotNode(node);
                using (SuppressRecording())
                    stNodeEditor1.Nodes.Remove(node);

                if (node.Entity != null)
                    UndoStack.Current.Record(new NodePresenceEdit(_composite, _flowgraphName, snapshot, false,
                        "Remove node for " + UndoLabels.Entity(_composite, node.Entity)));
            }
            DirtyTracker.MarkLevelDataModified();
        }

        private void RecordPinDelay(Entity entity, Parameter previous, ParameterData before, bool wasModified, Parameter current)
        {
            if (_composite == null || entity == null || current == null || UndoStack.Current.IsSuspended)
                return;

            string label = "Set delay on " + current.name + " of " + UndoLabels.Entity(_composite, entity);
            if (previous == null)
                UndoStack.Current.Record(new ParameterPresenceEdit(_composite, entity, current, entity.parameters.IndexOf(current), true, false, label));
            else
                UndoStack.Current.Record(new ParameterValueEdit(_composite, entity, current.name, before, ParameterValues.Clone(current.content), wasModified, wasModified, label));
        }

        /// <summary>
        /// The node a reference names, as it is on this page now: the same object while it is here,
        /// otherwise the entity's node at the expected position, then the one where the old object was
        /// when it left the page (a rebuild puts its replacement there), otherwise its most recent node.
        /// </summary>
        internal STNode ResolveNode(NodeRef reference, Point? expectedLocation = null)
        {
            if (reference == null)
                return null;
            if (reference.Node != null && stNodeEditor1.Nodes.IndexOf(reference.Node) >= 0)
                return reference.Node;

            Point? wanted = expectedLocation ?? reference.Location;
            Point? lastSeen = reference.Node?.Location;
            STNode seen = null;
            STNode last = null;
            foreach (STNode node in stNodeEditor1.Nodes)
            {
                if (node?.Entity == null || node.ShortGUID != reference.Entity)
                    continue;
                if (wanted.HasValue && node.Location == wanted.Value)
                    return node;
                if (seen == null && lastSeen.HasValue && node.Location == lastSeen.Value)
                    seen = node;
                last = node;
            }
            return seen ?? last;
        }

        /// <summary>
        /// The node an undo or redo should take off the page for this snapshot: its own node while that is
        /// here; after a rebuild, the entity's node where the snapshot says it sits (or where the old node
        /// was when it left the page); failing those, the one node of the entity whose connections are
        /// exactly the snapshot's. Never a node carrying a connection the snapshot doesn't list - that
        /// belongs to another node of the same entity, and would leave the script with it. Null when no
        /// node can be told apart for certain; <paramref name="unsure"/> says the entity does have nodes here.
        /// </summary>
        internal STNode ResolveNodeToRemove(NodeSnapshot snapshot, out bool unsure)
        {
            unsure = false;
            NodeRef self = snapshot?.Self;
            if (self == null)
                return null;
            if (self.Node != null && stNodeEditor1.Nodes.IndexOf(self.Node) >= 0)
                return self.Node;

            List<STNode> candidates = new List<STNode>();
            foreach (STNode node in stNodeEditor1.Nodes)
            {
                if (node?.Entity != null && node.ShortGUID == self.Entity)
                    candidates.Add(node);
            }
            if (candidates.Count == 0)
                return null;

            HashSet<(uint, uint, uint)> listed = new HashSet<(uint, uint, uint)>(snapshot.Connections.Select(o => ConnectionKey(o.ThisPin, o.Peer.Entity, o.PeerPin)));
            foreach (Point? at in new Point?[] { snapshot.Location, self.Location, self.Node?.Location })
            {
                if (!at.HasValue)
                    continue;
                List<STNode> here = candidates.Where(o => o.Location == at.Value && listed.IsSupersetOf(ConnectionKeys(o))).ToList();
                if (here.Count != 0)
                    return here.FirstOrDefault(o => listed.SetEquals(ConnectionKeys(o))) ?? here[0];
            }

            List<STNode> alike = candidates.Where(o => listed.SetEquals(ConnectionKeys(o))).ToList();
            if (alike.Count == 1)
                return alike[0];

            unsure = true;
            Debug.Log("Undo", "WARNING: Left the " + candidates.Count + " node(s) of " + candidates[0].Title + " on '" + _flowgraphName + "' as they are: none is certainly the node this step adds or removes");
            return null;
        }

        //A node's connections by what they join - its pin, the other end's entity and pin - which a page rebuild keeps, unlike the node objects
        private static HashSet<(uint, uint, uint)> ConnectionKeys(STNode node)
        {
            HashSet<(uint, uint, uint)> keys = new HashSet<(uint, uint, uint)>();
            foreach (STNodeOption option in node.GetAllOptions())
            {
                if (option == null || option == STNodeOption.Empty)
                    continue;
                List<STNodeOption> connected = option.GetConnectedOption();
                if (connected == null)
                    continue;
                foreach (STNodeOption peer in connected)
                {
                    if (peer?.Owner != null)
                        keys.Add(ConnectionKey(option.ShortGUID, peer.Owner.ShortGUID, peer.ShortGUID));
                }
            }
            return keys;
        }

        private static (uint, uint, uint) ConnectionKey(ShortGuid pin, ShortGuid peerEntity, ShortGuid peerPin)
        {
            return (pin.AsUInt32, peerEntity.AsUInt32, peerPin.AsUInt32);
        }

        internal NodeSnapshot SnapshotNode(STNode node)
        {
            NodeSnapshot snapshot = new NodeSnapshot()
            {
                Page = _flowgraphName,
                Self = new NodeRef(node),
                NodeID = node.NodeID,
                Location = node.Location,
            };

            void Capture(STNodeOption[] options, bool outgoing)
            {
                foreach (STNodeOption option in options)
                {
                    if (option == null || option == STNodeOption.Empty)
                        continue;

                    snapshot.Pins.Add(new PinSnapshot() { Parameter = option.ShortGUID, Location = option.Location, Style = option.Style });

                    List<STNodeOption> connected = option.GetConnectedOption();
                    if (connected == null)
                        continue;
                    foreach (STNodeOption peer in connected)
                    {
                        if (peer?.Owner == null)
                            continue;
                        PointF[] route = stNodeEditor1.GetReroutePoints(option, peer);
                        snapshot.Connections.Add(new ConnectionSnapshot()
                        {
                            Peer = new NodeRef(peer.Owner),
                            ThisPin = option.ShortGUID,
                            ThisSide = option.Location,
                            PeerPin = peer.ShortGUID,
                            PeerSide = peer.Location,
                            Outgoing = outgoing,
                            Route = route.Length == 0 ? null : route,
                        });
                    }
                }
            }
            Capture(node.GetInputOptions(), false);
            Capture(node.GetOutputOptions(), true);
            Capture(node.GetTopOptions(), true);
            Capture(node.GetBottomOptions(), false);
            return snapshot;
        }

        internal List<NodeSnapshot> SnapshotNodesFor(Entity entity)
        {
            List<NodeSnapshot> snapshots = new List<NodeSnapshot>();
            if (entity == null)
                return snapshots;
            foreach (STNode node in stNodeEditor1.Nodes.ToArray())
            {
                if (node?.Entity != null && node.ShortGUID == entity.shortGUID)
                    snapshots.Add(SnapshotNode(node));
            }
            return snapshots;
        }

        /// <summary>Put a node back from its snapshot: position, pins, and every connection whose other end is still here.</summary>
        internal STNode RestoreNode(NodeSnapshot snapshot)
        {
            if (snapshot?.Self == null || _composite == null)
                return null;
            Entity entity = _composite.GetEntityByID(snapshot.Self.Entity);
            if (entity == null)
                return null;

            STNode oldSelf = snapshot.Self.Node;
            STNode node;
            using (SuppressRecording())
            {
                node = EntityToNode(entity);
                node.NodeID = snapshot.NodeID;
                node.SetPosition(snapshot.Location);

                foreach (PinSnapshot pin in snapshot.Pins)
                {
                    if (FindOption(node, pin.Parameter, pin.Location) == null)
                        AddOption(node, pin.Parameter, pin.Location, pin.Style);
                }
                node.AlignRelayRows(_composite, _commands);
                UpdatePinTexts(node);
                node.Recompute();

                foreach (ConnectionSnapshot connection in snapshot.Connections)
                {
                    //A node wired to itself names its old self as the peer
                    STNode peer = ReferenceEquals(connection.Peer.Node, oldSelf) ? node : ResolveNode(connection.Peer);
                    if (peer == null)
                        continue;
                    if (connection.Outgoing)
                        Connect(node, connection.ThisPin, connection.ThisSide, peer, connection.PeerPin, connection.PeerSide, connection.Route);
                    else
                        Connect(peer, connection.PeerPin, connection.PeerSide, node, connection.ThisPin, connection.ThisSide, connection.Route);
                }

                node.EnsureProperNodeSizing();
                stNodeEditor1.Invalidate();
            }
            RefreshActivityBadgesForNewNodes(); //Live Link Show Activity

            snapshot.Self = new NodeRef(node);
            DirtyTracker.MarkLevelDataModified();
            return node;
        }

        internal void RemoveNodeForUndo(STNode node)
        {
            if (node == null || stNodeEditor1.Nodes.IndexOf(node) < 0)
                return;
            using (SuppressRecording())
                stNodeEditor1.Nodes.Remove(node);
            stNodeEditor1.Invalidate();
            DirtyTracker.MarkLevelDataModified();
        }

        /// <summary>Make or break one connection on the live page, as an undo or redo asks - one made drawn through <paramref name="route"/>, if given.</summary>
        internal void SetConnection(STNode source, ShortGuid sourcePin, PinLocation sourceSide, STNode target, ShortGuid targetPin, PinLocation targetSide, bool connect, PointF[] route = null)
        {
            using (SuppressRecording())
            {
                if (connect)
                {
                    Connect(source, sourcePin, sourceSide, target, targetPin, targetSide, route);
                }
                else
                {
                    STNodeOption pinOut = FindOption(source, sourcePin, sourceSide) ?? source.GetOption(sourcePin);
                    STNodeOption pinIn = FindOption(target, targetPin, targetSide) ?? target.GetOption(targetPin);
                    List<STNodeOption> connected = pinOut?.GetConnectedOption();
                    if (pinIn != null && connected != null && connected.Contains(pinIn))
                        pinOut.DisconnectOption(pinIn);
                }
                source.EnsureProperNodeSizing();
                target.EnsureProperNodeSizing();
                stNodeEditor1.Invalidate();
            }

            DirtyTracker.MarkLevelDataModified();
            Singleton.Editor?.CompositeDisplay?.EntityDisplay?.RefreshParameterHighlights();
        }

        /// <summary>Redraw the delay text on every pin this page draws for the entity.</summary>
        internal void RefreshPinDelayTexts(Entity entity)
        {
            //Raised globally, so a page that is on its way out can hear it
            if (entity == null || IsDisposed || Disposing)
                return;
            bool any = false;
            foreach (STNode node in stNodeEditor1.Nodes)
            {
                if (node?.Entity != entity)
                    continue;
                UpdatePinDelayTexts(node);
                any = true;
            }
            if (any)
                stNodeEditor1.Invalidate();
        }

        private void Connect(STNode source, ShortGuid sourcePin, PinLocation sourceSide, STNode target, ShortGuid targetPin, PinLocation targetSide, PointF[] route = null)
        {
            source.AddPinsForConnection(target, sourcePin, targetPin, _composite, _commands);
            STNodeOption pinOut = FindOption(source, sourcePin, sourceSide) ?? source.GetOption(sourcePin) ?? AddOption(source, sourcePin, sourceSide, PinStyle.ArrowDown);
            STNodeOption pinIn = FindOption(target, targetPin, targetSide) ?? target.GetOption(targetPin) ?? AddOption(target, targetPin, targetSide, PinStyle.ArrowDown);

            List<STNodeOption> connected = pinOut.GetConnectedOption();
            if (connected == null || !connected.Contains(pinIn))
            {
                ConnectionStatus status = pinOut.ConnectOption(pinIn);
                if (status != ConnectionStatus.Connected)
                    Debug.Log("Flowgraph", "WARNING: Could not restore the connection " + source.Title + " [" + pinOut.Text + "] -> " + target.Title + " [" + pinIn.Text + "]: " + status);
            }
            if (route != null && route.Length != 0)
                stNodeEditor1.SetReroutePoints(pinOut, pinIn, route);
        }

        /// <summary>
        /// Draw a connection's line through these reroute points (none: straight from pin to pin), as an undo or redo asks.
        /// False when the line is not on the page.
        /// </summary>
        internal bool SetRoute(LineRef line, PointF[] points)
        {
            STNode source = ResolveNode(line?.Source);
            STNode target = ResolveNode(line?.Target);
            if (source == null || target == null)
                return false;
            STNodeOption pinOut = FindOption(source, line.SourcePin, line.SourceSide) ?? source.GetOption(line.SourcePin);
            STNodeOption pinIn = FindOption(target, line.TargetPin, line.TargetSide) ?? target.GetOption(line.TargetPin);
            if (pinOut == null || pinIn == null || !stNodeEditor1.SetReroutePoints(pinOut, pinIn, points))
                return false;
            stNodeEditor1.Invalidate();
            DirtyTracker.MarkLevelDataModified();
            return true;
        }

        /// <summary>The pins a node shows now, and where it sits.</summary>
        internal PinSet SnapshotPins(STNode node)
        {
            PinSet set = new PinSet() { Location = node.Location };
            foreach (STNodeOption option in node.GetAllOptions())
            {
                if (option == null || option == STNodeOption.Empty)
                    continue;
                set.Pins.Add(new PinSnapshot() { Parameter = option.ShortGUID, Location = option.Location, Style = option.Style });
            }
            return set;
        }

        /// <summary>Give a node exactly this pin set, disconnecting whatever a removed pin carried, and put it where the set says.</summary>
        internal void SetNodePins(STNode node, PinSet target)
        {
            if (node == null || target == null)
                return;

            using (SuppressRecording())
            {
                bool wasAutoSize = node.AutoSize;
                node.AutoSize = false;
                try
                {
                    foreach (STNodeOption existing in node.GetAllOptions())
                    {
                        if (existing == null || existing == STNodeOption.Empty)
                            continue;
                        if (target.Pins.Any(o => o.Parameter == existing.ShortGUID && o.Location == existing.Location))
                            continue;
                        existing.DisconnectAll();
                        RemoveOption(node, existing);
                    }
                    foreach (PinSnapshot pin in target.Pins)
                    {
                        if (FindOption(node, pin.Parameter, pin.Location) == null)
                            AddOption(node, pin.Parameter, pin.Location, pin.Style);
                    }
                    node.AlignRelayRows(_composite, _commands);
                    UpdatePinTexts(node);
                    node.Recompute();
                }
                finally
                {
                    node.AutoSize = wasAutoSize;
                    node.EnsureProperNodeSizing();
                }
                node.SetPosition(target.Location);
                stNodeEditor1.Invalidate();
            }
            DirtyTracker.MarkLevelDataModified();
        }

        private void RecordPinChange(STNode node, PinSet before, string label)
        {
            if (!CanRecord || node?.Entity == null || before == null)
                return;
            PinSet after = SnapshotPins(node);
            if (PinSet.Same(before, after))
                return;
            UndoStack.Current.Record(new PinSetEdit(_composite, _flowgraphName, new NodeRef(node), before, after, label));
        }

        /// <summary>
        /// The destination-pin popup's choice: a pin made on the target node and the connection into it,
        /// as one step.
        /// </summary>
        internal void ConnectToChosenPin(STNodeOption from, STNode node, ShortGuid pin, PinLocation side)
        {
            if (from == null || node == null || _composite == null)
                return;

            using (UndoStack.Current.BeginGroup(null))
            {
                PinSet before = SnapshotPins(node);
                STNodeOption option = FindOption(node, pin, side) ?? AddOption(node, pin, side, PinStyle.ArrowDown);
                node.AlignRelayRows(_composite, _commands);
                node.Recompute();
                RecordPinChange(node, before, "Add pin " + pin + " to " + UndoLabels.Entity(_composite, node.Entity));
                from.ConnectOption(option);
                node.EnsureProperNodeSizing();
                stNodeEditor1.Invalidate();
            }
        }

        private static void RemoveOption(STNode node, STNodeOption option)
        {
            switch (option.Location)
            {
                case PinLocation.Left: node.RemoveInputOption(option.ShortGUID); break;
                case PinLocation.Right: node.RemoveOutputOption(option.ShortGUID); break;
                case PinLocation.Top: node.RemoveTopOption(option.ShortGUID); break;
                default: node.RemoveBottomOption(option.ShortGUID); break;
            }
        }

        /// <summary>The page as a layout, stored in the table as it stands now.</summary>
        internal FlowgraphMeta CaptureLayout()
        {
            return FlowgraphLayoutManager.SaveLayout(stNodeEditor1, _composite, _flowgraphName);
        }

        internal void RenameForUndo(string name)
        {
            FlowgraphMeta layout = FlowgraphLayoutManager.GetLayouts(_composite).FirstOrDefault(o => o.Name == _flowgraphName);
            if (layout != null)
                layout.Name = name;
            this.Text = name;
            _flowgraphName = name;
        }

        private static STNodeOption FindOption(STNode node, ShortGuid parameter, PinLocation side)
        {
            STNodeOption[] options;
            switch (side)
            {
                case PinLocation.Left: options = node.GetInputOptions(); break;
                case PinLocation.Right: options = node.GetOutputOptions(); break;
                case PinLocation.Top: options = node.GetTopOptions(); break;
                default: options = node.GetBottomOptions(); break;
            }
            foreach (STNodeOption option in options)
            {
                if (option != null && option != STNodeOption.Empty && option.ShortGUID == parameter)
                    return option;
            }
            return null;
        }

        private static STNodeOption AddOption(STNode node, ShortGuid parameter, PinLocation side, PinStyle style)
        {
            switch (side)
            {
                case PinLocation.Left: return node.AddInputOption(parameter);
                case PinLocation.Right: return node.AddOutputOption(parameter);
                case PinLocation.Top: return node.AddTopOption(parameter, style);
                default: return node.AddBottomOption(parameter);
            }
        }
    }
}
