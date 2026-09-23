using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public bool IsShowing(Composite composite)
        {
            CompositeDisplay display = Display;
            return display != null && display.Populated && display.Composite == composite;
        }

        public void AfterEdit(IEdit edit)
        {
            //A gesture made on several entities - a viewport drag of a multi-selection - selects them all again
            if (edit is IMultiEntityEdit multi && SelectAll(edit.CompositeId, multi.EntityIds))
                return;

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

        /* False when fewer than two of them are left to select, and the edit's own entity decides */
        private bool SelectAll(ShortGuid composite, IReadOnlyList<ShortGuid> ids)
        {
            CompositeDisplay display = Display;
            if (ids == null || ids.Count < 2 || display == null || !display.Populated || display.Composite?.shortGUID != composite)
                return false;

            List<Entity> entities = new List<Entity>();
            foreach (ShortGuid id in ids)
            {
                Entity entity = display.Composite.GetEntityByID(id);
                if (entity != null)
                    entities.Add(entity);
            }
            if (entities.Count < 2)
                return false;

            //Already showing exactly these: the edit refreshed them in place
            List<Entity> shown = display.EntityDisplay?.MultiSelectedEntities;
            if (shown != null && shown.Count == entities.Count && entities.All(shown.Contains))
                return true;

            display.ApplyMultiSelection(entities);
            return true;
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

        public void ReloadPages(Composite composite, List<FlowgraphMeta> layouts)
        {
            CompositeDisplay display = Display;
            if (display == null || !display.Populated || display.Composite != composite || layouts == null)
                return;

            //Only a layout the table still holds is a page's truth; one replaced since has its own
            List<FlowgraphMeta> current = FlowgraphLayoutManager.GetLayouts(composite);
            foreach (FlowgraphMeta layout in layouts)
            {
                if (!current.Any(o => ReferenceEquals(o, layout)))
                    continue;
                display.FindFlowgraph(layout.Name)?.ShowFlowgraph(composite, layout);
            }
        }

        public void EntityChanged(Entity entity, bool rowsChanged)
        {
            CompositeDisplay display = Display;
            if (display == null || !display.Populated)
                return;

            //Just the inspector: a pin delay reaches the pages through Singleton.OnPinDelayModified, which
            //the edit raises alongside the rest of its events, and every open page listens to that
            display.EntityDisplay?.RefreshParameterGrid(entity, rowsChanged);
        }

        public void ReloadEntity(Entity entity)
        {
            Display?.ReloadEntity(entity);
        }

        public void RefreshNodeMarkers()
        {
            Display?.RefreshNodeMarkers();
        }

        public void ProxyRetargeted(Composite composite, ProxyEntity proxy)
        {
            Display?.AfterProxyRetargeted(composite, proxy);
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
