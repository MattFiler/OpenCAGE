using CATHODE.Scripting;
using System;
using System.Collections.Generic;

namespace OpenCAGE.Undo
{
    /// <summary>
    /// The editor's undo history. Every user edit either goes through <see cref="Apply"/> (the stack
    /// performs it) or is performed by the caller and then handed in with <see cref="Record"/>.
    /// Nothing is recorded while the stack is applying an edit itself, or inside a
    /// <see cref="Suspend"/> scope, which is how the editor's own housekeeping - the link compile when
    /// a composite is left, the viewer letting go of a deep-select alias - stays out of the history.
    /// </summary>
    public sealed class UndoStack
    {
        public static readonly UndoStack Current = new UndoStack();

        private readonly List<IEdit> _undo = new List<IEdit>();
        private readonly List<IEdit> _redo = new List<IEdit>();
        private int _suspendDepth = 0;
        private int _applyDepth = 0;
        private Group _group = null;
        private int _groupDepth = 0;

        /// <summary>Oldest edits drop off past this.</summary>
        public int MaxEdits = 200;

        /// <summary>Set while a save pumps the message loop: undo must not run underneath it.</summary>
        public bool Blocked = false;

        /// <summary>Supplies the level and UI an edit runs against. Set by the main window.</summary>
        public Func<UndoContext> ContextFactory;

        /// <summary>The history changed: labels, availability. May be raised off the UI thread on level load.</summary>
        public event Action Changed;

        /// <summary>A line for the status bar: "Undid Move Door_1".</summary>
        public event Action<string> Status;

        public bool IsApplying => _applyDepth > 0;
        public bool IsSuspended => _suspendDepth > 0 || _applyDepth > 0;
        public bool CanUndo => !Blocked && _applyDepth == 0 && _groupDepth == 0 && _undo.Count > 0;
        public bool CanRedo => !Blocked && _applyDepth == 0 && _groupDepth == 0 && _redo.Count > 0;
        public string UndoLabel => _undo.Count > 0 ? _undo[_undo.Count - 1].Label : null;
        public string RedoLabel => _redo.Count > 0 ? _redo[_redo.Count - 1].Label : null;

        /// <summary>Changes made inside the scope are not recorded, and the redo history is left alone.</summary>
        public IDisposable Suspend()
        {
            _suspendDepth++;
            return new Scope(() => _suspendDepth--);
        }

        /// <summary>
        /// Everything recorded inside the scope becomes one step. Scopes nest and flatten; a scope that
        /// records nothing leaves no trace. A null label takes the label of the last edit in the group.
        /// </summary>
        public IDisposable BeginGroup(string label)
        {
            if (_groupDepth++ == 0)
                _group = new Group(label);
            else if (_group.Label == null && label != null)
                _group.Label = label;
            return new Scope(EndGroup);
        }

        private void EndGroup()
        {
            if (--_groupDepth > 0)
                return;

            Group group = _group;
            _group = null;
            if (group == null || group.Count == 0)
                return;

            //A lone edit in an unnamed scope is just that edit, merge and all
            if (group.Count == 1 && group.Label == null)
                Push(group.Single, true);
            else
                Push(group, false);
        }

        /// <summary>Perform the edit and remember it.</summary>
        public void Apply(IEdit edit)
        {
            if (edit == null)
                return;

            UndoContext context = MakeContext();
            _applyDepth++;
            try
            {
                edit.Apply(context);
            }
            finally
            {
                _applyDepth--;
            }
            Push(edit, true);
        }

        /// <summary>Remember an edit the caller has already performed.</summary>
        public void Record(IEdit edit)
        {
            if (edit == null)
                return;
            Push(edit, true);
        }

        private void Push(IEdit edit, bool allowMerge)
        {
            if (IsSuspended)
                return;

            if (_group != null)
            {
                _group.Add(edit);
                return;
            }

            if (allowMerge && _undo.Count > 0 && _undo[_undo.Count - 1].TryMerge(edit))
            {
                Changed?.Invoke();
                return;
            }

            _undo.Add(edit);
            _redo.Clear();
            while (_undo.Count > MaxEdits)
                _undo.RemoveAt(0);
            Changed?.Invoke();
        }

        public void Undo() => Step(_undo, _redo, true);
        public void Redo() => Step(_redo, _undo, false);

        private void Step(List<IEdit> from, List<IEdit> to, bool undo)
        {
            if (Blocked || _applyDepth > 0 || _groupDepth > 0 || from.Count == 0)
                return;

            IEdit edit = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);

            UndoContext context = MakeContext();
            _applyDepth++;
            try
            {
                context.Ui?.BeforeEdit(edit);
                if (undo)
                    edit.Revert(context);
                else
                    edit.Apply(context);
                context.Ui?.AfterEdit(edit);
                to.Add(edit);
                Status?.Invoke((undo ? "Undid " : "Redid ") + edit.Label);
            }
            catch (Exception ex)
            {
                //The data may be part-way between two states now, and every later edit was recorded
                //against the state this one should have produced - so none of the history can be trusted
                Debug.Log("Undo", (undo ? "Undo" : "Redo") + " of '" + edit.Label + "' failed: " + ex);
                _undo.Clear();
                _redo.Clear();
                Status?.Invoke("Could not " + (undo ? "undo " : "redo ") + edit.Label + " - the history has been cleared");
            }
            finally
            {
                _applyDepth--;
            }
            Changed?.Invoke();
        }

        /// <summary>Forget everything: a different level is loading.</summary>
        public void Clear()
        {
            if (_undo.Count == 0 && _redo.Count == 0)
                return;
            _undo.Clear();
            _redo.Clear();
            Changed?.Invoke();
        }

        private UndoContext MakeContext()
        {
            return ContextFactory?.Invoke() ?? new UndoContext(null, null);
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

        /// <summary>Several edits that undo and redo as one.</summary>
        private sealed class Group : IEdit
        {
            private readonly List<IEdit> _edits = new List<IEdit>();
            public string Label;

            public Group(string label) { Label = label; }

            public int Count => _edits.Count;
            public IEdit Single => _edits[0];
            public void Add(IEdit edit) => _edits.Add(edit);

            string IEdit.Label => Label ?? (_edits.Count > 0 ? _edits[_edits.Count - 1].Label : "");
            public ShortGuid CompositeId => _edits.Count > 0 ? _edits[0].CompositeId : ShortGuid.Invalid;
            public ShortGuid EntityId
            {
                get
                {
                    foreach (IEdit edit in _edits)
                        if (!edit.EntityId.IsInvalid)
                            return edit.EntityId;
                    return ShortGuid.Invalid;
                }
            }

            public void Apply(UndoContext context)
            {
                for (int i = 0; i < _edits.Count; i++)
                    _edits[i].Apply(context);
            }
            public void Revert(UndoContext context)
            {
                for (int i = _edits.Count - 1; i >= 0; i--)
                    _edits[i].Revert(context);
            }
            public bool TryMerge(IEdit next) => false;
        }
    }
}
