using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using System;
using System.Collections.Generic;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE.Undo
{
    /// <summary>The editor's windows, as the undo stack drives them.</summary>
    internal sealed class WinFormsUndoUi : IUndoUi
    {
        private readonly CommandsEditor _editor;

        public WinFormsUndoUi(CommandsEditor editor)
        {
            _editor = editor;
        }

        private CompositeDisplay Display
        {
            get
            {
                CompositeDisplay display = _editor?.CompositeDisplay;
                return display == null || display.IsDisposed ? null : display;
            }
        }

        public void BeforeEdit(IEdit edit)
        {
            if (edit.CompositeId.IsInvalid)
                return;

            Composite composite = _editor?.CompositeBrowser?.Content?.Level?.Commands?.GetComposite(edit.CompositeId);
            if (composite == null)
                throw new InvalidOperationException("The composite this change belongs to is no longer loaded");

            CompositeDisplay display = Display;
            if (display != null && display.Populated && display.Composite == composite)
                return;

            _editor.CompositeBrowser.LoadComposite(composite);
            display = Display;
            if (display == null || display.Composite != composite)
                throw new InvalidOperationException("Could not open the composite this change belongs to");
        }

        public void AfterEdit(IEdit edit)
        {
            if (edit.EntityId.IsInvalid)
                return;

            CompositeDisplay display = Display;
            if (display == null || !display.Populated)
                return;

            //Gone means the edit deleted it, and the deletion has already cleared the inspector
            Entity entity = display.Composite?.GetEntityByID(edit.EntityId);
            if (entity == null)
                return;

            //Already showing: the edit refreshed it in place
            if (display.EntityDisplay != null && display.EntityDisplay.Populated && display.EntityDisplay.Entity == entity)
                return;

            display.LoadEntity(entity, false);
        }

        public Flowgraph Page(Composite composite, string page)
        {
            CompositeDisplay display = Display;
            if (display == null || !display.Populated || display.Composite != composite)
                return null;
            return display.FindFlowgraph(page);
        }

        public Flowgraph OpenPage(Composite composite, FlowgraphMeta meta)
        {
            CompositeDisplay display = Display;
            if (display == null || !display.Populated || display.Composite != composite || meta == null)
                return null;
            Flowgraph page = display.CreateFlowgraphWindow(meta);
            page?.Show();
            return page;
        }

        public List<NodeSnapshot> CaptureNodes(Composite composite, Entity entity)
        {
            List<NodeSnapshot> nodes = new List<NodeSnapshot>();
            CompositeDisplay display = Display;
            if (display == null || !display.Populated || display.Composite != composite)
                return nodes;

            foreach (Flowgraph page in display.Flowgraphs)
            {
                if (page == null || page.IsDisposed)
                    continue;
                nodes.AddRange(page.SnapshotNodesFor(entity));
            }
            return nodes;
        }

        public void RestoreNodes(Composite composite, List<NodeSnapshot> nodes)
        {
            foreach (NodeSnapshot snapshot in nodes)
                Page(composite, snapshot.Page)?.RestoreNode(snapshot);
        }

        public void EntityChanged(Entity entity, bool rowsChanged)
        {
            CompositeDisplay display = Display;
            if (display == null || !display.Populated)
                return;

            display.EntityDisplay?.RefreshParameterGrid(entity, rowsChanged);
            foreach (Flowgraph page in display.Flowgraphs)
            {
                if (page != null && !page.IsDisposed)
                    page.RefreshPinDelayTexts(entity);
            }
        }

        public void ReloadEntity(Entity entity)
        {
            Display?.ReloadEntity(entity);
        }

        public void RefreshNodeMarkers()
        {
            Display?.RefreshNodeMarkers();
        }

        public void CompositesChanged()
        {
            _editor?.CompositeBrowser?.RefreshList();
        }

        public void CompositesRenamed(List<Composite> renamed)
        {
            _editor?.CompositeBrowser?.AfterCompositesRenamed(renamed);
        }

        public void BeforeCompositesRemoved(HashSet<ShortGuid> ids)
        {
            CompositeDisplay display = Display;
            if (display == null || !display.Populated || display.Composite == null)
                return;
            if (ids.Contains(display.Composite.shortGUID))
                _editor?.CompositeBrowser?.CloseAllChildTabs();
        }
    }
}
