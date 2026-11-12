using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.Scripting.Refactor;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.Characters
{
    /// <summary>
    /// The editor's side of keeping a "DisplayModel:" composite in the shape the game needs (<see cref="DisplayModelShape"/>) when it is
    /// copied or renamed: CathodeLib's duplicate gives the copy's skeleton entity the copy's id; this gives the copy an animated-model
    /// entry of its own (level data, outside the script transaction) and decides the id a renamed display model moves to.
    /// </summary>
    internal static class DisplayModelIdentity
    {
        private static Level CurrentLevel => Singleton.Editor?.CompositeBrowser?.Content?.Level;

        /// <summary>
        /// For CompositeRefactoring.DuplicateEdit: after every apply (and redo) a copied display model points at an animated-model entry of
        /// its own - a copy of the original's listing the copy's parts - and after every undo that entry leaves the level again.
        /// </summary>
        public static Action<RefactorResult, bool> AfterDuplicate()
        {
            EnvironmentAnimations.EnvironmentAnimation own = null;
            return (result, reverted) =>
            {
                try
                {
                    Composite copy = result?.CreatedComposite;
                    Level level = CurrentLevel;
                    if (copy == null || level?.EnvironmentAnimations == null || !DisplayModelShape.IsDisplayModel(copy))
                        return;
                    if (reverted)
                    {
                        if (own != null) level.EnvironmentAnimations.Entries.RemoveAll(o => ReferenceEquals(o, own));
                        return;
                    }
                    ResourceReference reference = DisplayModelShape.AnimatedModelReference(DisplayModelShape.SkeletonEntity(copy));
                    if (reference?.AnimatedModel == null)
                        return;
                    if (own == null)
                    {
                        //only a copy that shares its entry (as every fresh copy does) needs one of its own
                        if (DisplayModelShape.Users(level.Commands, reference.AnimatedModel).All(o => o == copy))
                            return;
                        own = DisplayModelShape.OwnEntry(reference.AnimatedModel, copy);
                    }
                    DisplayModelShape.AddEntry(level.EnvironmentAnimations, own);
                    reference.AnimatedModel = own;
                }
                catch (Exception e)
                {
                    Console.WriteLine("DisplayModelIdentity.AfterDuplicate: " + e);
                }
            };
        }

        /// <summary>
        /// The id a composite renamed from <paramref name="before"/> to <paramref name="after"/> has to move to, or null to keep its own:
        /// a display model is found by the hash of its name, so a new "DisplayModel:" name means a new id (unless another composite holds
        /// it already). Anything else keeps its id, so its instances follow it.
        /// </summary>
        public static ShortGuid? IdAfterRename(Commands commands, Composite composite, string after)
        {
            if (commands == null || composite == null || !DisplayModelShape.IsDisplayModelName(after))
                return null;
            ShortGuid want = DisplayModelShape.IdFor(after);
            if (want == ShortGuid.Invalid || want == composite.shortGUID)
                return null;
            if (commands.GetComposite(want) != null)
                return null;
            return want;
        }

        /// <summary>
        /// A display model's id (and its skeleton entity's) moved for a rename, and back for its undo; whatever keys the editor keeps by the
        /// composite's id (its pages, its preview) are let go so they are made again.
        /// </summary>
        public static void MoveId(Commands commands, Composite composite, ShortGuid id)
        {
            ShortGuid old = composite.shortGUID;
            DisplayModelShape.ChangeCompositeId(commands, composite, id);
            try { CompositePreviewManager.MarkEdited(composite); } catch { }
        }
    }
}
