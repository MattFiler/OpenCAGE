using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.Scripting.Refactor;
using OpenCAGE.DockPanels;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// De-instance and Create Composite as the editor offers them, from the entity list and the viewport's
    /// menu: work out what the refactor involves, show anything that stops it or that it cannot carry over
    /// exactly, then make it one undo step.
    /// </summary>
    internal static class CompositeRefactoring
    {
        //Past this many, pulled-out entities are left unselected rather than all loaded into the inspector
        private const int MaxSelectedAfterDeinstance = 64;

        public static bool IsCompositeInstance(Entity entity, Commands commands)
        {
            return entity is FunctionEntity function && !function.function.IsFunctionType && commands?.GetComposite(function.function) != null;
        }

        /// <summary>One composite instance selected: it can be pulled apart.</summary>
        public static bool CanDeinstance(IList<Entity> selection, Commands commands)
        {
            return selection != null && selection.Count == 1 && IsCompositeInstance(selection[0], commands);
        }

        /// <summary>Anything but the composite's own parameters selected: it can be grouped into a new composite.</summary>
        public static bool CanCreateComposite(IList<Entity> selection)
        {
            return selection != null && selection.Count != 0 && selection.All(o => o != null && !(o is VariableEntity));
        }

        public static void Deinstance(FunctionEntity instance)
        {
            if (!TryGetDisplay(out CompositeDisplay display, out Commands commands))
                return;
            Composite parent = display.Composite;
            if (instance == null || parent.GetEntityByID(instance.shortGUID) != instance)
                return;
            string name = commands.Utils.GetEntityName(parent, instance);

            //The live pages are the truth for the composite on screen until they are compiled into its links
            display.SaveAllFlowgraphs();
            DeinstancePlan plan;
            Cursor previous = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                plan = DeinstancePlan.Plan(commands, parent, instance);
            }
            finally
            {
                Cursor.Current = previous;
            }

            //A clean one just happens; anything to know about first is shown, and anything that stops it
            if (!plan.CanApply || plan.Issues.Count != 0)
            {
                using (RefactorDialog dialog = RefactorDialog.ForDeinstance(plan, name))
                {
                    if (dialog.ShowDialog(Singleton.Editor) != DialogResult.OK)
                        return;
                }
            }

            Composite content = plan.Content;
            Run("De-instance " + name, () => UndoStack.Current.Apply(new RefactorEdit("De-instance " + name, parent,
                pages => plan.Apply(pages),
                (result, reverted) =>
                {
                    if (reverted)
                        return new List<Entity>() { instance };
                    List<Entity> pulled = result.PulledEntities.Where(o => o.variant == EntityVariant.FUNCTION).ToList();
                    return pulled.Count <= MaxSelectedAfterDeinstance ? pulled : new List<Entity>();
                },
                result =>
                {
                    //Which parameters were set by hand travels with each copy
                    foreach (KeyValuePair<ShortGuid, ShortGuid> pair in result.IdMap)
                        ParameterModificationTracker.CopyEntityModifications(content.shortGUID, pair.Key, parent.shortGUID, pair.Value);
                })));
        }

        public static void CreateComposite(List<Entity> selection)
        {
            if (!TryGetDisplay(out CompositeDisplay display, out Commands commands))
                return;
            Composite parent = display.Composite;
            List<Entity> chosen = selection?.Where(o => o != null && parent.GetEntityByID(o.shortGUID) == o).ToList() ?? new List<Entity>();
            if (chosen.Count == 0)
                return;

            display.SaveAllFlowgraphs();
            CreateCompositePlan plan;
            using (RefactorDialog dialog = RefactorDialog.ForCreateComposite(commands, parent, chosen, DefaultName(commands, parent)))
            {
                if (dialog.ShowDialog(Singleton.Editor) != DialogResult.OK)
                    return;
                plan = dialog.CreatePlan;
            }
            if (plan == null || !plan.CanApply)
                return;

            string leaf = EditorUtils.GetCompositeName(new Composite() { name = plan.Name });
            Run("Create composite " + leaf, () => UndoStack.Current.Apply(new RefactorEdit("Create composite " + leaf, parent,
                pages => plan.Apply(pages),
                (result, reverted) => reverted ? plan.Selection.ToList() : new List<Entity>() { result.CreatedInstance },
                result =>
                {
                    foreach (Entity entity in plan.Selection)
                        ParameterModificationTracker.CopyEntityModifications(parent.shortGUID, entity.shortGUID, result.CreatedComposite.shortGUID, entity.shortGUID);
                })));
        }

        private static bool TryGetDisplay(out CompositeDisplay display, out Commands commands)
        {
            display = Singleton.Editor?.CompositeDisplay;
            commands = display?.Content?.Level?.Commands;
            return display != null && !display.IsDisposed && display.Populated && commands != null && !UndoStack.Current.Blocked;
        }

        private static void Run(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                //The refactor has already put back anything it changed before failing
                MessageBox.Show(what + " did not complete, and nothing was changed.\n\n" + e.Message, what + " failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Beside the composite being edited, named after it, and not taken.</summary>
        private static string DefaultName(Commands commands, Composite parent)
        {
            string path = (parent.name ?? "").Replace('/', '\\');
            int slash = path.LastIndexOf('\\');
            string folder = slash < 0 ? "" : path.Substring(0, slash + 1);
            string leaf = slash < 0 ? path : path.Substring(slash + 1);
            if (string.IsNullOrEmpty(leaf))
                leaf = "Composite";
            for (int i = 1; ; i++)
            {
                string candidate = folder + leaf + "_Group" + (i == 1 ? "" : "_" + i);
                if (CreateCompositePlan.CheckName(commands, candidate) == null)
                    return candidate;
            }
        }
    }
}
