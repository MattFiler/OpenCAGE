using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System;
using System.Collections.Generic;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE.Undo
{
    /// <summary>What an edit runs against: the live level, and the editor UI it has to keep in step.</summary>
    public sealed class UndoContext
    {
        public LevelContent Content { get; }
        public IUndoUi Ui { get; }
        public Commands Commands => Content?.Level?.Commands;

        public UndoContext(LevelContent content, IUndoUi ui)
        {
            Content = content;
            Ui = ui;
        }

        public Composite Composite(ShortGuid id) => Commands?.GetComposite(id);
        public Entity Entity(ShortGuid composite, ShortGuid entity) => Composite(composite)?.GetEntityByID(entity);

        public Composite RequireComposite(ShortGuid id)
        {
            Composite composite = Composite(id);
            if (composite == null)
                throw new InvalidOperationException("The composite this change belongs to is no longer loaded");
            return composite;
        }

        public Entity RequireEntity(ShortGuid composite, ShortGuid entity)
        {
            Entity found = RequireComposite(composite).GetEntityByID(entity);
            if (found == null)
                throw new InvalidOperationException("The entity this change belongs to no longer exists");
            return found;
        }
    }

    /// <summary>
    /// The editor as an edit sees it. Undo brings the composite on screen before an edit runs, so
    /// edits can rely on the display and its flowgraph pages being live.
    /// </summary>
    public interface IUndoUi
    {
        /// <summary>Open the composite the edit belongs to.</summary>
        void BeforeEdit(IEdit edit);

        /// <summary>Select what the edit touched.</summary>
        void AfterEdit(IEdit edit);

        /// <summary>The live page of that name for the composite, or null if the composite is not open.</summary>
        Flowgraph Page(Composite composite, string page);

        /// <summary>Create and show a page for the composite from a layout already in the table.</summary>
        Flowgraph OpenPage(Composite composite, FlowgraphMeta meta);

        /// <summary>Every node the entity has on the composite's open pages, as it stands now.</summary>
        List<NodeSnapshot> CaptureNodes(Composite composite, Entity entity);

        /// <summary>Put captured nodes back on their pages.</summary>
        void RestoreNodes(Composite composite, List<NodeSnapshot> nodes);

        /// <summary>A parameter value changed under the inspector: repaint, or rebuild its rows.</summary>
        void EntityChanged(Entity entity, bool rowsChanged);

        /// <summary>The entity's parameter set changed: rebuild the inspector for it.</summary>
        void ReloadEntity(Entity entity);

        void RefreshNodeMarkers();

        /// <summary>Composites were added or removed: rebuild the browser.</summary>
        void CompositesChanged();

        /// <summary>Composite names changed: refresh what shows them.</summary>
        void CompositesRenamed(List<Composite> renamed);

        /// <summary>Composites are about to go: leave any of them that is on screen.</summary>
        void BeforeCompositesRemoved(HashSet<ShortGuid> ids);
    }
}
