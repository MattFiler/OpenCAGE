using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.Undo
{
    /// <summary>Copies of parameter values that an edit can keep, and the write-back that keeps the live object.</summary>
    public static class ParameterValues
    {
        public static ParameterData Clone(ParameterData data)
        {
            if (data == null)
                return null;

            ParameterData clone = (ParameterData)data.Clone();
            if (clone is cResource resource && resource.value != null)
            {
                //ParameterData.Clone shallow-copies each ResourceReference, which still shares its
                //renderable list with the live one; take the list too, so an editor filling the live one
                //in place does not rewrite this record. The elements are REDS entries and stay shared -
                //a copy of one would not be the registered run.
                for (int i = 0; i < resource.value.Count; i++)
                {
                    if (resource.value[i]?.RenderableInstance != null)
                        resource.value[i].RenderableInstance = new List<RenderableElements.Element>(resource.value[i].RenderableInstance);
                }
            }
            return clone;
        }

        /// <summary>
        /// Write one value's fields into another of the same type. The target object is kept - the
        /// inspector's rows hold on to it - so this is what an undo writes through. False when the
        /// types differ, in which case the caller replaces the object.
        /// </summary>
        public static bool CopyInto(ParameterData from, ParameterData to)
        {
            if (from == null || to == null || from.dataType != to.dataType)
                return false;

            switch (from)
            {
                case cEnumString source when to is cEnumString target:
                    target.value = source.value;
                    target.enumID = source.enumID;
                    return true;
                case cString source when to is cString target:
                    target.value = source.value;
                    return true;
                case cBool source when to is cBool target:
                    target.value = source.value;
                    return true;
                case cInteger source when to is cInteger target:
                    target.value = source.value;
                    return true;
                case cFloat source when to is cFloat target:
                    target.value = source.value;
                    return true;
                case cVector3 source when to is cVector3 target:
                    target.value = source.value;
                    return true;
                case cTransform source when to is cTransform target:
                    target.position = source.position;
                    target.rotation = source.rotation;
                    return true;
                case cEnum source when to is cEnum target:
                    target.enumID = source.enumID;
                    target.enumIndex = source.enumIndex;
                    return true;
                case cSpline source when to is cSpline target:
                    target.splinePoints = source.splinePoints == null
                        ? new List<cTransform>()
                        : source.splinePoints.Select(o => (cTransform)o.Clone()).ToList();
                    return true;
                case cResource source when to is cResource target:
                    target.shortGUID = source.shortGUID;
                    target.value = ((cResource)Clone(source)).value;
                    return true;
            }
            return false;
        }
    }

    /// <summary>A parameter's value changed. Holds the value before and after, and the "modified" flag with each.</summary>
    public sealed class ParameterValueEdit : IEdit
    {
        /// <summary>Edits to the same parameter closer together than this merge: a spinner run or a gizmo drag is one step.</summary>
        private const int MergeWindowMs = 500;

        private readonly ShortGuid _composite;
        private readonly ShortGuid _entity;
        private readonly ShortGuid _parameter;
        private readonly ParameterData _before;
        private ParameterData _after;
        private readonly bool _modifiedBefore;
        private bool _modifiedAfter;
        private DateTime _stamp = DateTime.UtcNow;

        public string Label { get; }
        public ShortGuid CompositeId => _composite;
        public ShortGuid EntityId => _entity;

        public ParameterValueEdit(Composite composite, Entity entity, ShortGuid parameter, ParameterData before, ParameterData after, bool modifiedBefore, bool modifiedAfter, string label)
        {
            _composite = composite.shortGUID;
            _entity = entity.shortGUID;
            _parameter = parameter;
            _before = before;
            _after = after;
            _modifiedBefore = modifiedBefore;
            _modifiedAfter = modifiedAfter;
            Label = label;
        }

        public void Apply(UndoContext context) => Write(context, _after, _modifiedAfter);
        public void Revert(UndoContext context) => Write(context, _before, _modifiedBefore);

        private void Write(UndoContext context, ParameterData value, bool modified)
        {
            Entity entity = context.RequireEntity(_composite, _entity);
            Parameter parameter = entity.GetParameter(_parameter);
            if (parameter == null)
                throw new InvalidOperationException("Parameter " + _parameter + " no longer exists on the entity");

            if (parameter.content == null || !ParameterValues.CopyInto(value, parameter.content))
                parameter.content = ParameterValues.Clone(value);

            if (modified)
                ParameterModificationTracker.SetParameterModified(_composite, _entity, _parameter);
            else
                ParameterModificationTracker.ClearParameterModified(_composite, _entity, _parameter);

            Notify(entity, parameter);
            context.Ui?.EntityChanged(entity, true);
        }

        /// <summary>The events the editor raises for a value edit, so everything listening updates as it would for one.</summary>
        internal static void Notify(Entity entity, Parameter parameter)
        {
            Singleton.OnEntityParameterModified?.Invoke(entity, parameter, false);
            if (parameter.content is cTransform transform)
                Singleton.OnEntityMoved?.Invoke(transform, entity);
            if (parameter.content is cResource)
                Singleton.OnResourceModified?.Invoke();
            if (parameter.name == ShortGuids.name)
                Singleton.OnEntityRenamed?.Invoke(entity, (parameter.content as cString)?.value ?? "");
            Singleton.OnParameterModified?.Invoke();
        }

        public bool TryMerge(IEdit next)
        {
            ParameterValueEdit other = next as ParameterValueEdit;
            if (other == null || other._composite != _composite || other._entity != _entity || other._parameter != _parameter)
                return false;
            if ((other._stamp - _stamp).TotalMilliseconds > MergeWindowMs)
                return false;

            _after = other._after;
            _modifiedAfter = other._modifiedAfter;
            _stamp = other._stamp;
            return true;
        }
    }

    /// <summary>
    /// A parameter was added to or removed from an entity. Keeps the Parameter object itself, so
    /// putting it back preserves identity for anything still holding it.
    /// </summary>
    public sealed class ParameterPresenceEdit : IEdit
    {
        private readonly ShortGuid _composite;
        private readonly ShortGuid _entity;
        private readonly Parameter _parameter;
        private readonly int _index;
        private readonly bool _presentAfter;
        private readonly bool _modified;

        public string Label { get; }
        public ShortGuid CompositeId => _composite;
        public ShortGuid EntityId => _entity;

        /// <param name="index">Where in the entity's list the parameter sits (or sat).</param>
        /// <param name="presentAfter">True for an add, false for a removal.</param>
        /// <param name="modified">The parameter's "modified" flag while it is present.</param>
        public ParameterPresenceEdit(Composite composite, Entity entity, Parameter parameter, int index, bool presentAfter, bool modified, string label)
        {
            _composite = composite.shortGUID;
            _entity = entity.shortGUID;
            _parameter = parameter;
            _index = index;
            _presentAfter = presentAfter;
            _modified = modified;
            Label = label;
        }

        public void Apply(UndoContext context) => Set(context, _presentAfter);
        public void Revert(UndoContext context) => Set(context, !_presentAfter);

        private void Set(UndoContext context, bool present)
        {
            Entity entity = context.RequireEntity(_composite, _entity);
            Parameter live = entity.GetParameter(_parameter.name);

            if (present)
            {
                if (live == null)
                {
                    int at = Math.Min(Math.Max(_index, 0), entity.parameters.Count);
                    entity.parameters.Insert(at, _parameter);
                    live = _parameter;
                }
                if (_modified)
                    ParameterModificationTracker.SetParameterModified(_composite, _entity, live.name);
                ParameterValueEdit.Notify(entity, live);
            }
            else if (live != null)
            {
                //Removed is announced before the removal, as the inspector does: the viewer packs the
                //parameter it is losing
                Singleton.OnEntityParameterModified?.Invoke(entity, live, true);
                if (live.name == ShortGuids.position && live.content is cTransform)
                    Singleton.OnEntityMoved?.Invoke(null, entity);
                entity.parameters.Remove(live);
                ParameterModificationTracker.ClearParameterModified(_composite, _entity, live.name);
                Singleton.OnParameterModified?.Invoke();
            }

            context.Ui?.ReloadEntity(entity);
        }

        public bool TryMerge(IEdit next) => false;
    }
}
