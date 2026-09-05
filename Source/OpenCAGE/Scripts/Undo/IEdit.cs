using CATHODE.Scripting;

namespace OpenCAGE.Undo
{
    /// <summary>
    /// One reversible change to the level's scripting data. An edit names its targets by id and
    /// resolves them against the live level when it runs, so it outlives anything the editor rebuilds
    /// in between - a composite display, a flowgraph page, an inspector row.
    /// </summary>
    public interface IEdit
    {
        /// <summary>What the menu shows: "Move Door_1", "Delete 3 entities".</summary>
        string Label { get; }

        /// <summary>The composite the change lives in. Undo opens it before applying.</summary>
        ShortGuid CompositeId { get; }

        /// <summary>The entity to select afterwards, or an invalid id when there is no one entity.</summary>
        ShortGuid EntityId { get; }

        /// <summary>Redo. Never called for the first application: the editor made that change itself.</summary>
        void Apply(UndoContext context);

        /// <summary>Undo.</summary>
        void Revert(UndoContext context);

        /// <summary>
        /// Fold a following edit into this one, so a drag or a spinner run reads as one step. Return
        /// false to leave it separate.
        /// </summary>
        bool TryMerge(IEdit next);
    }
}
