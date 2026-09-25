using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.Scripting.Refactor;
using CathodeLib;
using CathodeLib.ObjectExtensions;
using OpenCAGE.DockPanels;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Linq;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE.Undo
{
    /// <summary>
    /// A De-instance or Create Composite: one step that can change many composites at once.
    /// </summary>
    /// <remarks>
    /// The script changes are CathodeLib's <see cref="ScriptTransaction"/>: the before and after state of
    /// everything the refactor touched, written back onto the same objects. What the transaction does not
    /// hold is editor state, which this keeps alongside it: the script pages of each composite whose pages
    /// the refactor rewrote (with its flowgraph verdict), and a new composite's place in the browser.
    ///
    /// <para>The composite the refactor was made in is <see cref="CompositeId"/>, so undo and redo open it
    /// first; its pages are then rebuilt from the page table, and the viewer is told exactly what came and
    /// went in each composite.</para>
    /// </remarks>
    public sealed class RefactorEdit : IEdit
    {
        private readonly Composite _parent;
        private readonly Func<IRefactorPageSource, RefactorResult> _run;
        private readonly Func<RefactorResult, bool, List<Entity>> _selectAfter;
        private readonly Action<RefactorResult> _firstApplied;
        private RefactorResult _result;
        private readonly Dictionary<Composite, PageState> _pagesBefore = new Dictionary<Composite, PageState>();
        private readonly Dictionary<Composite, PageState> _pagesAfter = new Dictionary<Composite, PageState>();

        public string Label { get; }
        public ShortGuid CompositeId => _parent.shortGUID;
        public ShortGuid EntityId => ShortGuid.Invalid;

        /// <param name="run">Carries out the refactor, the first time: the plan's Apply.</param>
        /// <param name="selectAfter">What to select once done (false) or undone (true).</param>
        /// <param name="firstApplied">Anything to do once, after the first application only.</param>
        public RefactorEdit(string label, Composite parent, Func<IRefactorPageSource, RefactorResult> run, Func<RefactorResult, bool, List<Entity>> selectAfter, Action<RefactorResult> firstApplied = null)
        {
            Label = label;
            _parent = parent;
            _run = run;
            _selectAfter = selectAfter;
            _firstApplied = firstApplied;
        }

        public RefactorResult Result => _result;

        public void Apply(UndoContext context)
        {
            //Whatever the live pages hold goes into the links and the page table first: both are rewritten below
            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            if (display != null && display.Populated && display.Composite == _parent)
                display.SaveAllFlowgraphs();

            if (_result == null)
            {
                _result = _run(new EditorPageSource());
                foreach (KeyValuePair<Composite, List<FlowgraphMeta>> pages in _result.Pages)
                {
                    _pagesBefore[pages.Key] = PageState.Capture(pages.Key);
                    FlowgraphLayoutManager.RemoveAllLayouts(pages.Key);
                    foreach (FlowgraphMeta page in pages.Value)
                        FlowgraphLayoutManager.AddLayout(page.Copy());
                }
                AnnounceComposites(context, reverted: false);
                //A new composite is judged on the pages it was given, so it opens with them
                foreach (Composite added in _result.Transaction.AddedComposites())
                {
                    if (!_pagesBefore.ContainsKey(added))
                        _pagesBefore[added] = PageState.Capture(added);
                    if (!FlowgraphLayoutManager.HasCompatibilityInfo(added))
                        FlowgraphLayoutManager.EvaluateCompatibility(added);
                }
                foreach (Composite composite in _pagesBefore.Keys)
                    _pagesAfter[composite] = PageState.Capture(composite);
                _firstApplied?.Invoke(_result);
            }
            else
            {
                _result.Transaction.Reapply();
                foreach (KeyValuePair<Composite, PageState> state in _pagesAfter)
                    state.Value.Restore(state.Key);
                AnnounceComposites(context, reverted: false);
            }
            AfterChange(context, reverted: false);
        }

        public void Revert(UndoContext context)
        {
            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            if (display != null && display.Populated && display.Composite == _parent)
                display.SaveAllFlowgraphs();

            List<Composite> removing = _result.Transaction.AddedComposites();
            if (removing.Count != 0)
                context.Ui?.BeforeCompositesRemoved(new HashSet<ShortGuid>(removing.Select(o => o.shortGUID)));

            _result.Transaction.Revert();
            foreach (KeyValuePair<Composite, PageState> state in _pagesBefore)
                state.Value.Restore(state.Key);
            AnnounceComposites(context, reverted: true);
            AfterChange(context, reverted: true);
        }

        /// <summary>
        /// Composites that came or went. One that comes is announced while the viewer still has none of its
        /// contents (they are sent with everything else, in order, below), so its arrival does not queue a
        /// second copy of them.
        /// </summary>
        private void AnnounceComposites(UndoContext context, bool reverted)
        {
            List<Composite> added = _result.Transaction.AddedComposites(reverted);
            List<Composite> removed = _result.Transaction.RemovedComposites(reverted);
            using (Send.SuppressAddedCompositeContents())
                foreach (Composite composite in added)
                    Singleton.OnCompositeAdded?.Invoke(composite);
            foreach (Composite composite in removed)
                Singleton.OnCompositeDeleted?.Invoke(composite);
            if (added.Count != 0 || removed.Count != 0)
                context.Ui?.CompositesChanged();
        }

        /// <summary>
        /// Bring everything that keeps its own copy of the script up to date: the viewer (told which
        /// entities left each composite and which arrived or changed, new composites first so an instance
        /// of one spawns with its contents), the instance cache, and the composite on screen.
        /// </summary>
        private void AfterChange(UndoContext context, bool reverted)
        {
            ScriptTransaction tx = _result.Transaction;
            List<Composite> added = tx.AddedComposites(reverted);
            HashSet<Composite> removed = new HashSet<Composite>(tx.RemovedComposites(reverted));
            HashSet<Entity> changed = new HashSet<Entity>(tx.EntitiesWithChangedState());

            List<Composite> order = new List<Composite>(added);
            if (!order.Contains(_parent)) order.Add(_parent);
            foreach (Composite composite in tx.TouchedComposites)
                if (!order.Contains(composite)) order.Add(composite);

            foreach (Composite composite in order)
            {
                if (removed.Contains(composite))
                    continue;
                if (added.Contains(composite))
                {
                    Send.SendCompositeContents(composite);
                    continue;
                }
                HashSet<Entity> now = new HashSet<Entity>(composite.GetEntities());
                List<Entity> gone = tx.RemovedEntities(composite, reverted);
                List<Entity> changedHere = changed.Where(o => now.Contains(o) && !gone.Contains(o)).ToList();
                List<Entity> arrived = tx.AddedEntities(composite, reverted);
                List<Entity> resent = arrived.Concat(changedHere.Where(o => !arrived.Contains(o))).ToList();
                //A changed entity goes and comes back: the viewer only takes parameters and paths with an add
                Send.SendEntitiesDeleted(composite, gone.Concat(changedHere.Where(o => !arrived.Contains(o))));
                Send.SendCompositeContents(composite, resent);
            }

            context.Content?.EditorUtils?.GenerateCompositeInstances(context.Commands);
            DirtyTracker.MarkLevelDataModified();
            ViewerZoneSync.MarkDirty();

            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            if (display == null || display.IsDisposed || !display.Populated)
                return;
            if (display.Composite == _parent || tx.TouchedComposites.Contains(display.Composite))
            {
                display.ClearEntitySelection();
                display.Reload(true);
            }
            if (display.Composite != _parent)
                return;
            List<Entity> select = _selectAfter?.Invoke(_result, reverted)?.Where(o => o != null && _parent.GetEntityByID(o.shortGUID) == o).ToList() ?? new List<Entity>();
            if (select.Count == 0)
                return;
            //After the page windows the reload just made have settled: showing one can move the selection
            display.BeginInvoke(new Action(() =>
            {
                if (display.IsDisposed || display.Composite != _parent)
                    return;
                select.RemoveAll(o => _parent.GetEntityByID(o.shortGUID) != o);
                if (select.Count == 1)
                    display.LoadEntity(select[0], false);
                else if (select.Count > 1)
                    display.ApplyMultiSelection(select);
            }));
        }

        public bool TryMerge(IEdit next) => false;

        /// <summary>A composite's script pages and flowgraph verdict, copied so nothing else can change them.</summary>
        private sealed class PageState
        {
            private List<FlowgraphMeta> _pages;
            private CompositeFlowgraphCompatibilityTable.CompatibilityInfo _verdict;

            public static PageState Capture(Composite composite)
            {
                FlowgraphLayoutManager.CompositeLayoutState live = FlowgraphLayoutManager.RemoveCompositeState(composite);
                FlowgraphLayoutManager.RestoreCompositeState(live);
                return new PageState()
                {
                    _pages = live.Layouts.Select(o => o.Copy()).ToList(),
                    _verdict = Copy(live.Compatibility),
                };
            }

            public void Restore(Composite composite)
            {
                FlowgraphLayoutManager.RemoveCompositeState(composite);
                FlowgraphLayoutManager.RestoreCompositeState(new FlowgraphLayoutManager.CompositeLayoutState()
                {
                    Layouts = _pages.Select(o => o.Copy()).ToList(),
                    Compatibility = Copy(_verdict),
                });
            }

            private static CompositeFlowgraphCompatibilityTable.CompatibilityInfo Copy(CompositeFlowgraphCompatibilityTable.CompatibilityInfo verdict)
            {
                return verdict == null ? null : new CompositeFlowgraphCompatibilityTable.CompatibilityInfo()
                {
                    composite_id = verdict.composite_id,
                    flowgraphs_supported = verdict.flowgraphs_supported,
                };
            }
        }

        /// <summary>
        /// The page table as a refactor reads it. A composite's pages are kept in step with its links when
        /// its links are compiled from them: its verdict says so, or it has never been judged and its pages
        /// would pass (so the first time it is opened, they still do).
        /// </summary>
        private sealed class EditorPageSource : IRefactorPageSource
        {
            public List<FlowgraphMeta> GetPages(Composite composite)
            {
                return FlowgraphLayoutManager.GetLayouts(composite).Select(o => o.Copy()).ToList();
            }

            public bool PagesCarryLinks(Composite composite)
            {
                if (FlowgraphLayoutManager.HasCompatibilityInfo(composite))
                    return FlowgraphLayoutManager.IsCompatible(composite);
                if (!FlowgraphLayoutManager.HasLayout(composite))
                    return false;
                List<FlowgraphMeta> judged = GetPages(composite);
                FlowgraphLayoutManager.TrimToComposite(judged, composite, out int _, out int _);
                return RefactorPages.PagesMatchLinks(composite, judged);
            }
        }
    }
}
