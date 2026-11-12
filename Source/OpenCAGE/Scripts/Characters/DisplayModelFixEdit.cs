using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.Characters
{
    /// <summary>
    /// One undo step putting a display model in the shipped shape (<see cref="DisplayModelShape"/>): its id to the hash of its name (when
    /// that id is free), its skeleton entity to the composite's id with the shipped resource id, an animated-model entry of its own when it
    /// shares one, and that entry's MeshMappings to exactly its parts. Worked out on the first apply; redo replays the same changes, undo
    /// puts every one back (the entry it added leaves the level's table again).
    /// </summary>
    internal sealed class DisplayModelFixEdit : IEdit, IChangesComposites
    {
        private readonly Composite _composite;
        private readonly bool _fixId, _fixEntry, _fixMappings, _fixSkeleton;
        private bool _planned;

        private ShortGuid _idBefore, _idAfter;
        private FunctionEntity _skeleton;
        private ShortGuid _skeletonIdBefore;
        private cResource _resource;
        private ShortGuid _resourceIdBefore;
        private Dictionary<ResourceReference, ShortGuid> _referenceIdsBefore = new Dictionary<ResourceReference, ShortGuid>();
        private ResourceReference _animated;
        private EnvironmentAnimations.EnvironmentAnimation _entryBefore, _entryAfter;
        private bool _entryAdded;
        private List<ShortGuid> _mappingsBefore, _mappingsAfter;

        public string Label { get; }
        public ShortGuid CompositeId => _composite.shortGUID;
        public ShortGuid EntityId => ShortGuid.Invalid;
        public IEnumerable<ShortGuid> ChangedComposites => new[] { _composite.shortGUID };
        /// <summary>What the step changed, for a report (filled by the first apply).</summary>
        public List<string> Changes { get; } = new List<string>();

        public DisplayModelFixEdit(Composite composite, string label, bool id = true, bool skeleton = true, bool entry = true, bool mappings = true)
        {
            _composite = composite;
            Label = label;
            _fixId = id; _fixSkeleton = skeleton; _fixEntry = entry; _fixMappings = mappings;
        }

        private static Level Level(UndoContext context) => context?.Content?.Level ?? Singleton.Editor?.CompositeBrowser?.Content?.Level;

        public void Apply(UndoContext context)
        {
            Level level = Level(context);
            Commands commands = level?.Commands;
            if (commands == null) return;
            if (!_planned) Plan(commands);

            if (_idAfter != _idBefore && _composite.shortGUID == _idBefore && commands.GetComposite(_idAfter) == null)
                DisplayModelShape.ChangeCompositeId(commands, _composite, _idAfter);
            if (_skeleton != null && _skeleton.shortGUID != _composite.shortGUID && _composite.GetEntityByID(_composite.shortGUID) == null)
                DisplayModelShape.ChangeEntityId(_composite, _skeleton, _composite.shortGUID);
            if (_resource != null && _resourceIdBefore != DisplayModelShape.AnimatedModelResourceId)
            {
                _resource.shortGUID = DisplayModelShape.AnimatedModelResourceId;
                foreach (ResourceReference reference in _referenceIdsBefore.Keys) reference.resource_id = DisplayModelShape.AnimatedModelResourceId;
            }
            if (_animated != null && _entryAfter != null && !ReferenceEquals(_entryAfter, _entryBefore))
            {
                if (_entryAdded) DisplayModelShape.AddEntry(level.EnvironmentAnimations, _entryAfter);
                _animated.AnimatedModel = _entryAfter;
            }
            if (_mappingsTarget != null && _mappingsAfter != null) _mappingsTarget.MeshMappings = new List<ShortGuid>(_mappingsAfter);
            Announce(context);
        }

        public void Revert(UndoContext context)
        {
            Level level = Level(context);
            Commands commands = level?.Commands;
            if (commands == null || !_planned) return;
            if (_mappingsTarget != null && _mappingsAfter != null) _mappingsTarget.MeshMappings = _mappingsBefore == null ? null : new List<ShortGuid>(_mappingsBefore);
            if (_animated != null && _entryAfter != null && !ReferenceEquals(_entryAfter, _entryBefore))
            {
                _animated.AnimatedModel = _entryBefore;
                if (_entryAdded) level.EnvironmentAnimations.Entries.RemoveAll(o => ReferenceEquals(o, _entryAfter));
            }
            if (_resource != null && _resourceIdBefore != DisplayModelShape.AnimatedModelResourceId)
            {
                _resource.shortGUID = _resourceIdBefore;
                foreach (KeyValuePair<ResourceReference, ShortGuid> reference in _referenceIdsBefore) reference.Key.resource_id = reference.Value;
            }
            if (_composite.shortGUID != _idBefore && commands.GetComposite(_idBefore) == null)
                DisplayModelShape.ChangeCompositeId(commands, _composite, _idBefore);
            if (_skeleton != null && _skeleton.shortGUID != _skeletonIdBefore && _composite.GetEntityByID(_skeletonIdBefore) == null)
                DisplayModelShape.ChangeEntityId(_composite, _skeleton, _skeletonIdBefore);
            Announce(context);
        }

        //which entry the mappings were changed on (the shared one when no own entry was made)
        private EnvironmentAnimations.EnvironmentAnimation _mappingsTarget;

        private void Plan(Commands commands)
        {
            _planned = true;
            _idBefore = _composite.shortGUID;
            _idAfter = _idBefore;
            DisplayModelShape.Report report = DisplayModelShape.Inspect(commands, _composite);
            if (_fixId && report.IdWrong)
            {
                ShortGuid want = DisplayModelShape.IdFor(_composite.name);
                if (commands.GetComposite(want) == null) { _idAfter = want; Changes.Add("composite id " + _idBefore.ToByteString() + " -> " + want.ToByteString()); }
                else Changes.Add("the id " + want.ToByteString() + " its name needs is taken by " + commands.GetComposite(want).name + ": left as it is");
            }
            _skeleton = DisplayModelShape.SkeletonEntity(_composite);
            _skeletonIdBefore = _skeleton?.shortGUID ?? ShortGuid.Invalid;
            if (_skeleton != null && _fixSkeleton && _skeletonIdBefore != _idAfter)
                Changes.Add("skeleton entity " + _skeletonIdBefore.ToByteString() + " -> " + _idAfter.ToByteString());
            _resource = _fixSkeleton ? _skeleton?.GetParameter(ShortGuids.resource)?.content as cResource : null;
            _resourceIdBefore = _resource?.shortGUID ?? DisplayModelShape.AnimatedModelResourceId;
            if (_resource != null)
            {
                foreach (ResourceReference reference in _resource.value.Where(r => r.resource_type == ResourceType.ANIMATED_MODEL)) _referenceIdsBefore[reference] = reference.resource_id;
                if (_resourceIdBefore != DisplayModelShape.AnimatedModelResourceId) Changes.Add("animated-model resource id " + _resourceIdBefore.ToByteString() + " -> 00-E4-14-34");
            }
            if (!_fixSkeleton) _skeletonIdBefore = _skeleton?.shortGUID ?? ShortGuid.Invalid;
            _animated = DisplayModelShape.AnimatedModelReference(_skeleton);
            _entryBefore = _animated?.AnimatedModel;
            _entryAfter = _entryBefore;
            if (_fixEntry && _entryBefore != null && report.EntryShared)
            {
                _entryAfter = DisplayModelShape.OwnEntry(_entryBefore, _composite);
                _entryAdded = true;
                Changes.Add("own animated-model entry (was sharing entry " + _entryBefore.ID + " with " + string.Join(", ", DisplayModelShape.Users(commands, _entryBefore).Where(o => o != _composite).Select(o => o.name)) + ")");
            }
            _mappingsTarget = _entryAfter;
            if (_entryAfter != null)
            {
                _mappingsBefore = _entryAfter.MeshMappings == null ? null : new List<ShortGuid>(_entryAfter.MeshMappings);
                List<ShortGuid> parts = DisplayModelShape.Parts(_composite).Select(o => o.Renderable.resource_id).Distinct().ToList();
                if (_fixMappings && (_mappingsBefore == null || !new HashSet<ShortGuid>(_mappingsBefore).SetEquals(parts)))
                {
                    //the parts already listed keep their order (retail lists them in the composite's order); new ones go at the end
                    List<ShortGuid> after = (_mappingsBefore ?? new List<ShortGuid>()).Where(parts.Contains).ToList();
                    after.AddRange(parts.Where(o => !after.Contains(o)));
                    _mappingsAfter = after;
                    Changes.Add("parts listed in the animated model: " + (_mappingsBefore?.Count ?? 0) + " -> " + after.Count);
                }
            }
            if (!_fixSkeleton) _skeleton = null;
        }

        private void Announce(UndoContext context)
        {
            try
            {
                context?.Ui?.CompositesRenamed(new List<Composite>() { _composite });
                Singleton.OnResourceModified?.Invoke();
                CompositePreviewManager.MarkEdited(_composite);
            }
            catch { }
        }

        public bool TryMerge(IEdit next) => false;
    }
}
