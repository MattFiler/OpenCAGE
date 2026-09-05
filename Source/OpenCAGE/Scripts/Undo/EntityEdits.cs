using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System;
using System.Collections.Generic;

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
                if (composite.GetEntityByID(record.Owner) is CAGEAnimation owner)
                    owner.connections.Insert(Math.Min(record.Index, owner.connections.Count), record.Connection);
            }

            _layoutTrim?.Restore();

            if (_entity is FunctionEntity function && !function.function.IsFunctionType)
                context.Content?.EditorUtils?.GenerateCompositeInstances(context.Commands);

            Singleton.OnEntityAdded?.Invoke(_entity);

            if (_nodes != null && _nodes.Count > 0)
                context.Ui?.RestoreNodes(composite, _nodes);
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
}
