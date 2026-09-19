using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE.Undo
{
    /// <summary>
    /// An entity leaves its composite. This is the whole of what deleting an entity does to the level
    /// - the dictionary entry, every link into it, the TriggerSequence and CAGEAnimation entries that
    /// pointed at it, its nodes on saved and open flowgraph pages - recorded so that undo puts each
    /// piece back where it was. The Entity object itself is kept rather than copied.
    /// </summary>
    public sealed class EntityDeleteEdit : IEdit
    {
        private struct LinkRecord
        {
            public ShortGuid Owner;
            public int Index;
            public EntityConnector Link;
        }
        private struct TriggerRecord
        {
            public ShortGuid Owner;
            public int Index;
            public TriggerSequence.SequenceEntry Entry;
        }
        private struct AnimationRecord
        {
            public ShortGuid Owner;
            public int Index;
            public CAGEAnimation.Connection Connection;
        }

        private readonly ShortGuid _composite;
        private readonly Entity _entity;
        private List<LinkRecord> _incomingLinks;
        private List<TriggerRecord> _triggerEntries;
        private List<AnimationRecord> _animationConnections;
        private FlowgraphLayoutManager.LayoutTrim _layoutTrim;
        private List<NodeSnapshot> _nodes;
        private bool _deletedOffScreen;
        private bool _hadVerdict;

        public string Label { get; }
        public ShortGuid CompositeId => _composite;
        public ShortGuid EntityId => _entity.shortGUID;

        public EntityDeleteEdit(Composite composite, Entity entity, string label)
        {
            _composite = composite.shortGUID;
            _entity = entity;
            Label = label;
        }

        /// <summary>Remove the entity, capturing everything the removal disturbs.</summary>
        public void Apply(UndoContext context)
        {
            Composite composite = context.RequireComposite(_composite);
            if (composite.GetEntityByID(_entity.shortGUID) == null)
                throw new InvalidOperationException("The entity is no longer in the composite");
            Commands commands = context.Commands;

            //The nodes the entity has on the open pages go when OnEntityDeleted fires, so they are
            //taken first; the saved layouts are trimmed by the pending event, and the manager keeps
            //what it trimmed for us
            _deletedOffScreen = context.Ui != null && !context.Ui.IsShowing(composite);
            _hadVerdict = FlowgraphLayoutManager.HasCompatibilityInfo(composite);
            _nodes = context.Ui?.CaptureNodes(composite, _entity);

            FlowgraphLayoutManager.BeginTrimCapture();
            try
            {
                Singleton.OnEntityDeletePending?.Invoke(_entity, composite);
            }
            finally
            {
                _layoutTrim = FlowgraphLayoutManager.EndTrimCapture();
            }

            switch (_entity.variant)
            {
                case EntityVariant.VARIABLE:
                    composite.RemoveVariable(_entity.shortGUID);
                    break;
                case EntityVariant.FUNCTION:
                    composite.RemoveFunction(_entity.shortGUID);
                    break;
                case EntityVariant.ALIAS:
                    composite.RemoveAlias(_entity.shortGUID);
                    break;
                case EntityVariant.PROXY:
                    composite.RemoveProxy(_entity.shortGUID);
                    break;
            }

            _incomingLinks = new List<LinkRecord>();
            _triggerEntries = new List<TriggerRecord>();
            _animationConnections = new List<AnimationRecord>();
            foreach (Entity other in composite.GetEntities())
            {
                List<EntityConnector> keptLinks = new List<EntityConnector>(other.childLinks.Count);
                for (int i = 0; i < other.childLinks.Count; i++)
                {
                    if (other.childLinks[i].linkedEntityID == _entity.shortGUID)
                        _incomingLinks.Add(new LinkRecord() { Owner = other.shortGUID, Index = i, Link = other.childLinks[i] });
                    else
                        keptLinks.Add(other.childLinks[i]);
                }
                if (keptLinks.Count != other.childLinks.Count)
                    other.childLinks = keptLinks;

                //Trigger and animation references whose path ends "...-> us -> something" are pruned,
                //as the editor has always done; exactly those come back on undo
                if (other is TriggerSequence triggerSequence)
                {
                    List<TriggerSequence.SequenceEntry> kept = new List<TriggerSequence.SequenceEntry>(triggerSequence.sequence.Count);
                    for (int i = 0; i < triggerSequence.sequence.Count; i++)
                    {
                        if (PointsThroughUs(triggerSequence.sequence[i].connectedEntity))
                            _triggerEntries.Add(new TriggerRecord() { Owner = other.shortGUID, Index = i, Entry = triggerSequence.sequence[i] });
                        else
                            kept.Add(triggerSequence.sequence[i]);
                    }
                    if (kept.Count != triggerSequence.sequence.Count)
                        triggerSequence.sequence = kept;
                }
                else if (other is CAGEAnimation animation)
                {
                    List<CAGEAnimation.Connection> kept = new List<CAGEAnimation.Connection>(animation.connections.Count);
                    for (int i = 0; i < animation.connections.Count; i++)
                    {
                        if (PointsThroughUs(animation.connections[i].connectedEntity))
                            _animationConnections.Add(new AnimationRecord() { Owner = other.shortGUID, Index = i, Connection = animation.connections[i] });
                        else
                            kept.Add(animation.connections[i]);
                    }
                    if (kept.Count != animation.connections.Count)
                        animation.connections = kept;
                }
            }

            commands.Utils.PurgedComposites.purged.Clear(); //TODO: we should smartly remove from this list, rather than removing all

            Singleton.OnEntityDeleted?.Invoke(_entity);
            context.Ui?.RefreshNodeMarkers();
        }

        private bool PointsThroughUs(EntityPath path)
        {
            return path?.path != null && path.path.Length >= 2 && path.path[path.path.Length - 2] == _entity.shortGUID;
        }

        /// <summary>Put the entity and everything that referred to it back.</summary>
        public void Revert(UndoContext context)
        {
            Composite composite = context.RequireComposite(_composite);
            if (composite.GetEntityByID(_entity.shortGUID) != null)
                return;

            Singleton.OnEntityAddPending?.Invoke();

            switch (_entity.variant)
            {
                case EntityVariant.VARIABLE:
                    composite.AddVariable((VariableEntity)_entity);
                    break;
                case EntityVariant.FUNCTION:
                    composite.AddFunction((FunctionEntity)_entity);
                    break;
                case EntityVariant.ALIAS:
                    composite.AddAlias((AliasEntity)_entity);
                    break;
                case EntityVariant.PROXY:
                    composite.AddProxy((ProxyEntity)_entity);
                    break;
            }

            //Records were taken in ascending index order per owner, so inserting in the same order
            //lands each one at its original index
            foreach (LinkRecord record in _incomingLinks)
            {
                Entity owner = composite.GetEntityByID(record.Owner);
                if (owner != null)
                    owner.childLinks.Insert(Math.Min(record.Index, owner.childLinks.Count), record.Link);
            }
            foreach (TriggerRecord record in _triggerEntries)
            {
                if (composite.GetEntityByID(record.Owner) is TriggerSequence owner)
                    owner.sequence.Insert(Math.Min(record.Index, owner.sequence.Count), record.Entry);
            }
            foreach (AnimationRecord record in _animationConnections)
            {
                //Into a new list: the one the animation holds may be an undo step's own record of it
                //(CageAnimationEdit keeps the list objects), which an insert in place would corrupt
                if (composite.GetEntityByID(record.Owner) is CAGEAnimation owner)
                {
                    List<CAGEAnimation.Connection> connections = new List<CAGEAnimation.Connection>(owner.connections);
                    connections.Insert(Math.Min(record.Index, connections.Count), record.Connection);
                    owner.connections = connections;
                }
            }

            /* Deleted while its composite was off screen there were no live nodes to take, and its
               pages may have been saved again since (the composite opened and left, the level saved),
               which replaces the layouts the trim knew - so what went is fitted into the current ones */
            List<FlowgraphMeta> restoredLayouts = _layoutTrim?.Restore(_deletedOffScreen);

            if (_entity is FunctionEntity function && !function.function.IsFunctionType)
                context.Content?.EditorUtils?.GenerateCompositeInstances(context.Commands);

            Singleton.OnEntityAdded?.Invoke(_entity);

            if (_nodes != null && _nodes.Count > 0)
                context.Ui?.RestoreNodes(composite, _nodes);

            /* Deleted while its composite was off screen (from the references window, say) there were
               no live nodes to take, and undo has since brought the composite on - building its pages
               from the trimmed layouts. The pages the trim touched are built again from the restored
               ones, now that the entity and its links are back; otherwise the next save of those pages
               would lose its nodes, and the links compiled from them. */
            if (_deletedOffScreen && restoredLayouts != null && restoredLayouts.Count > 0)
                context.Ui?.ReloadPages(composite, restoredLayouts);

            /* A composite never opened has no verdict on its pages; undo opening it to put the entity
               back gave it one - and with the entity's links gone at the time, possibly an empty
               default page and "supported", which the next leave would compile the restored links
               away against. Forgotten again, so the next open judges it with its links back. */
            if (_deletedOffScreen && !_hadVerdict && FlowgraphLayoutManager.GetLayouts(composite).All(o => o.Nodes.Count == 0))
            {
                FlowgraphLayoutManager.RemoveAllLayouts(composite);
                FlowgraphLayoutManager.ClearCompatibilityInfo(composite);
            }
            context.Ui?.RefreshNodeMarkers();
        }

        public bool TryMerge(IEdit next) => false;
    }

    /// <summary>
    /// An entity was created. The creation itself was the editor's; undoing it is a deletion, and
    /// that deletion's record is what a redo restores from.
    /// </summary>
    public sealed class EntityAddEdit : IEdit
    {
        private readonly ShortGuid _composite;
        private readonly Entity _entity;
        private EntityDeleteEdit _removal = null;

        public string Label { get; }
        public ShortGuid CompositeId => _composite;
        public ShortGuid EntityId => _entity.shortGUID;

        public EntityAddEdit(Composite composite, Entity entity, string label)
        {
            _composite = composite.shortGUID;
            _entity = entity;
            Label = label;
        }

        public void Apply(UndoContext context)
        {
            if (_removal == null)
                throw new InvalidOperationException("The entity was never removed, so there is nothing to restore");
            _removal.Revert(context);
        }

        public void Revert(UndoContext context)
        {
            if (_removal == null)
                _removal = new EntityDeleteEdit(context.RequireComposite(_composite), _entity, Label);
            _removal.Apply(context);
        }

        public bool TryMerge(IEdit next) => false;
    }

    /// <summary>
    /// A proxy pointed at a different entity: its path, and the record it keeps of the target's type
    /// (ProxyEntity.function). The links through the proxy stay as they are - that is the point of
    /// re-pointing rather than replacing (see CommandsUtils.IsDeadProxy).
    /// </summary>
    public sealed class ProxyRetargetEdit : IEdit
    {
        private readonly ShortGuid[] _pathBefore, _pathAfter;
        private readonly ShortGuid _functionBefore, _functionAfter;

        public string Label { get; }
        public ShortGuid CompositeId { get; }
        public ShortGuid EntityId { get; }

        /// <summary>Record after the change has been made to <paramref name="proxy"/>.</summary>
        public ProxyRetargetEdit(Composite composite, ProxyEntity proxy, ShortGuid[] pathBefore, ShortGuid functionBefore, string label)
        {
            CompositeId = composite.shortGUID;
            EntityId = proxy.shortGUID;
            _pathBefore = (ShortGuid[])(pathBefore ?? new ShortGuid[0]).Clone();
            _functionBefore = functionBefore;
            _pathAfter = (ShortGuid[])(proxy.proxy?.path ?? new ShortGuid[0]).Clone();
            _functionAfter = proxy.function;
            Label = label;
        }

        public void Apply(UndoContext context) => Set(context, _pathAfter, _functionAfter);
        public void Revert(UndoContext context) => Set(context, _pathBefore, _functionBefore);

        private void Set(UndoContext context, ShortGuid[] path, ShortGuid function)
        {
            Composite composite = context.RequireComposite(CompositeId);
            if (!(composite.GetEntityByID(EntityId) is ProxyEntity proxy))
                throw new InvalidOperationException("The proxy this change belongs to no longer exists");
            proxy.proxy = new EntityPath((ShortGuid[])path.Clone());
            proxy.function = function;
            context.Ui?.ProxyRetargeted(composite, proxy);
        }

        public bool TryMerge(IEdit next) => false;
    }
}
