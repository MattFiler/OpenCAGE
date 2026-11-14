using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.Undo
{
    /// <summary>
    /// The user changed what is selected in a composite: undo selects what was selected before, redo selects it again.
    /// Entities are kept by id, so any deleted since (a viewport deep-select alias let go of, say) are left out; nothing is
    /// made again to be selected.
    /// </summary>
    public sealed class SelectionEdit : IEdit, IChangesComposites, ISelectionStep
    {
        private readonly ShortGuid _composite;
        private readonly List<ShortGuid> _before;
        private readonly string _beforeName;
        private List<ShortGuid> _after;
        private string _afterName;
        private readonly object _input;

        /// <param name="input">What the user did to make the change (a click, a key, a viewport pick): a later piece of the same input joins this step.</param>
        public SelectionEdit(Composite composite, List<Entity> before, List<Entity> after, object input)
        {
            _composite = composite.shortGUID;
            _before = before.Select(o => o.shortGUID).ToList();
            _after = after.Select(o => o.shortGUID).ToList();
            _beforeName = before.Count == 1 ? Describe(composite, before[0]) : null;
            _afterName = after.Count == 1 ? Describe(composite, after[0]) : null;
            _input = input;
        }

        //An alias (a viewport deep-select's, say) is called by what it reaches, as the entity list calls it
        private static string Describe(Composite composite, Entity entity)
        {
            if (entity is AliasEntity alias)
            {
                try
                {
                    Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
                    string reached = commands?.Utils?.GetResolvedAsString(commands.Utils.ResolveAlias(alias, composite), false);
                    if (!string.IsNullOrEmpty(reached))
                        return reached;
                }
                catch
                {
                    //A name is a nicety; the id will do
                }
            }
            return UndoLabels.Entity(composite, entity);
        }

        public IReadOnlyList<ShortGuid> Before => _before;
        public IReadOnlyList<ShortGuid> After => _after;

        public string Label
        {
            get
            {
                if (_after.Count == 0)
                    return _before.Count == 1 ? "Deselect " + _beforeName : "Deselect all";
                if (_after.Count == 1)
                    return "Select " + _afterName;
                return "Select " + _after.Count + " entities";
            }
        }

        public ShortGuid CompositeId => _composite;
        public ShortGuid EntityId => ShortGuid.Invalid;
        public IEnumerable<ShortGuid> ChangedComposites => Enumerable.Empty<ShortGuid>();
        public bool ChangesNothing => SameSelection(_before, _after);

        public void Apply(UndoContext context) => context.Ui?.Select(_composite, _after);
        public void Revert(UndoContext context) => context.Ui?.Select(_composite, _before);

        public bool TryMerge(IEdit next)
        {
            SelectionEdit piece = next as SelectionEdit;
            if (piece == null || piece._composite != _composite || _input == null || !Equals(_input, piece._input))
                return false;

            _after = piece._after;
            _afterName = piece._afterName;
            return true;
        }

        internal static bool SameSelection(IReadOnlyCollection<ShortGuid> a, IReadOnlyCollection<ShortGuid> b)
        {
            return a.Count == b.Count && !a.Except(b).Any();
        }
    }

    /// <summary>
    /// Turns the user's own selection changes into undo steps. Each place the user selects from - a click or key in the
    /// entity list, a click on a flowgraph page, a pick in the viewport - handles it inside <see cref="UserChange"/>.
    /// Anything else that moves the selection (an undo, a level load, opening or leaving a composite, an edit selecting
    /// what it made, a search or Go jumping to an entity) is never recorded.
    /// </summary>
    public static class SelectionUndo
    {
        private static int _depth = 0;
        private static object _input;
        private static ShortGuid _composite;
        private static List<Entity> _before;
        private static int _recordsBefore;

        /// <summary>Options > Entity Display > Undo Includes Selection Changes (on unless turned off).</summary>
        public static bool Enabled => SettingsManager.GetBool(Settings.UndoIncludesSelection, true);

        /// <summary>
        /// The selection changes made inside the scope are the user's: when they leave a different selection in the same
        /// composite, that is a step. Scopes nest, the outermost deciding.
        /// </summary>
        /// <param name="input">
        /// What the user did. Every piece of selection change one input makes (a rubber-band drag raises one per mouse move)
        /// passes an equal object, and they join into one step. Null when the change is not the user's: nothing is recorded.
        /// </param>
        public static IDisposable UserChange(object input)
        {
            if (input == null)
                return new Scope(null);

            if (_depth++ == 0)
            {
                _input = input;
                CompositeDisplay display = Display;
                _composite = display?.Composite?.shortGUID ?? ShortGuid.Invalid;
                _before = display?.CurrentSelection();
                _recordsBefore = UndoStack.Current.RecordCount;
            }
            return new Scope(End);
        }

        private static void End()
        {
            if (--_depth > 0)
                return;

            object input = _input;
            List<Entity> before = _before;
            _input = null;
            _before = null;
            if (before == null)
                return;

            UndoStack stack = UndoStack.Current;
            if (!Enabled)
            {
                stack.RemoveSelectionSteps();
                return;
            }

            //Under an undo, a suspension or an open group the selection is the system's doing; an edit made inside selects for itself
            if (stack.IsSuspended || stack.IsGrouping || stack.RecordCount != _recordsBefore)
                return;

            //Somewhere else now: that was navigation, which is not recorded
            CompositeDisplay display = Display;
            Composite composite = display?.Composite;
            if (composite == null || composite.shortGUID != _composite)
                return;

            List<Entity> after = display.CurrentSelection();
            if (SelectionEdit.SameSelection(before.Select(o => o.shortGUID).ToList(), after.Select(o => o.shortGUID).ToList()))
                return;

            stack.Record(new SelectionEdit(composite, before, after, input));
        }

        private static CompositeDisplay Display
        {
            get
            {
                CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
                return display == null || display.IsDisposed || !display.Populated ? null : display;
            }
        }

        private sealed class Scope : IDisposable
        {
            private Action _end;
            public Scope(Action end) { _end = end; }
            public void Dispose()
            {
                Action end = _end;
                _end = null;
                end?.Invoke();
            }
        }
    }
}
