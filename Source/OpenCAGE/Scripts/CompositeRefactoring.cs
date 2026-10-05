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
    /// De-instance Composite Instance, Create Composite From Selected, Create Composite Variant and Duplicate as the editor
    /// offers them, from the entity list, the composite browser and the viewport's
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

            Run("De-instance " + name, () => UndoStack.Current.Apply(DeinstanceEdit(plan, "De-instance " + name)));
        }

        /// <summary>The undo step that carries out a De-instance plan (the parent's live pages must be compiled first).</summary>
        internal static RefactorEdit DeinstanceEdit(DeinstancePlan plan, string label)
        {
            Composite parent = plan.Parent;
            FunctionEntity instance = plan.Instance;
            Composite content = plan.Content;
            return new RefactorEdit(label, parent,
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
                });
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
            Run("Create composite " + leaf, () => UndoStack.Current.Apply(CreateCompositeEdit(plan, "Create composite " + leaf)));
        }

        /// <summary>The undo step that carries out a Create Composite plan (the parent's live pages must be compiled first).</summary>
        internal static RefactorEdit CreateCompositeEdit(CreateCompositePlan plan, string label)
        {
            Composite parent = plan.Parent;
            return new RefactorEdit(label, parent,
                pages => plan.Apply(pages),
                (result, reverted) => reverted ? plan.Selection.ToList() : new List<Entity>() { result.CreatedInstance },
                result =>
                {
                    foreach (Entity entity in plan.Selection)
                        ParameterModificationTracker.CopyEntityModifications(parent.shortGUID, entity.shortGUID, result.CreatedComposite.shortGUID, entity.shortGUID);
                });
        }

        /// <summary>An instance of a composite selected: the composite can be copied for it alone.</summary>
        public static bool CanCreateVariant(IList<Entity> selection, Commands commands) => CanDeinstance(selection, commands);

        private static RenameGeneric _namePrompt;

        /// <summary>Duplicate a composite (the Composite Browser's Duplicate): ask for the copy's name, then make it.</summary>
        public static void Duplicate(Composite source)
        {
            Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
            if (source == null || commands == null || !commands.Entries.Contains(source) || UndoStack.Current.Blocked)
                return;
            AskName(DuplicateCompositePlan.DefaultName(commands, source), "Duplicate " + EditorUtils.GetCompositeName(source), "Name of the copy (a folder path, as the composite browser takes)", "Duplicate",
                name => Duplicate(source, name, null));
        }

        /// <summary>
        /// Create Composite Variant: copy the composite an instance places and switch the instance to the copy, so changing
        /// the copy changes only this instance. Asks for the copy's name first.
        /// </summary>
        public static void CreateVariant(FunctionEntity instance)
        {
            if (!TryGetDisplay(out CompositeDisplay display, out Commands commands))
                return;
            Composite holder = display.Composite;
            Composite source = commands.GetComposite(instance?.function ?? ShortGuid.Invalid);
            if (instance == null || source == null || holder.GetEntityByID(instance.shortGUID) != instance)
                return;
            AskName(DuplicateCompositePlan.DefaultName(commands, source), "Create Composite Variant: copy " + EditorUtils.GetCompositeName(source) + " for '" + commands.Utils.GetEntityName(holder, instance) + "'",
                "Name of the copy this instance will use", "Create Variant",
                name => Duplicate(source, name, new List<(Composite, FunctionEntity)>() { (holder, instance) }));
        }

        private static void AskName(string initial, string title, string description, string button, Action<string> chosen)
        {
            _namePrompt?.Close();
            _namePrompt = new RenameGeneric(initial, new RenameGeneric.RenameGenericContent() { Title = title, Description = description, ButtonText = button });
            _namePrompt.OnRenamed += name => chosen(name);
            _namePrompt.FormClosed += (sender, e) => _namePrompt = null;
            _namePrompt.Show();
        }

        /// <summary>Make the copy (and switch the instances) as one undo step, then show it. Anything that stops it is said, and nothing changes.</summary>
        private static void Duplicate(Composite source, string name, List<(Composite, FunctionEntity)> switching)
        {
            Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
            if (commands == null || UndoStack.Current.Blocked)
                return;
            PrepareToDuplicate(commands, source);
            DuplicateCompositePlan plan = DuplicateCompositePlan.Plan(commands, source, name, switching);
            if (!plan.CanApply)
            {
                MessageBox.Show(string.Join("\n\n", plan.Issues.Where(o => o.Blocking).Select(o => o.Message)), "Can't duplicate " + EditorUtils.GetCompositeName(source), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string label = (switching != null && switching.Count != 0 ? "Create variant of " : "Duplicate ") + EditorUtils.GetCompositeName(source);
            RefactorEdit edit = DuplicateEdit(plan, label);
            Run(label, () => UndoStack.Current.Apply(edit));
            Composite copy = edit.Result?.CreatedComposite;
            if (copy == null || !commands.Entries.Contains(copy))
                return;
            //A variant stays where the instance is (now placing the copy); a plain duplicate opens the copy
            if (switching == null || switching.Count == 0)
                Singleton.Editor?.CompositeBrowser?.LoadComposite(copy);
        }

        /// <summary>
        /// Before a composite is copied: the live pages go into the page table (the copy takes the source's pages, and
        /// for the composite on screen those are the truth until compiled), and the source gets the clean-up its first
        /// opening does (links and entries that point at nothing taken out), outside the undo step as opening does it -
        /// its pages were drawn against what that leaves, and the copy's are judged against the copy's links.
        /// </summary>
        internal static void PrepareToDuplicate(Commands commands, Composite source)
        {
            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            if (display != null && !display.IsDisposed && display.Populated)
                display.SaveAllFlowgraphs();
            if (source != null && !UndoStack.Current.Blocked && !commands.Utils.PurgedComposites.purged.Contains(source.shortGUID))
            {
                commands.Utils.PurgeDeadLinks(source);
                commands.Utils.PurgedComposites.purged.Add(source.shortGUID);
            }
        }

        /// <summary>The undo step that carries out a Duplicate plan (see <see cref="PrepareToDuplicate"/> first).</summary>
        internal static RefactorEdit DuplicateEdit(DuplicateCompositePlan plan, string label)
        {
            Composite source = plan.Source;
            Composite holder = plan.Switch.Count != 0 ? plan.Switch[0].Holder : source;
            return new RefactorEdit(label, holder,
                pages => plan.Apply(pages),
                (result, reverted) => plan.Switch.Where(o => o.Holder == holder).Select(o => (Entity)o.Instance).ToList(),
                result =>
                {
                    //Which parameters were set by hand, the page last looked at, and a new preview at the next save
                    foreach (Entity entity in result.CreatedComposite.GetEntities())
                        ParameterModificationTracker.CopyEntityModifications(source.shortGUID, entity.shortGUID, result.CreatedComposite.shortGUID, entity.shortGUID);
                    string page = FlowgraphLayoutManager.GetSelectedPage(source);
                    if (!string.IsNullOrEmpty(page))
                        FlowgraphLayoutManager.SetSelectedPage(result.CreatedComposite, page);
                    CompositePreviewManager.MarkEdited(result.CreatedComposite);
                });
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
            //A folder or name that can never be used (REQUIRED_ASSETS, TEMPLATE, PHYSICS) is dropped for the next, plainer one
            foreach (string stem in new[] { folder + leaf, leaf, "Composite" })
            {
                for (int i = 1; i <= 10000; i++)
                {
                    string candidate = stem + "_Group" + (i == 1 ? "" : "_" + i);
                    if (CreateCompositePlan.CheckName(commands, candidate) == null)
                        return candidate;
                    bool taken = commands.Entries.Any(o => o != null && string.Equals((o.name ?? "").Replace('/', '\\'), candidate, StringComparison.OrdinalIgnoreCase));
                    if (!taken)
                        break;
                }
            }
            return "Composite_Group_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }
    }
}
