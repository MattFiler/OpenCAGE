using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.DockPanels;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Putting entities into a TriggerSequence and taking them out again without opening its editor: the
    /// right-click actions on a sequence, and adding whatever is placed while a sequence is set to take it.
    /// </summary>
    /// <remarks>
    /// Every change is a <see cref="TriggerSequenceEdit"/>, the step the editor records for a session, so
    /// it undoes and redoes the same way and the viewport's zones follow it.
    ///
    /// An entry is a path from the sequence's own composite to the entity, and the sequence need not be in
    /// the composite the entity is in: a level's zone sequences sit in its ENVIRONMENT composite while the
    /// geometry they name is in the composites placed inside it. So the path is the steps the display took
    /// down from the sequence's composite to the one on screen, then the entity - there is one whenever the
    /// display stands in the sequence's composite, or anywhere it has stepped down to through it.
    /// </remarks>
    public static class TriggerSequenceMembers
    {
        /// <summary>A sequence, and the composite it is in.</summary>
        public sealed class Target
        {
            public Entity Sequence;
            public Composite Composite;

            public bool Is(Entity sequence) => sequence != null && Sequence.shortGUID == sequence.shortGUID
                && Composite.GetEntityByID(sequence.shortGUID) == sequence;
        }

        private static Commands Commands => Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;

        /// <summary>
        /// The sequence as it stands in the composite open on the display, or null when the entity is
        /// not a TriggerSequence (or a proxy to one, which carries a sequence of its own) in it.
        /// </summary>
        public static Target At(CompositeDisplay display, Entity sequence)
        {
            Composite composite = display?.Composite;
            if (composite == null || sequence == null || composite.GetEntityByID(sequence.shortGUID) != sequence)
                return null;
            if (!TryGetLists(sequence, out _, out _))
                return null;

            return new Target() { Sequence = sequence, Composite = composite };
        }

        /// <summary>
        /// A TriggerSequence's own lists, or a proxy's - a proxy to a TriggerSequence carries a sequence of
        /// its own, which the Function editor edits the same way.
        /// </summary>
        private static bool TryGetLists(Entity entity, out List<TriggerSequence.SequenceEntry> sequence, out List<TriggerSequence.MethodEntry> methods)
        {
            sequence = null;
            methods = null;
            switch (entity)
            {
                case TriggerSequence triggerSequence:
                    sequence = triggerSequence.sequence;
                    methods = triggerSequence.methods;
                    return true;
                case ProxyEntity proxy:
                    Commands commands = Commands;
                    if (commands?.Utils == null)
                        return false;
                    if (!(commands.Utils.GetResolvedTarget(commands.Utils.ResolveProxy(proxy)).Item2 is TriggerSequence))
                        return false;
                    sequence = proxy.sequence;
                    methods = proxy.methods;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The steps the display took down from the target's composite to the composite it has open, or
        /// null when its path does not pass through there.
        /// </summary>
        /// <remarks>
        /// Any way down through the sequence's composite will do, not only the one it was found by: the
        /// entry is written from that composite, so how the display got there - from the level root, or
        /// by opening it on its own - makes no difference to it.
        /// </remarks>
        private static List<Entity> StepsBelow(Target target, CompositeDisplay display)
        {
            Composite open = display?.Composite;
            if (target == null || open == null)
                return null;
            if (open == target.Composite)
                return new List<Entity>();

            List<Composite> composites = display.Path.AllComposites;
            List<Entity> entities = display.Path.AllEntities;
            for (int i = Math.Min(composites.Count, entities.Count) - 1; i >= 0; i--)
            {
                if (composites[i] == target.Composite)
                    return entities.GetRange(i, entities.Count - i);
            }
            return null;
        }

        /// <summary>What can go in a sequence: a function or a composite instance, as the editor's picker offers.</summary>
        private static bool CanBeEntry(Target target, Entity entity)
        {
            return entity != null && entity.variant == EntityVariant.FUNCTION && !target.Is(entity);
        }

        /// <summary>An entity as it would be written into the sequence, and the two ways of saying where it is.</summary>
        private sealed class Candidate
        {
            public Entity Entity;
            public ShortGuid[] Entry;

            //From the sequence's composite; and from the level root, when the display's path starts there
            public List<uint> FromSequence;
            public List<uint> FromRoot;
        }

        private static List<Candidate> Candidates(Target target, CompositeDisplay display, IEnumerable<Entity> entities)
        {
            List<Candidate> candidates = new List<Candidate>();
            List<Entity> steps = StepsBelow(target, display);
            if (steps == null || entities == null)
                return candidates;

            Composite[] entryPoints = Commands?.EntryPoints;
            bool fromLevelRoot = entryPoints != null && entryPoints.Length != 0
                && (display.Path.AllComposites.FirstOrDefault() ?? display.Composite) == entryPoints[0];

            foreach (Entity entity in entities.Distinct())
            {
                if (!CanBeEntry(target, entity) || display.Composite.GetEntityByID(entity.shortGUID) != entity)
                    continue;

                List<ShortGuid> entry = steps.Select(o => o.shortGUID).ToList();
                entry.Add(entity.shortGUID);
                Candidate candidate = new Candidate()
                {
                    Entity = entity,
                    Entry = entry.ToArray(),
                    FromSequence = entry.Select(o => o.AsUInt32).ToList(),
                };
                if (fromLevelRoot)
                {
                    candidate.FromRoot = display.Path.AllEntities.Select(o => o.shortGUID.AsUInt32).ToList();
                    candidate.FromRoot.Add(entity.shortGUID.AsUInt32);
                }
                candidates.Add(candidate);
            }
            return candidates;
        }

        /// <summary>Where an entry in the sequence lands, read the way the viewport reads it to mark it.</summary>
        private sealed class Resolved
        {
            public List<uint> Path;

            //Written from the sequence's composite, rather than from the level root
            public bool FromSequence;

            public bool Names(Candidate candidate)
            {
                return FromSequence
                    ? EntityInstancePath.Equal(Path, candidate.FromSequence)
                    : candidate.FromRoot != null && EntityInstancePath.Equal(Path, candidate.FromRoot);
            }
        }

        /// <summary>
        /// Where each entry in the sequence lands. A stored path can be written from the sequence's
        /// composite or from the level root (see <see cref="EntityInstancePath.TryResolve"/>), and it is
        /// compared with an entity in the same terms. An entry naming nothing resolves to null.
        /// </summary>
        private static List<Resolved> ResolveEntries(Target target, List<TriggerSequence.SequenceEntry> sequence)
        {
            Commands commands = Commands;
            List<Resolved> resolved = new List<Resolved>(sequence.Count);
            foreach (TriggerSequence.SequenceEntry entry in sequence)
            {
                if (entry?.connectedEntity?.path != null
                    && EntityInstancePath.TryResolve(commands, target.Composite, null, entry.connectedEntity, out List<uint> path, out bool fromSequence))
                    resolved.Add(new Resolved() { Path = path, FromSequence = fromSequence });
                else
                    resolved.Add(null);
            }
            return resolved;
        }

        private static bool Contains(List<Resolved> resolved, Candidate candidate)
        {
            return resolved.Any(o => o != null && o.Names(candidate));
        }

        /// <summary>
        /// How many of these entities Add would put in the sequence, and how many Remove would take out.
        /// Both are 0 when the display is not where the sequence's entries can be written from.
        /// </summary>
        public static void Count(Target target, CompositeDisplay display, IEnumerable<Entity> entities, out int addable, out int removable)
        {
            addable = 0;
            removable = 0;
            if (target == null || !TryGetLists(target.Sequence, out List<TriggerSequence.SequenceEntry> sequence, out _))
                return;

            List<Candidate> candidates = Candidates(target, display, entities);
            if (candidates.Count == 0)
                return;

            List<Resolved> resolved = ResolveEntries(target, sequence);
            foreach (Candidate candidate in candidates)
            {
                if (Contains(resolved, candidate))
                    removable++;
                else
                    addable++;
            }
        }

        /// <summary>Put the entities that are not in the sequence yet on the end of it, as one step.</summary>
        /// <returns>How many went in.</returns>
        public static int Add(Target target, CompositeDisplay display, IEnumerable<Entity> entities, bool automatic = false)
        {
            List<Candidate> candidates = target == null ? null : Candidates(target, display, entities);
            if (candidates == null || candidates.Count == 0)
                return 0;
            CloseEditorsOn(target);
            if (!TryGetLists(target.Sequence, out List<TriggerSequence.SequenceEntry> sequence, out List<TriggerSequence.MethodEntry> methods))
                return 0;

            List<Resolved> resolved = ResolveEntries(target, sequence);
            List<TriggerSequence.SequenceEntry> after = TriggerSequenceEdit.CloneSequence(sequence);
            List<Entity> added = new List<Entity>();
            foreach (Candidate candidate in candidates)
            {
                if (Contains(resolved, candidate))
                    continue;
                after.Add(new TriggerSequence.SequenceEntry() { connectedEntity = new EntityPath(candidate.Entry) });
                added.Add(candidate.Entity);
            }
            if (added.Count == 0)
                return 0;

            string what = added.Count == 1 ? UndoLabels.Entity(display.Composite, added[0]) : UndoLabels.Count(added.Count, "entity", "entities");
            Apply(target, sequence, methods, after, (automatic ? "Auto-add " : "Add ") + what + " to " + UndoLabels.Entity(target.Composite, target.Sequence));
            return added.Count;
        }

        /// <summary>Take every entry naming one of the entities out of the sequence, as one step.</summary>
        /// <returns>How many of the entities were taken out.</returns>
        public static int Remove(Target target, CompositeDisplay display, IEnumerable<Entity> entities)
        {
            List<Candidate> candidates = target == null ? null : Candidates(target, display, entities);
            if (candidates == null || candidates.Count == 0)
                return 0;
            CloseEditorsOn(target);
            if (!TryGetLists(target.Sequence, out List<TriggerSequence.SequenceEntry> sequence, out List<TriggerSequence.MethodEntry> methods))
                return 0;

            List<Resolved> resolved = ResolveEntries(target, sequence);

            //The same entity at two timings is two entries, and both go
            List<TriggerSequence.SequenceEntry> kept = new List<TriggerSequence.SequenceEntry>();
            HashSet<Entity> removed = new HashSet<Entity>();
            for (int i = 0; i < sequence.Count; i++)
            {
                Candidate named = resolved[i] == null ? null : candidates.FirstOrDefault(o => resolved[i].Names(o));
                if (named == null)
                    kept.Add(sequence[i]);
                else
                    removed.Add(named.Entity);
            }
            if (removed.Count == 0)
                return 0;

            string what = removed.Count == 1 ? UndoLabels.Entity(display.Composite, removed.First()) : UndoLabels.Count(removed.Count, "entity", "entities");
            Apply(target, sequence, methods, TriggerSequenceEdit.CloneSequence(kept), "Remove " + what + " from " + UndoLabels.Entity(target.Composite, target.Sequence));
            return removed.Count;
        }

        /* An editor window open on the sequence is holding the lists the edit replaces, and edits them in
           place: it is closed first - which records what it had changed as a step of its own - and the
           lists are read after, so that change is not lost under this one. */
        private static void CloseEditorsOn(Target target)
        {
            foreach (TriggerSequenceEditor editor in Application.OpenForms.OfType<TriggerSequenceEditor>().ToList())
            {
                if (editor.Entity == target.Sequence)
                    editor.Close();
            }
        }

        private static void Apply(Target target, List<TriggerSequence.SequenceEntry> before, List<TriggerSequence.MethodEntry> methods,
            List<TriggerSequence.SequenceEntry> after, string label)
        {
            UndoStack.Current.Apply(new TriggerSequenceEdit(target.Composite, target.Sequence,
                TriggerSequenceEdit.CloneSequence(before), TriggerSequenceEdit.CloneMethods(methods),
                after, TriggerSequenceEdit.CloneMethods(methods), label));
        }

        #region AUTO-ADD

        /// <summary>The sequence new entities are added to as they are placed, or null.</summary>
        public static Target AutoAddTarget { get; private set; }

        public static bool IsAutoAddTarget(Entity sequence) => AutoAddTarget != null && AutoAddTarget.Is(sequence);

        /// <summary>
        /// Add everything placed from now on to this sequence - composite instances and the things the
        /// viewport can place (models, lights, fog spheres...) - whether it is placed in the sequence's
        /// composite or in one stepped down to from there. Null stops it.
        /// </summary>
        /// <remarks>
        /// Placed further down, it goes in with the steps from the sequence's composite to it, which is
        /// how a level's zone sequences name their geometry. Placed anywhere the display did not reach
        /// through the sequence's composite there is no such path, and it is left out.
        /// </remarks>
        public static void SetAutoAdd(Target target)
        {
            if (AutoAddTarget == null && target != null)
            {
                Singleton.OnEntityAdded += AutoAddEntity;
                Singleton.OnEntityDeleted += StopIfSequenceDeleted;
                Singleton.OnCompositeDeleted += StopIfCompositeDeleted;
                Singleton.OnLevelLoaded += StopOnLevelLoaded;
            }
            else if (AutoAddTarget != null && target == null)
            {
                Singleton.OnEntityAdded -= AutoAddEntity;
                Singleton.OnEntityDeleted -= StopIfSequenceDeleted;
                Singleton.OnCompositeDeleted -= StopIfCompositeDeleted;
                Singleton.OnLevelLoaded -= StopOnLevelLoaded;
            }
            AutoAddTarget = target;
        }

        private static void AutoAddEntity(Entity entity)
        {
            Target target = AutoAddTarget;
            if (target == null || entity == null)
                return;

            //An undo or redo bringing an entity back brings back the entry it had too, as its own step
            if (UndoStack.Current.IsSuspended)
                return;

            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            if (display?.Composite == null || display.Composite.GetEntityByID(entity.shortGUID) != entity)
                return;
            if (!IsPlaced(entity))
                return;

            Add(target, display, new[] { entity }, automatic: true);
        }

        /* The things a zone is made of: composite instances, and what the viewport's Create menu places */
        private static bool IsPlaced(Entity entity)
        {
            if (!(entity is FunctionEntity function))
                return false;
            if (function.function.IsFunctionType)
                return CompositeDisplay.IsInSceneEntity(entity);
            return Commands?.GetComposite(function.function) != null;
        }

        private static void StopIfSequenceDeleted(Entity entity)
        {
            if (AutoAddTarget != null && entity != null && entity.shortGUID == AutoAddTarget.Sequence.shortGUID
                && AutoAddTarget.Composite.GetEntityByID(entity.shortGUID) == null)
                SetAutoAdd(null);
        }

        private static void StopIfCompositeDeleted(Composite composite)
        {
            if (AutoAddTarget != null && composite == AutoAddTarget.Composite)
                SetAutoAdd(null);
        }

        private static void StopOnLevelLoaded(LevelContent content)
        {
            SetAutoAdd(null);
        }

        #endregion

        #region MENUS

        /// <summary>What a right-click menu's TriggerSequence actions act on, worked out as it opens.</summary>
        public sealed class MenuChoice
        {
            public Target Target;
            public List<Entity> Entities;

            //The sequence was what was right-clicked, rather than the one being auto-added to
            internal bool Clicked;
        }

        /// <summary>
        /// The sequence a right-click's actions are for: the TriggerSequence clicked on - or, clicking
        /// anything else, the one new entities are being added to, while the display is where its entries
        /// can be written from. That second is what lets a sequence up the hierarchy (a level's zones, in
        /// its ENVIRONMENT composite) take entities from the composites placed below it.
        /// </summary>
        public static MenuChoice ChooseForMenu(CompositeDisplay display, Entity clicked, List<Entity> entities)
        {
            Target target = At(display, clicked);
            if (target != null)
                return new MenuChoice() { Target = target, Entities = entities, Clicked = true };

            if (clicked != null && AutoAddTarget != null && StepsBelow(AutoAddTarget, display) != null)
                return new MenuChoice() { Target = AutoAddTarget, Entities = entities };

            return null;
        }

        /// <summary>Show, name and enable a menu's items for the choice - or hide them when there is none.</summary>
        public static void ConfigureMenuItems(MenuChoice choice, CompositeDisplay display, ToolStripItem separator,
            ToolStripMenuItem add, ToolStripMenuItem remove, ToolStripMenuItem autoAdd)
        {
            bool show = choice != null;
            separator.Visible = show;
            add.Visible = show;
            remove.Visible = show;
            autoAdd.Visible = show;
            if (!show)
                return;

            //The one clicked is "TriggerSequence"; one being added to from elsewhere is named, as it is not on screen
            string name = choice.Clicked ? "TriggerSequence" : UndoLabels.Entity(choice.Target.Composite, choice.Target.Sequence);
            Count(choice.Target, display, choice.Entities, out int addable, out int removable);
            add.Enabled = addable != 0;
            add.Text = "Add " + (addable > 1 ? addable + " " : "") + "Selected To " + name;
            remove.Enabled = removable != 0;
            remove.Text = "Remove " + (removable > 1 ? removable + " " : "") + "Selected From " + name;
            autoAdd.Text = "Auto-add New Entities To " + name;
            autoAdd.Checked = IsAutoAddTarget(choice.Target.Sequence);
        }

        public static void ToggleAutoAdd(MenuChoice choice)
        {
            if (choice?.Target == null)
                return;
            SetAutoAdd(IsAutoAddTarget(choice.Target.Sequence) ? null : choice.Target);
        }

        #endregion
    }
}
