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
    /// <remarks>
    /// What elsewhere in the level reaches through the entity goes in the same step: aliases (in any composite) whose path
    /// passes through it, and trigger sequence entries and animation bindings whose path does. Left behind, they would be
    /// dropped the next time their composite opens (CommandsUtils.PurgeDeadLinks), outside any undo step, so undoing the
    /// delete afterwards could not bring them back. Proxies stay, as dead proxies always have.
    /// </remarks>
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
            public ShortGuid Composite;
            public ShortGuid Owner;
            public int Index;
            public TriggerSequence.SequenceEntry Entry;
        }
        private struct AnimationRecord
        {
            public ShortGuid Composite;
            public ShortGuid Owner;
            public int Index;
            public CAGEAnimation.Connection Connection;
        }
        /// <summary>An alias elsewhere whose path passed through the entity: it goes with it, with the links into it in its composite.</summary>
        private sealed class AliasRecord
        {
            public ShortGuid Composite;
            public AliasEntity Alias;
            public List<LinkRecord> IncomingLinks = new List<LinkRecord>();
            public List<NodeSnapshot> Nodes;
        }

        private readonly ShortGuid _composite;
        private readonly Entity _entity;
        private List<LinkRecord> _incomingLinks;
        private List<TriggerRecord> _triggerEntries;
        private List<AnimationRecord> _animationConnections;
        private List<AliasRecord> _aliases;
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

            //What reaches through the entity from anywhere in the level, found while it is still there to resolve through
            HashSet<TriggerSequence.SequenceEntry> throughEntries = new HashSet<TriggerSequence.SequenceEntry>();
            HashSet<CAGEAnimation.Connection> throughConnections = new HashSet<CAGEAnimation.Connection>();
            List<(Composite holder, Entity owner)> reachingOwners = new List<(Composite, Entity)>();
            _aliases = new List<AliasRecord>();
            FindReaching(commands, composite, throughEntries, throughConnections, reachingOwners, _aliases);

            //The nodes the entity has on the open pages go when OnEntityDeleted fires, so they are
            //taken first; the saved layouts are trimmed below, and the manager keeps what it trimmed for us
            _deletedOffScreen = context.Ui != null && !context.Ui.IsShowing(composite);
            _hadVerdict = FlowgraphLayoutManager.HasCompatibilityInfo(composite);
            _nodes = context.Ui?.CaptureNodes(composite, _entity);
            foreach (AliasRecord alias in _aliases)
                alias.Nodes = context.Ui?.CaptureNodes(commands.GetComposite(alias.Composite), alias.Alias);

            /* The saved layouts lose the entity and the aliases going with it in one pass, while the capture is open.
               A pass per entity kept each page's lists as that pass found them, so a page two passes trimmed was put
               back by undo as the first pass left it - without the nodes (and their connections) the first pass took,
               which the next compile of that page then took out of the script. And each pass visits every saved page
               in the level. The pending events each deletion still raises below find their entity already trimmed. */
            IDisposable together = null;
            FlowgraphLayoutManager.BeginTrimCapture();
            try
            {
                together = FlowgraphLayoutManager.BeginDeletingTogether(new Entity[] { _entity }.Concat(_aliases.Select(o => (Entity)o.Alias)), composite);
            }
            finally
            {
                _layoutTrim = FlowgraphLayoutManager.EndTrimCapture();
            }
            using (together)
                Remove(composite, commands, throughEntries, throughConnections, reachingOwners);
            context.Ui?.RefreshNodeMarkers();
        }

        /// <summary>Take the entity, and what reaches through it, out of the level, recording each piece; then say they went.</summary>
        private void Remove(Composite composite, Commands commands, HashSet<TriggerSequence.SequenceEntry> throughEntries,
            HashSet<CAGEAnimation.Connection> throughConnections, List<(Composite holder, Entity owner)> reachingOwners)
        {
            Singleton.OnEntityDeletePending?.Invoke(_entity, composite);

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
                //as the editor has always done, with those found reaching through us deeper down;
                //exactly those come back on undo
                Prune(composite, other, entry => (other is TriggerSequence && PointsThroughUs(entry.connectedEntity)) || throughEntries.Contains(entry),
                    connection => PointsThroughUs(connection.connectedEntity) || throughConnections.Contains(connection));
            }

            //Elsewhere in the level: the aliases reaching through it, with the links into them, and the entries reaching through it
            foreach (AliasRecord record in _aliases)
            {
                Composite holder = commands.GetComposite(record.Composite);
                if (holder == null || holder.GetEntityByID(record.Alias.shortGUID) != record.Alias) continue;
                holder.RemoveAlias(record.Alias.shortGUID);
                foreach (Entity other in holder.GetEntities())
                {
                    if (!other.childLinks.Any(o => o.linkedEntityID == record.Alias.shortGUID)) continue;
                    List<EntityConnector> keptLinks = new List<EntityConnector>(other.childLinks.Count);
                    for (int i = 0; i < other.childLinks.Count; i++)
                    {
                        if (other.childLinks[i].linkedEntityID == record.Alias.shortGUID)
                            record.IncomingLinks.Add(new LinkRecord() { Owner = other.shortGUID, Index = i, Link = other.childLinks[i] });
                        else
                            keptLinks.Add(other.childLinks[i]);
                    }
                    other.childLinks = keptLinks;
                }
            }
            foreach ((Composite holder, Entity owner) in reachingOwners)
            {
                if (holder == composite) continue; //done above
                Prune(holder, owner, entry => throughEntries.Contains(entry), connection => throughConnections.Contains(connection));
            }

            commands.Utils.PurgedComposites.purged.Clear(); //TODO: we should smartly remove from this list, rather than removing all

            Singleton.OnEntityDeleted?.Invoke(_entity);

            /* Each alias with its own pending event just before its deletion, as the viewer's own alias removals do:
               the pending event names the composite the ENTITY_DELETED that follows goes to (Send keeps one, and takes
               the open composite otherwise, where an alias held elsewhere is not). One packet per composite that held them. */
            foreach (IGrouping<ShortGuid, AliasRecord> held in _aliases.GroupBy(o => o.Composite))
            {
                Composite holder = commands.GetComposite(held.Key);
                using (UnityConnection.Send.BeginDeletedBatch(holder))
                {
                    foreach (AliasRecord record in held)
                    {
                        Singleton.OnEntityDeletePending?.Invoke(record.Alias, holder);
                        Singleton.OnEntityDeleted?.Invoke(record.Alias);
                    }
                }
            }
        }

        private bool PointsThroughUs(EntityPath path)
        {
            return path?.path != null && path.path.Length >= 2 && path.path[path.path.Length - 2] == _entity.shortGUID;
        }

        /// <summary>Take the matching sequence entries (of a TriggerSequence, or a proxy of one) or animation bindings off <paramref name="owner"/>, recording each where it was.</summary>
        private void Prune(Composite holder, Entity owner, Func<TriggerSequence.SequenceEntry, bool> entryGoes, Func<CAGEAnimation.Connection, bool> connectionGoes)
        {
            List<TriggerSequence.SequenceEntry> sequence = (owner as TriggerSequence)?.sequence ?? (owner as ProxyEntity)?.sequence;
            if (sequence != null)
            {
                List<TriggerSequence.SequenceEntry> kept = new List<TriggerSequence.SequenceEntry>(sequence.Count);
                for (int i = 0; i < sequence.Count; i++)
                {
                    if (sequence[i] != null && entryGoes(sequence[i]))
                        _triggerEntries.Add(new TriggerRecord() { Composite = holder.shortGUID, Owner = owner.shortGUID, Index = i, Entry = sequence[i] });
                    else
                        kept.Add(sequence[i]);
                }
                if (kept.Count != sequence.Count)
                {
                    if (owner is TriggerSequence trigger) trigger.sequence = kept;
                    else ((ProxyEntity)owner).sequence = kept;
                }
            }
            else if (owner is CAGEAnimation animation)
            {
                List<CAGEAnimation.Connection> kept = new List<CAGEAnimation.Connection>(animation.connections.Count);
                for (int i = 0; i < animation.connections.Count; i++)
                {
                    if (animation.connections[i] != null && connectionGoes(animation.connections[i]))
                        _animationConnections.Add(new AnimationRecord() { Composite = holder.shortGUID, Owner = owner.shortGUID, Index = i, Connection = animation.connections[i] });
                    else
                        kept.Add(animation.connections[i]);
                }
                if (kept.Count != animation.connections.Count)
                    animation.connections = kept;
            }
        }

        /// <summary>
        /// What stored elsewhere reaches through the entity (an instance, to something inside it) or to it: trigger sequence entries
        /// and animation bindings in any composite whose path, as the editor resolves it, passes through it, and aliases in any
        /// composite whose path does. An entry whose path merely shares an id with it does not count. Nothing reaches through an
        /// alias or a proxy, so deleting one of those finds nothing. Read before the entity goes.
        /// </summary>
        private void FindReaching(Commands commands, Composite composite, HashSet<TriggerSequence.SequenceEntry> entries, HashSet<CAGEAnimation.Connection> connections,
            List<(Composite holder, Entity owner)> owners, List<AliasRecord> aliases)
        {
            if (_entity is AliasEntity || _entity is ProxyEntity)
                return;
            ShortGuid id = _entity.shortGUID;
            bool Through(List<Tuple<Composite, Entity>> resolved) => resolved != null && resolved.Any(o => o.Item2 == _entity && o.Item1 == composite);
            foreach (Composite holder in commands.Entries)
            {
                if (holder == null) continue;
                foreach (AliasEntity alias in holder.aliases_dictionary.Values)
                {
                    if (alias?.alias?.path == null || !alias.alias.path.Contains(id)) continue;
                    if (Through(commands.Utils.ResolveAlias(alias, holder)))
                        aliases.Add(new AliasRecord() { Composite = holder.shortGUID, Alias = alias });
                }
                foreach (FunctionEntity function in holder.functions_dictionary.Values)
                {
                    bool found = false;
                    if (function is TriggerSequence trigger)
                        foreach (TriggerSequence.SequenceEntry entry in trigger.sequence)
                            if (entry?.connectedEntity?.path != null && entry.connectedEntity.path.Contains(id) && Through(commands.Utils.ResolveEntityPath(entry.connectedEntity, holder)) && entries.Add(entry)) found = true;
                    if (function is CAGEAnimation animation)
                        foreach (CAGEAnimation.Connection connection in animation.connections)
                            if (connection?.connectedEntity?.path != null && connection.connectedEntity.path.Contains(id) && Through(commands.Utils.ResolveEntityPath(connection.connectedEntity, holder)) && connections.Add(connection)) found = true;
                    if (found) owners.Add((holder, function));
                }
                //A proxy of a trigger sequence keeps its own list of entries
                foreach (ProxyEntity proxy in holder.proxies_dictionary.Values)
                {
                    if (proxy?.sequence == null) continue;
                    bool found = false;
                    foreach (TriggerSequence.SequenceEntry entry in proxy.sequence)
                        if (entry?.connectedEntity?.path != null && entry.connectedEntity.path.Contains(id) && Through(commands.Utils.ResolveEntityPath(entry.connectedEntity, holder)) && entries.Add(entry)) found = true;
                    if (found) owners.Add((holder, proxy));
                }
            }
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

            //The aliases elsewhere that reached through it, before the links into them: those were taken after the
            //links into the entity, from the lists as they were then, so they go back first (the last alias first)
            List<AliasRecord> aliases = _aliases ?? new List<AliasRecord>();
            foreach (AliasRecord record in aliases)
            {
                Composite holder = context.Composite(record.Composite);
                if (holder != null && holder.GetEntityByID(record.Alias.shortGUID) == null)
                    holder.AddAlias(record.Alias);
            }
            for (int a = aliases.Count - 1; a >= 0; a--)
            {
                Composite holder = context.Composite(aliases[a].Composite);
                if (holder == null) continue;
                foreach (LinkRecord record in aliases[a].IncomingLinks)
                {
                    Entity owner = holder.GetEntityByID(record.Owner);
                    if (owner != null)
                        owner.childLinks.Insert(Math.Min(record.Index, owner.childLinks.Count), record.Link);
                }
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
                Entity owner = context.Entity(record.Composite, record.Owner);
                if (owner is TriggerSequence trigger)
                    trigger.sequence.Insert(Math.Min(record.Index, trigger.sequence.Count), record.Entry);
                else if (owner is ProxyEntity proxy && proxy.sequence != null)
                    proxy.sequence.Insert(Math.Min(record.Index, proxy.sequence.Count), record.Entry);
            }
            foreach (AnimationRecord record in _animationConnections)
            {
                //Into a new list: the one the animation holds may be an undo step's own record of it
                //(CageAnimationEdit keeps the list objects), which an insert in place would corrupt
                if (context.Entity(record.Composite, record.Owner) is CAGEAnimation owner)
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
            foreach (AliasRecord record in aliases)
                Singleton.OnEntityAdded?.Invoke(record.Alias);
            ReaddAliasesInViewer(context, aliases);

            if (_nodes != null && _nodes.Count > 0)
                context.Ui?.RestoreNodes(composite, _nodes);
            foreach (AliasRecord record in aliases)
                if (record.Nodes != null && record.Nodes.Count > 0 && context.Composite(record.Composite) is Composite holder)
                    context.Ui?.RestoreNodes(holder, record.Nodes);

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

        /* Send announces an added entity in the composite on screen, which is not where an alias held by another composite
           belongs: the viewer made a copy of it there that resolves from the wrong place. That copy is taken back out, and
           the alias given to the composite that holds it - gone first, as for any re-send, so it never spawns twice. */
        private static void ReaddAliasesInViewer(UndoContext context, List<AliasRecord> aliases)
        {
            Composite open = Singleton.Editor?.CompositeDisplay?.Composite;
            foreach (IGrouping<ShortGuid, AliasRecord> held in aliases.GroupBy(o => o.Composite))
            {
                Composite holder = context.Composite(held.Key);
                if (holder == null || holder == open)
                    continue;
                List<Entity> back = held.Select(o => (Entity)o.Alias).Where(o => holder.GetEntityByID(o.shortGUID) == o).ToList();
                if (back.Count == 0)
                    continue;
                if (open != null)
                    UnityConnection.Send.SendEntitiesDeleted(open, back);
                UnityConnection.Send.SendEntitiesDeleted(holder, back);
                UnityConnection.Send.SendCompositeContents(holder, back);
            }
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
