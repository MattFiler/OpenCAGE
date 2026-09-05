using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.Undo
{
    /// <summary>
    /// A TriggerSequence editor session. The editor rewrites the sequence and method lists in place
    /// from many handlers, so the session is one edit: the lists as they were when the window opened
    /// and as they were when it closed, each kept as its own copy.
    /// </summary>
    public sealed class TriggerSequenceEdit : IEdit
    {
        private readonly ShortGuid _composite;
        private readonly ShortGuid _entity;
        private readonly List<TriggerSequence.SequenceEntry> _beforeSequence;
        private readonly List<TriggerSequence.MethodEntry> _beforeMethods;
        private readonly List<TriggerSequence.SequenceEntry> _afterSequence;
        private readonly List<TriggerSequence.MethodEntry> _afterMethods;

        public string Label { get; }
        public ShortGuid CompositeId => _composite;
        public ShortGuid EntityId => _entity;

        public TriggerSequenceEdit(Composite composite, Entity entity,
            List<TriggerSequence.SequenceEntry> beforeSequence, List<TriggerSequence.MethodEntry> beforeMethods,
            List<TriggerSequence.SequenceEntry> afterSequence, List<TriggerSequence.MethodEntry> afterMethods, string label)
        {
            _composite = composite.shortGUID;
            _entity = entity.shortGUID;
            _beforeSequence = beforeSequence;
            _beforeMethods = beforeMethods;
            _afterSequence = afterSequence;
            _afterMethods = afterMethods;
            Label = label;
        }

        public static List<TriggerSequence.SequenceEntry> CloneSequence(IEnumerable<TriggerSequence.SequenceEntry> entries)
        {
            List<TriggerSequence.SequenceEntry> clone = new List<TriggerSequence.SequenceEntry>();
            if (entries == null)
                return clone;
            foreach (TriggerSequence.SequenceEntry entry in entries)
            {
                clone.Add(new TriggerSequence.SequenceEntry()
                {
                    timing = entry.timing,
                    connectedEntity = entry.connectedEntity?.Copy() ?? new EntityPath(),
                });
            }
            return clone;
        }

        public static List<TriggerSequence.MethodEntry> CloneMethods(IEnumerable<TriggerSequence.MethodEntry> entries)
        {
            List<TriggerSequence.MethodEntry> clone = new List<TriggerSequence.MethodEntry>();
            if (entries == null)
                return clone;
            foreach (TriggerSequence.MethodEntry entry in entries)
                clone.Add(new TriggerSequence.MethodEntry(entry.method, entry.relay, entry.finished));
            return clone;
        }

        public void Apply(UndoContext context) => Set(context, _afterSequence, _afterMethods);
        public void Revert(UndoContext context) => Set(context, _beforeSequence, _beforeMethods);

        private void Set(UndoContext context, List<TriggerSequence.SequenceEntry> sequence, List<TriggerSequence.MethodEntry> methods)
        {
            Entity entity = context.RequireEntity(_composite, _entity);
            switch (entity)
            {
                case TriggerSequence triggerSequence:
                    triggerSequence.sequence = CloneSequence(sequence);
                    triggerSequence.methods = CloneMethods(methods);
                    break;
                case ProxyEntity proxy:
                    proxy.sequence = CloneSequence(sequence);
                    proxy.methods = CloneMethods(methods);
                    break;
                default:
                    throw new InvalidOperationException("The entity no longer carries trigger sequence data");
            }
            DirtyTracker.MarkLevelDataModified();
            context.Ui?.ReloadEntity(entity);
        }

        public bool TryMerge(IEdit next) => false;
    }

    /// <summary>
    /// The CAGEAnimation editor works on a copy and commits by handing four lists to the real entity.
    /// Both sets of lists are kept; nothing is cloned.
    /// </summary>
    public sealed class CageAnimationEdit : IEdit
    {
        public struct Lists
        {
            public List<CAGEAnimation.Connection> Connections;
            public List<CAGEAnimation.EventTrack> EventTracks;
            public List<CAGEAnimation.FloatTrack> FloatTracks;
            public List<Parameter> Parameters;

            public static Lists Of(CAGEAnimation animation)
            {
                return new Lists()
                {
                    Connections = animation.connections,
                    EventTracks = animation.eventTracks,
                    FloatTracks = animation.floatTracks,
                    Parameters = animation.parameters,
                };
            }

            public void ApplyTo(CAGEAnimation animation)
            {
                animation.connections = Connections;
                animation.eventTracks = EventTracks;
                animation.floatTracks = FloatTracks;
                animation.parameters = Parameters;
            }
        }

        private readonly ShortGuid _composite;
        private readonly ShortGuid _entity;
        private readonly Lists _before;
        private readonly Lists _after;

        public string Label { get; }
        public ShortGuid CompositeId => _composite;
        public ShortGuid EntityId => _entity;

        public CageAnimationEdit(Composite composite, CAGEAnimation entity, Lists before, Lists after, string label)
        {
            _composite = composite.shortGUID;
            _entity = entity.shortGUID;
            _before = before;
            _after = after;
            Label = label;
        }

        public void Apply(UndoContext context) => Set(context, _after);
        public void Revert(UndoContext context) => Set(context, _before);

        private void Set(UndoContext context, Lists lists)
        {
            CAGEAnimation animation = context.RequireEntity(_composite, _entity) as CAGEAnimation;
            if (animation == null)
                throw new InvalidOperationException("The entity is no longer a CAGEAnimation");
            lists.ApplyTo(animation);
            DirtyTracker.MarkLevelDataModified();
            Singleton.OnParameterModified?.Invoke();
            context.Ui?.ReloadEntity(animation);
        }

        public bool TryMerge(IEdit next) => false;
    }

    /// <summary>
    /// A resource dialog session on an entity: either its "resource" parameter (which the dialog may
    /// have had to create) or its entity-level resource list. References are kept as shallow copies
    /// with their own renderable lists; the models, materials and REDS entries they name stay shared.
    /// </summary>
    public sealed class ResourceSessionEdit : IEdit
    {
        private readonly ShortGuid _composite;
        private readonly ShortGuid _entity;
        private readonly bool _parameterMode;

        private readonly Parameter _parameter;
        private readonly bool _hadParameter;
        private readonly int _index;
        private readonly ParameterData _beforeContent;
        private readonly ParameterVariant _beforeVariant;
        private readonly cResource _after;

        private readonly List<ResourceReference> _beforeList;
        private readonly List<ResourceReference> _afterList;

        public string Label { get; }
        public ShortGuid CompositeId => _composite;
        public ShortGuid EntityId => _entity;

        /// <summary>The dialog edited the entity's "resource" parameter.</summary>
        public ResourceSessionEdit(Composite composite, FunctionEntity entity, Parameter parameter, bool hadParameter, int index,
            ParameterData beforeContent, ParameterVariant beforeVariant, cResource after, string label)
        {
            _composite = composite.shortGUID;
            _entity = entity.shortGUID;
            _parameterMode = true;
            _parameter = parameter;
            _hadParameter = hadParameter;
            _index = index;
            _beforeContent = beforeContent;
            _beforeVariant = beforeVariant;
            _after = after;
            Label = label;
        }

        /// <summary>The dialog edited the entity's own resource list.</summary>
        public ResourceSessionEdit(Composite composite, FunctionEntity entity, List<ResourceReference> before, List<ResourceReference> after, string label)
        {
            _composite = composite.shortGUID;
            _entity = entity.shortGUID;
            _parameterMode = false;
            _beforeList = before;
            _afterList = after;
            Label = label;
        }

        public static List<ResourceReference> CloneReferences(IEnumerable<ResourceReference> references)
        {
            List<ResourceReference> clone = new List<ResourceReference>();
            if (references == null)
                return clone;
            foreach (ResourceReference reference in references)
            {
                if (reference == null)
                    continue;
                ResourceReference copy = (ResourceReference)reference.Clone();
                if (copy.RenderableInstance != null)
                    copy.RenderableInstance = new List<RenderableElements.Element>(copy.RenderableInstance);
                clone.Add(copy);
            }
            return clone;
        }

        /// <summary>Same references in the same order, judged by what they point at rather than object identity.</summary>
        public static bool ReferencesEqual(List<ResourceReference> a, List<ResourceReference> b)
        {
            int countA = a?.Count ?? 0, countB = b?.Count ?? 0;
            if (countA != countB)
                return false;
            for (int i = 0; i < countA; i++)
            {
                ResourceReference x = a[i], y = b[i];
                if (x == null || y == null)
                {
                    if (!ReferenceEquals(x, y))
                        return false;
                    continue;
                }
                if (x.resource_type != y.resource_type || x.resource_id != y.resource_id || x.entityID != y.entityID
                    || x.position != y.position || x.rotation != y.rotation
                    || !ReferenceEquals(x.PhysicsSystem, y.PhysicsSystem) || x.PhysicsSystemIndex != y.PhysicsSystemIndex
                    || !ReferenceEquals(x.AnimatedModel, y.AnimatedModel) || !ReferenceEquals(x.CollisionMapping, y.CollisionMapping))
                    return false;

                int runA = x.RenderableInstance?.Count ?? 0, runB = y.RenderableInstance?.Count ?? 0;
                if (runA != runB)
                    return false;
                for (int e = 0; e < runA; e++)
                {
                    if (!ReferenceEquals(x.RenderableInstance[e], y.RenderableInstance[e]))
                        return false;
                }
            }
            return true;
        }

        public void Apply(UndoContext context)
        {
            FunctionEntity entity = RequireFunction(context);
            if (_parameterMode)
            {
                Parameter live = entity.GetParameter(_parameter.name);
                if (live == null)
                {
                    entity.parameters.Insert(Math.Min(Math.Max(_index, 0), entity.parameters.Count), _parameter);
                    live = _parameter;
                }
                if (live.content is cResource resource)
                    ParameterValues.CopyInto(_after, resource);
                else
                    live.content = ParameterValues.Clone(_after);
                live.variant = ParameterVariant.INTERNAL;
                Singleton.OnEntityParameterModified?.Invoke(entity, live, false);
            }
            else
            {
                entity.resources = CloneReferences(_afterList);
            }
            Finish(context, entity);
        }

        public void Revert(UndoContext context)
        {
            FunctionEntity entity = RequireFunction(context);
            if (_parameterMode)
            {
                Parameter live = entity.GetParameter(_parameter.name);
                if (!_hadParameter)
                {
                    if (live != null)
                    {
                        Singleton.OnEntityParameterModified?.Invoke(entity, live, true);
                        entity.parameters.Remove(live);
                    }
                }
                else if (live != null)
                {
                    //Write through the object the inspector holds where the type allows
                    if (live.content is cResource resource && _beforeContent is cResource before)
                        ParameterValues.CopyInto(before, resource);
                    else
                        live.content = ParameterValues.Clone(_beforeContent);
                    live.variant = _beforeVariant;
                    Singleton.OnEntityParameterModified?.Invoke(entity, live, false);
                }
            }
            else
            {
                entity.resources = CloneReferences(_beforeList);
            }
            Finish(context, entity);
        }

        private FunctionEntity RequireFunction(UndoContext context)
        {
            FunctionEntity entity = context.RequireEntity(_composite, _entity) as FunctionEntity;
            if (entity == null)
                throw new InvalidOperationException("The entity is no longer a function entity");
            return entity;
        }

        private static void Finish(UndoContext context, Entity entity)
        {
            Singleton.OnResourceModified?.Invoke();
            Singleton.OnParameterModified?.Invoke();
            context.Ui?.ReloadEntity(entity);
        }

        public bool TryMerge(IEdit next) => false;
    }

    /// <summary>
    /// A link in the composite's own data, for composites edited through the links panel rather than
    /// a flowgraph page (there the page is the truth and <see cref="LinkEdit"/> applies).
    /// </summary>
    public sealed class LinkDataEdit : IEdit
    {
        private readonly ShortGuid _composite;
        private readonly ShortGuid _owner;
        private readonly EntityConnector _link;
        private readonly int _index;
        private readonly bool _presentAfter;

        public string Label { get; }
        public ShortGuid CompositeId => _composite;
        public ShortGuid EntityId => _owner;

        public LinkDataEdit(Composite composite, Entity owner, EntityConnector link, int index, bool presentAfter, string label)
        {
            _composite = composite.shortGUID;
            _owner = owner.shortGUID;
            _link = link;
            _index = index;
            _presentAfter = presentAfter;
            Label = label;
        }

        public void Apply(UndoContext context) => Set(context, _presentAfter);
        public void Revert(UndoContext context) => Set(context, !_presentAfter);

        private void Set(UndoContext context, bool present)
        {
            Entity owner = context.RequireEntity(_composite, _owner);
            if (present)
            {
                if (!owner.childLinks.Any(o => o.ID == _link.ID))
                    owner.childLinks.Insert(Math.Min(Math.Max(_index, 0), owner.childLinks.Count), _link);
            }
            else
            {
                owner.childLinks.RemoveAll(o => o.ID == _link.ID);
            }

            Singleton.OnParameterModified?.Invoke();
            DirtyTracker.MarkLevelDataModified();
            context.Ui?.ReloadEntity(owner);
            Entity linked = context.Entity(_composite, _link.linkedEntityID);
            if (linked != null)
                context.Ui?.ReloadEntity(linked);
        }

        public bool TryMerge(IEdit next) => false;
    }
}
