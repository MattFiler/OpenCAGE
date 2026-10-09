using CATHODE.Scripting;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
        /// <summary>A group is open: whatever is recorded now joins it rather than becoming a step of its own.</summary>
        public bool IsGrouping => _groupDepth > 0;
        public bool IsSuspended => _suspendDepth > 0 || _applyDepth > 0;
        public bool CanUndo => !Blocked && _applyDepth == 0 && _groupDepth == 0 && _undo.Count > 0;
        public bool CanRedo => !Blocked && _applyDepth == 0 && _groupDepth == 0 && _redo.Count > 0;
        public string UndoLabel => _undo.Count > 0 ? _undo[_undo.Count - 1].Label : null;
        public string RedoLabel => _redo.Count > 0 ? _redo[_redo.Count - 1].Label : null;
        public int UndoCount => _undo.Count;
        public int RedoCount => _redo.Count;

        /// <summary>
        /// Who is making the edits recorded now, when it is not the user at the keyboard (an AI assistant's tool, say).
        /// Each new step is stamped with it for <see cref="History"/>; null (or a null answer) is the user.
        /// </summary>
        public Func<string> OriginProvider;

        /// <summary>Goes up whenever a step is recorded, merged into the latest one, or the steps are regrouped. Any thread may read it.</summary>
        public int RecordCount => System.Threading.Volatile.Read(ref _recordCount);
        private int _recordCount = 0;

        //Bumped when the history is wiped, so a mark taken before can tell
        private int _generation = 0;

        //Numbers each step as it is recorded, so a mark can tell the steps recorded after it from those before
        private long _stampCount = 0;

        private sealed class StepInfo
        {
            public string Origin;
            public DateTime Time;
            public long Number;
        }
        private readonly ConditionalWeakTable<IEdit, StepInfo> _stepInfo = new ConditionalWeakTable<IEdit, StepInfo>();

        /// <summary>One step of the history, as <see cref="History"/> lists it.</summary>
        public sealed class HistoryEntry
        {
            public string Label;
            /// <summary>Who made it (<see cref="OriginProvider"/>); null for the user.</summary>
            public string Origin;
            /// <summary>When it was recorded (local time).</summary>
            public DateTime? Time;
        }

        /// <summary>The steps undo (or redo) would take, the next one first, at most <paramref name="max"/>.</summary>
        public List<HistoryEntry> History(bool redo, int max)
        {
            List<IEdit> list = redo ? _redo : _undo;
            List<HistoryEntry> entries = new List<HistoryEntry>();
            for (int i = list.Count - 1; i >= 0 && entries.Count < max; i--)
            {
                StepInfo info = _stepInfo.TryGetValue(list[i], out StepInfo found) ? found : null;
                entries.Add(new HistoryEntry() { Label = list[i].Label, Origin = info?.Origin, Time = info?.Time });
            }
            return entries;
        }

        private sealed class HistoryMark
        {
            public IEdit Top;
            public int Generation;
            public long Stamped;
        }

        /// <summary>A mark on the history as it stands now: <see cref="StepsSince"/> counts the steps recorded after it.</summary>
        public object Mark() => new HistoryMark() { Top = _undo.Count > 0 ? _undo[_undo.Count - 1] : null, Generation = _generation, Stamped = _stampCount };

        /// <summary>
        /// How many steps there are above a <see cref="Mark"/>; -1 when the history has been cleared since. When the marked
        /// step itself has gone (undone, or dropped off the end), the steps recorded after the mark that are on top still count.
        /// </summary>
        public int StepsSince(object mark)
        {
            if (!(mark is HistoryMark at) || at.Generation != _generation)
                return -1;
            if (at.Top == null)
                return _undo.Count;
            int index = _undo.LastIndexOf(at.Top);
            if (index >= 0)
                return _undo.Count - 1 - index;
            //What was below the mark can only have gone by an undo (or off the end): the run on top recorded since is what came after it
            int since = 0;
            for (int i = _undo.Count - 1; i >= 0; i--)
            {
                if (!_stepInfo.TryGetValue(_undo[i], out StepInfo info) || info.Number <= at.Stamped)
                    break;
                since++;
            }
            return since;
        }

        /// <summary>
        /// Whether the step on top at a <see cref="Mark"/> is still on the undo side of the history - itself, or inside a step
        /// it has been grouped into since. False once it is undone, dropped off the end or cleared. UI thread.
        /// </summary>
        public bool StillHas(object mark)
        {
            if (!(mark is HistoryMark at) || at.Generation != _generation)
                return false;
            if (at.Top == null)
                return true;
            foreach (IEdit edit in _undo)
                if (edit == at.Top || (edit is Group group && group.Contains(at.Top)))
                    return true;
            return false;
        }

        /// <summary>
        /// Make the latest <paramref name="count"/> steps one step, called <paramref name="label"/>: they undo and redo
        /// together. Refused (false) while a group is open or an edit is applying, or if there are not that many steps.
        /// </summary>
        public bool Collapse(int count, string label)
        {
            if (count < 1 || count > _undo.Count || Blocked || _applyDepth > 0 || _groupDepth > 0)
                return false;
            int first = _undo.Count - count;
            Group group = new Group(label, null);
            for (int i = first; i < _undo.Count; i++)
                group.Add(_undo[i]);
            StepInfo latest = _stepInfo.TryGetValue(_undo[_undo.Count - 1], out StepInfo found) ? found : null;
            _undo.RemoveRange(first, count);
            _undo.Add(group);
            if (latest != null)
                _stepInfo.Add(group, new StepInfo() { Origin = latest.Origin, Time = latest.Time, Number = latest.Number });
            _recordCount++;
            Changed?.Invoke();
            return true;
        }

        private void Stamp(IEdit edit)
        {
            string origin = null;
            try { origin = OriginProvider?.Invoke(); }
            catch { }
            _stepInfo.Remove(edit);
            _stepInfo.Add(edit, new StepInfo() { Origin = origin, Time = DateTime.Now, Number = ++_stampCount });
        }

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
        /// <param name="gesture">
        /// Set when the scope is one piece of a gesture that arrives in several - a viewport drag of five
        /// entities is five packets. The step joins the latest step when that one was made under an equal
        /// key, so the gesture undoes as one however many pieces it came in, and whatever else happens
        /// in between breaks the run. A piece with a label renames the step; one without leaves its name.
        /// Undo and redo select every entity the gesture touched.
        /// </param>
        public IDisposable BeginGroup(string label, object gesture = null)
        {
            if (_groupDepth++ == 0)
                _group = new Group(label, gesture);
            else
            {
                if (_group.Label == null && label != null)
                    _group.Label = label;
                if (_group.Gesture == null && gesture != null)
                    _group.Gesture = gesture;
            }
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

            //A lone edit in an unnamed scope is just that edit, merge and all - unless it is a piece of a
            //gesture, which stays a group for the rest of the gesture to join
            if (group.Count == 1 && group.Label == null && group.Gesture == null)
                Push(group.Single, true);
            else
                Push(group, group.Gesture != null);
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
                _recordCount++;
                Changed?.Invoke();
                return;
            }

            _undo.Add(edit);
            Stamp(edit);
            _recordCount++;
            _redo.Clear();
            while (_undo.Count > MaxEdits)
                _undo.RemoveAt(0);
            Changed?.Invoke();
        }

        public void Undo() => Step(_undo, _redo, true);
        public void Redo() => Step(_redo, _undo, false);

        /// <summary>
        /// A gesture was called off after it made a step of its own (a viewport shift-clone's copies, then
        /// Escape before the drag was let go): take that step back and forget it - it was never finished, so
        /// there is nothing to redo. Only when the latest step is that gesture's (<see cref="BeginGroup"/>'s
        /// key); anything else - the gesture made no step, or something has been done since - is left alone.
        /// </summary>
        /// <returns>Whether the step was taken back.</returns>
        public bool CancelGesture(object gesture)
        {
            if (gesture == null || !CanUndo)
                return false;

            Group group = _undo[_undo.Count - 1] as Group;
            if (group == null || !Equals(group.Gesture, gesture))
                return false;

            Undo();
            if (_redo.Count == 0 || _redo[_redo.Count - 1] != group)
                return false; //the undo failed, and cleared the history

            _redo.RemoveAt(_redo.Count - 1);
            Changed?.Invoke();
            return true;
        }

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
                _generation++;
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
            _generation++;
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
        private sealed class Group : IEdit, IMultiEntityEdit
        {
            private readonly List<IEdit> _edits = new List<IEdit>();
            public string Label;

            /// <summary>Set on a piece of a gesture: a later piece under an equal key joins this step.</summary>
            public object Gesture;

            public Group(string label, object gesture)
            {
                Label = label;
                Gesture = gesture;
            }

            public int Count => _edits.Count;
            public IEdit Single => _edits[0];
            public void Add(IEdit edit) => _edits.Add(edit);
            /// <summary>Whether the edit is one of this group's, at any depth.</summary>
            public bool Contains(IEdit edit)
            {
                foreach (IEdit own in _edits)
                    if (own == edit || (own is Group inner && inner.Contains(edit)))
                        return true;
                return false;
            }

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
            public bool TryMerge(IEdit next)
            {
                Group piece = next as Group;
                if (Gesture == null || piece == null || !Equals(Gesture, piece.Gesture))
                    return false;

                _edits.AddRange(piece._edits);
                if (piece.Label != null)
                    Label = piece.Label;
                return true;
            }

            /* Only a gesture's step selects everything it touched; any other group keeps selecting its
               first entity, as it always has */
            public IReadOnlyList<ShortGuid> EntityIds
            {
                get
                {
                    List<ShortGuid> ids = new List<ShortGuid>();
                    if (Gesture == null)
                        return ids;

                    ShortGuid composite = CompositeId;
                    foreach (IEdit edit in _edits)
                    {
                        if (edit.CompositeId == composite && !edit.EntityId.IsInvalid && !ids.Contains(edit.EntityId))
                            ids.Add(edit.EntityId);
                    }
                    return ids;
                }
            }
        }
    }
}
