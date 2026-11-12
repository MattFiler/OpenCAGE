using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.LevelDiff;
using OpenCAGE.MCP;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.Recipes
{
    /// <summary>
    /// Record Edits in the editor: <see cref="Start"/> snapshots the open level (<see cref="ScriptSnapshot"/> plus asset names) and
    /// <see cref="Stop"/> writes the net change as a <see cref="Recipe"/>, so every way of editing counts, undo included. The undo stack
    /// is only listened to for step names. UI thread.
    /// </summary>
    public static class RecipeRecorder
    {
        private static Level _level;
        private static ScriptSnapshot _before;
        private static Table _materials;
        private static HashSet<string> _models, _textures, _mappings;
        private static object _mark;
        private static bool _hooked;

        public static bool Recording => _level != null;
        public static DateTime Started { get; private set; }
        public static string LevelName => _level?.Name;
        /// <summary>A recipe the recording made when its level closed under it, waiting to be saved (or null).</summary>
        public static Recipe Orphan { get; private set; }
        /// <summary>Recording started, stopped, or a step was recorded meanwhile.</summary>
        public static event Action Changed;

        /// <summary>Undo steps made since recording started (-1 when the history was cleared meanwhile).</summary>
        public static int StepCount => Recording ? UndoStack.Current.StepsSince(_mark) : 0;

        /// <summary>The names of the undo steps made since recording started, oldest first.</summary>
        public static List<string> StepLabels()
        {
            int n = StepCount;
            if (n <= 0) return new List<string>();
            List<UndoStack.HistoryEntry> entries = UndoStack.Current.History(false, n);
            entries.Reverse();
            return entries.Select(o => (o.Origin != null ? "[" + o.Origin + "] " : "") + o.Label).ToList();
        }

        private static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            UndoStack.Current.Changed += () => { if (Recording) Changed?.Invoke(); };
            //A level closing under the recording: stop it while the level is still there, and keep what it made
            Singleton.OnLevelClosing += content =>
            {
                if (!Recording || content?.Level != _level) return;
                try { Orphan = Stop("Recorded edits (stopped when " + _level.Name + " closed)"); }
                catch (Exception e) { Debug.Log("Recipes", "Stopping the recording as the level closed failed: " + e); Discard(); }
            };
        }

        /// <summary>Start recording the open level's edits. Takes a second or two on a big level.</summary>
        public static void Start(Level level)
        {
            if (level?.Commands == null) throw new RecipeException("no level is open");
            if (Recording) throw new RecipeException("already recording " + _level.Name);
            Hook();
            ResourceNames names = new ResourceNames(level);
            _before = ScriptSnapshot.Take(level.Commands, names, level.Name);
            _materials = LevelComparer.Materials(level, names);
            _models = new HashSet<string>((level.Models?.Entries ?? new List<Models.CS2>()).Where(o => o != null).Select(o => names.ModelName(o)), StringComparer.OrdinalIgnoreCase);
            _textures = new HashSet<string>((level.Textures?.Entries ?? new List<Textures.TEX4>()).Where(o => o?.Name != null).Select(o => o.Name), StringComparer.OrdinalIgnoreCase);
            _mappings = new HashSet<string>((level.MaterialMappings?.Entries ?? new List<MaterialMappings.MaterialMapping>()).Where(o => o != null).Select(o => o.Name + " " + Fields.Line(o, null, 3)), StringComparer.Ordinal);
            _mark = UndoStack.Current.Mark();
            _level = level;
            Orphan = null;
            Started = DateTime.Now;
            Changed?.Invoke();
        }

        /// <summary>Stop recording: the recipe of what changed since <see cref="Start"/>.</summary>
        public static Recipe Stop(string name)
        {
            if (!Recording) throw new RecipeException("not recording");
            Level level = _level;
            ResourceNames names = new ResourceNames(level);
            ScriptSnapshot after = ScriptSnapshot.Take(level.Commands, names, level.Name);
            Table materialsAfter = LevelComparer.Materials(level, names);
            List<string> steps = StepLabels();
            if (StepCount < 0) steps.Add("(the undo history was cleared while recording: these names are missing; the recipe is complete)");

            //What a recipe doesn't carry: assets made while recording (they come with an import or a port)
            List<string> notCarried = new List<string>();
            foreach (Models.CS2 m in (level.Models?.Entries ?? new List<Models.CS2>()).Where(o => o != null && !_models.Contains(names.ModelName(o))))
                notCarried.Add("model " + names.ModelName(m) + " (imported while recording: import or port it into the target first)");
            foreach (Textures.TEX4 t in (level.Textures?.Entries ?? new List<Textures.TEX4>()).Where(o => o?.Name != null && !_textures.Contains(o.Name)))
                notCarried.Add("texture " + t.Name + " (added while recording: import it into the target first)");
            foreach (MaterialMappings.MaterialMapping m in (level.MaterialMappings?.Entries ?? new List<MaterialMappings.MaterialMapping>()).Where(o => o != null && !_mappings.Contains(o.Name + " " + Fields.Line(o, null, 3))))
                notCarried.Add("material mapping " + m.Name + " (made or changed while recording)");

            ScriptDelta delta = ScriptDelta.Compare(_before, after, (c, e, p, v, isAfter) => DefaultValue(level, names, c, e, p, v));
            Recipe recipe = RecipeBuilder.Build(delta, new RecipeBuilder.Info() { Name = name, Level = level.Name, Root = _before.Root, Steps = steps, NotCarried = notCarried, Recorded = Started }, _materials, materialsAfter);
            Clear();
            Changed?.Invoke();
            return recipe;
        }

        /// <summary>Stop recording and keep nothing.</summary>
        public static void Discard()
        {
            Clear();
            Orphan = null;
            Changed?.Invoke();
        }

        /// <summary>The recipe made when the level closed, handed over once.</summary>
        public static Recipe TakeOrphan()
        {
            Recipe r = Orphan;
            Orphan = null;
            return r;
        }

        private static void Clear()
        {
            _level = null;
            _before = null;
            _materials = null;
            _models = _textures = _mappings = null;
            _mark = null;
        }

        private static bool DefaultValue(Level level, ResourceNames names, ShortGuid compositeId, ShortGuid entityId, ShortGuid parameter, string value)
        {
            try
            {
                Composite composite = level.Commands.GetComposite(compositeId);
                Entity entity = composite?.GetEntityByID(entityId);
                if (entity == null) return false;
                ParameterData def = level.Commands.Utils.CreateDefaultParameterData(entity, composite, parameter);
                return def != null && CathodeJson.Text(CathodeJson.Value(def, names)) == value;
            }
            catch { return false; }
        }
    }

    /// <summary>
    /// Replays a recipe onto the open level as one undo step, "Replay: name": script changes go through McpScriptEdit so the editor
    /// follows, with material values in the same step. UI thread or a tool's thread.
    /// </summary>
    public static class RecipeReplay
    {
        public sealed class Outcome
        {
            public RecipePlan Plan;
            public int Made;
            public bool Changed;
            public string Label;
            public List<string> Notes = new List<string>();
        }

        /// <summary>The dry run against the open level.</summary>
        public static RecipePlan Plan(Recipe recipe, string ids = "auto")
        {
            return McpEditor.UI(() => RecipePlayer.Plan(recipe, McpEditor.RequireLevel(forEditing: false).Level, ids));
        }

        /// <summary>Make the plan's ready steps (and conflicts, when asked) on the open level, as one undo step.</summary>
        public static Outcome Apply(RecipePlan plan, bool overwriteConflicts, string labelPrefix = "Replay: ")
        {
            Outcome outcome = new Outcome() { Plan = plan, Label = labelPrefix + (plan.Recipe.Name ?? "recipe") };
            List<(Materials.Material material, string label, Action<bool> set)> materials = new List<(Materials.Material, string, Action<bool>)>();
            Level level = null;
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel();
                level = content.Level;
                if (level.Name != plan.Target) throw new RecipeException("the plan was made for " + plan.Target + ", but " + level.Name + " is open: plan it again");
                Commands commands = level.Commands;
                //The composite undo opens: the first one the recipe touches that is here, else the root
                Composite focus = plan.Steps.Where(o => o.Op["composite"] != null).Select(o => commands.GetComposite(plan.Map.Map(CathodeJson.ToId(o.Op["composite"])))).FirstOrDefault(o => o != null)
                    ?? commands.EntryPoints?[0] ?? commands.Entries.First();
                bool anyScript = plan.Steps.Any(o => (string)o.Op["op"] != "set_material" && (o.Status == StepStatus.Ready || (overwriteConflicts && o.Status == StepStatus.Conflict)));
                if (anyScript)
                {
                    McpScriptEdit.Outcome result = McpScriptEdit.Run(outcome.Label, focus, edit =>
                    {
                        EditorHost host = new EditorHost(edit, materials);
                        outcome.Made = RecipePlayer.Apply(plan, level, host, overwriteConflicts);
                    }, new McpScriptEdit.RunOptions() { Show = false, LabelPrefix = "" });
                    outcome.Changed = result.Changed;
                    outcome.Notes.AddRange(result.Notes);
                }
                else
                {
                    outcome.Made = RecipePlayer.Apply(plan, level, new EditorHost(null, materials), overwriteConflicts);
                }
                if (materials.Count != 0)
                {
                    UndoStack.Current.Record(new MaterialValuesEdit(outcome.Label, level, materials));
                    DirtyTracker.MarkLevelDataModified();
                    Singleton.OnResourceModified?.Invoke();
                    //Script and materials as one step
                    if (outcome.Changed) UndoStack.Current.Collapse(2, outcome.Label);
                    outcome.Changed = true;
                }
            });
            return outcome;
        }

        /// <summary>The changes go through McpScriptEdit, so they are one undoable step with the editor kept in step.</summary>
        private sealed class EditorHost : IRecipeHost
        {
            private readonly McpScriptEdit _edit;
            private readonly List<(Materials.Material, string, Action<bool>)> _materials;
            public EditorHost(McpScriptEdit edit, List<(Materials.Material, string, Action<bool>)> materials) { _edit = edit; _materials = materials; }
            private McpScriptEdit Edit => _edit ?? throw new RecipeException("a script step outside a script edit");

            public void TouchEntries() => Edit.Tx.TouchEntries();
            public void TouchContents(Composite composite) => Edit.TouchContents(composite);
            public void TouchPins(Composite composite) => Edit.Tx.TouchPins(composite);
            //Touching marks the composite changed too: McpScriptEdit drops an edit that changed nothing
            public void Touch(Composite composite, Entity entity) { Edit.Touch(composite, entity); Edit.Changed(composite, false); }
            public void Made(Composite composite, Entity entity) => Edit.Made(composite, entity, defaults: false);
            public void Delete(Composite composite, Entity entity) => Edit.Delete(composite, entity);
            public Composite AddComposite(string name, ShortGuid id)
            {
                Composite composite = Edit.AddComposite(name);
                if (composite.shortGUID != id)
                {
                    composite.shortGUID = id;
                    //Its pin table under the id it keeps (the add snapshot was under the first one)
                    Edit.Tx.TouchPins(composite);
                }
                return composite;
            }
            public void RemoveComposite(Composite composite)
            {
                Edit.Tx.TouchEntries();
                Edit.Commands.Entries.Remove(composite);
                //A change McpScriptEdit counts (the removed composite itself has no pages left to keep in step)
                Composite root = Edit.Commands.EntryPoints?[0];
                if (root != null && root != composite) Edit.Changed(root, false);
            }
            public void Changed(Composite composite, bool links) => Edit.Changed(composite, links);
            public void AfterEach(Action<bool> set) => Edit.OnEachApply(set);
            public void MaterialChanged(Materials.Material material, string label, Action<bool> set) => _materials.Add((material, label, set));
        }

        /// <summary>A replay's material values, as (part of) its undo step.</summary>
        private sealed class MaterialValuesEdit : IEdit, IChangesComposites
        {
            private readonly Level _level;
            private readonly List<(Materials.Material material, string label, Action<bool> set)> _changes;
            public string Label { get; }
            public ShortGuid CompositeId => ShortGuid.Invalid;
            public ShortGuid EntityId => ShortGuid.Invalid;
            public IEnumerable<ShortGuid> ChangedComposites => Enumerable.Empty<ShortGuid>();

            public MaterialValuesEdit(string label, Level level, List<(Materials.Material, string, Action<bool>)> changes)
            {
                Label = label;
                _level = level;
                _changes = new List<(Materials.Material, string, Action<bool>)>(changes);
                Stamp();
            }

            public void Apply(UndoContext context) { foreach (var c in _changes) c.set(false); Done(); }
            public void Revert(UndoContext context) { for (int i = _changes.Count - 1; i >= 0; i--) _changes[i].set(true); Done(); }
            public bool TryMerge(IEdit next) => false;

            private void Done()
            {
                Stamp();
                DirtyTracker.MarkLevelDataModified();
                Singleton.OnResourceModified?.Invoke();
            }

            private void Stamp()
            {
                foreach (Materials.Material m in _changes.Select(o => o.material).Distinct())
                    EditHistory.AssetChanged(_level, AssetModificationInfoTable.AssetKind.MATERIAL, m.Name);
            }
        }
    }
}
