using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.Scripting.Refactor;
using CathodeLib;
using OpenCAGE.DockPanels;
using OpenCAGE.Undo;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// A change to the script made by a tool: any number of entities, parameters and links in any number of
    /// composites, made as one step on the editor's undo history.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It rides on the machinery De-instance Composite Instance and Create Composite From Selected use (<see cref="RefactorEdit"/>): what is
    /// about to change is snapshotted first (<see cref="ScriptTransaction"/>), so undo and redo put back
    /// exactly that; the viewer is told what came, went and changed in each composite; and the composite
    /// on screen is rebuilt.
    /// </para>
    /// <para>
    /// A composite shown as script pages takes its links from its pages whenever they are compiled (on
    /// save, or on leaving it), so a link that is not drawn would be lost. Each composite touched is
    /// therefore sorted once, before it changes: pages that already draw its links are kept in step (new
    /// links drawn beside what they join, gone ones taken off); a composite whose pages do not match its
    /// links is left to its links (the editor shows it without pages); and one with no pages at all is
    /// given pages drawing everything, when everything can be drawn. A link the pages could never draw is
    /// refused where pages must carry it, rather than written and silently dropped at the next compile.
    /// </para>
    /// </remarks>
    internal sealed class McpScriptEdit
    {
        private enum PageMode { Carry, DataOnly, Generate }

        public Commands Commands { get; }
        public ScriptTransaction Tx { get; }

        private readonly IRefactorPageSource _pages;
        private readonly Dictionary<Composite, PageMode> _modes = new Dictionary<Composite, PageMode>();
        private readonly HashSet<Composite> _newComposites = new HashSet<Composite>();
        private readonly HashSet<Composite> _relink = new HashSet<Composite>();
        private readonly Dictionary<Composite, string> _pageFor = new Dictionary<Composite, string>();

        /// <summary>Entities this edit made, in order.</summary>
        public List<(Composite Composite, Entity Entity)> Created { get; } = new List<(Composite, Entity)>();
        /// <summary>Composites this edit made.</summary>
        public List<Composite> CreatedComposites { get; } = new List<Composite>();
        /// <summary>What to select afterwards in the composite the edit is shown in (defaults to what was created).</summary>
        public List<Entity> Select { get; } = new List<Entity>();

        /// <summary>Things the caller should be told about what this edit did (passed on as the call's notes).</summary>
        public List<string> Notes { get; } = new List<string>();
        /// <summary>The edit is a dry run: it is made, reported and put back, and nothing reaches the undo history.</summary>
        public bool DryRun { get; private set; }

        //The inspector's "set by hand" mark per parameter this edit wrote or removed: (before, after), put back on undo
        private readonly Dictionary<(ShortGuid composite, ShortGuid entity, ShortGuid parameter), (bool before, bool after)> _modified = new Dictionary<(ShortGuid, ShortGuid, ShortGuid), (bool, bool)>();
        private readonly List<(ShortGuid composite, ShortGuid entity)> _defaultsApplied = new List<(ShortGuid, ShortGuid)>();
        //Composites whose content this edit changed (not just looked at)
        private readonly HashSet<Composite> _edited = new HashSet<Composite>();
        //State the transaction does not snapshot (a variable's name), set again on every redo (false) and undo (true)
        private readonly List<Action<bool>> _afterEach = new List<Action<bool>>();
        //Pages this edit rewrote itself (pin renames), used in place of the composite's own when its pages are brought in step
        private readonly Dictionary<Composite, List<FlowgraphMeta>> _pageRewrites = new Dictionary<Composite, List<FlowgraphMeta>>();
        private bool _changed;

        private McpScriptEdit(Commands commands, IRefactorPageSource pages)
        {
            Commands = commands;
            Tx = new ScriptTransaction(commands);
            _pages = pages;
        }

        #region Running an edit
        /// <summary>The result of <see cref="Run"/>.</summary>
        public sealed class Outcome
        {
            public McpScriptEdit Edit;
            public bool Changed;
            public string Label;
            /// <summary>A dry run: the change was made and put back.</summary>
            public bool DryRun;
            /// <summary>Why the editor was not taken to the change (the user had gone elsewhere), or null.</summary>
            public string NotShown;
            /// <summary>Composites the change went into that are placed more than once, with how many times.</summary>
            public List<(Composite Composite, int Placements)> Shared = new List<(Composite, int)>();
            /// <summary>What the caller should be told (the edit's notes, the placements and not-shown sentences).</summary>
            public List<string> Notes = new List<string>();
        }

        /// <summary>How <see cref="Run"/> makes a change.</summary>
        public sealed class RunOptions
        {
            /// <summary>
            /// Take the editor to the focus composite and select what was made - unless the user has gone to another
            /// composite since these tools last took it somewhere (<see cref="ForceShow"/> goes anyway).
            /// </summary>
            public bool Show = true;
            /// <summary>Show the change even if the user has gone elsewhere.</summary>
            public bool ForceShow;
            /// <summary>Make the change, report it, and put everything back: nothing reaches the undo history.</summary>
            public bool DryRun;
            /// <summary>Other composites the change goes into: their first-open purge is run beforehand, as the focus composite's is.</summary>
            public IEnumerable<Composite> AlsoTouches;
        }

        /// <summary>
        /// Make the change <paramref name="body"/> describes as one undo step. <paramref name="focus"/> is the
        /// composite it is about: undo opens it, and (when <paramref name="show"/>) the editor goes to it and
        /// selects what was made. The body throws <see cref="McpError"/> to refuse; nothing is left changed.
        /// Call from a tool's thread.
        /// </summary>
        public static Outcome Run(string label, Composite focus, Action<McpScriptEdit> body, bool show = true)
        {
            return Run(label, focus, body, new RunOptions() { Show = show });
        }

        /// <summary>As <see cref="Run(string, Composite, Action{McpScriptEdit}, bool)"/>, with <paramref name="options"/>.</summary>
        public static Outcome Run(string label, Composite focus, Action<McpScriptEdit> body, RunOptions options)
        {
            options = options ?? new RunOptions();
            Hook();
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                if (focus == null || !commands.Entries.Contains(focus))
                    throw new McpError("That composite is no longer in the level.");
                McpEditor.RequireUndoIdle();

                //The live pages hold the truth for the composite on screen until they are compiled
                CompositeDisplay display = Singleton.Editor.CompositeDisplay;
                if (display != null && !display.IsDisposed && display.Populated)
                    display.SaveAllFlowgraphs();

                //What opening the composite does first (drop links and entries that point at nothing), done now and
                //outside the step: the edit then reads, and its pages are judged against, what the editor will show
                foreach (Composite purging in new[] { focus }.Concat(options.AlsoTouches ?? Enumerable.Empty<Composite>()).Distinct())
                {
                    if (purging == null || commands.Utils.PurgedComposites.purged.Contains(purging.shortGUID) || !commands.Entries.Contains(purging))
                        continue;
                    commands.Utils.PurgeDeadLinks(purging);
                    commands.Utils.PurgedComposites.purged.Add(purging.shortGUID);
                }

                //The user went to another composite since these tools last took the editor somewhere: leave them there
                Composite userAt = UserWentTo();
                bool navigate = options.ForceShow || (options.Show && (userAt == null || userAt == focus));
                bool select = navigate || !options.Show;

                McpScriptEdit edit = null;
                RefactorEdit step = new RefactorEdit("AI: " + label, focus, pageSource =>
                {
                    edit = new McpScriptEdit(commands, pageSource) { DryRun = options.DryRun };
                    RefactorResult result;
                    try
                    {
                        body(edit);
                        if (edit.DryRun)
                            throw new DryRunDone();
                        if (!edit._changed)
                            throw new NothingToDo();
                        edit.Tx.Commit();
                        result = new RefactorResult() { Transaction = edit.Tx };
                        edit.BuildPages(result);
                    }
                    catch
                    {
                        //Committed or not, the snapshots put everything back
                        edit.Tx.Revert();
                        edit.RunAfterEach(true);
                        throw;
                    }
                    return result;
                },
                (result, reverted) => reverted || !select ? new List<Entity>() : edit.SelectionIn(focus),
                result => edit.AfterFirstApply(),
                (result, reverted) =>
                {
                    edit.RunAfterEach(reverted);
                    edit.ApplyModifiedMarks(reverted);
                    edit.MarkPreviewsStale();
                    edit.ReloadShown(focus);
                });

                try
                {
                    UndoStack.Current.Apply(step);
                }
                catch (NothingToDo)
                {
                    Outcome unchanged = new Outcome() { Edit = edit, Changed = false, Label = label };
                    unchanged.Notes.AddRange(edit.Notes);
                    return unchanged;
                }
                catch (DryRunDone)
                {
                    Outcome dry = new Outcome() { Edit = edit, Changed = edit._changed, Label = label, DryRun = true };
                    dry.Notes.AddRange(edit.Notes);
                    return dry;
                }
                catch (Exception e) when (step.Result != null && !(e is McpError))
                {
                    //The change went in and then bringing the editor up to date failed: keep it on the history, so it can be undone
                    UndoStack.Current.Record(step);
                    Debug.Log("MCP", "An edit was made but updating the editor failed: " + e);
                    throw new McpError("The change was made, but OpenCAGE failed while updating its views (" + e.GetType().Name + ": " + e.Message + "). It is on the undo history as 'AI: " + label + "' - undo it if anything looks wrong.");
                }

                Outcome outcome = new Outcome() { Edit = edit, Changed = true, Label = label };
                outcome.Notes.AddRange(edit.Notes);
                if (navigate)
                    Show(focus, edit.SelectionIn(focus));
                else if (options.Show)
                    outcome.NotShown = "The editor was left on " + userAt.name + ", where the user went after your last change was shown; pass show: true to take it to " + focus.name + ".";
                if (outcome.NotShown != null)
                    outcome.Notes.Add(outcome.NotShown);

                //A change inside a composite placed more than once shows in every placement
                foreach (Composite changed in edit._edited.Where(o => !edit._newComposites.Contains(o) && commands.Entries.Contains(o)))
                {
                    int placements = PlacementCount(commands, changed);
                    if (placements > 1)
                        outcome.Shared.Add((changed, placements));
                }
                foreach ((Composite shared, int placements) in outcome.Shared.Take(3))
                    outcome.Notes.Add(shared.name + " is placed " + placements + " times, so this change shows in every placement. For one placement only, write it on an alias in the composite that places it " +
                        "(set_parameters 'path' from that composite down to the entity finds or makes one), or give that placement its own copy (duplicate_composite use_for).");
                return outcome;
            });
        }

        private sealed class NothingToDo : Exception { }
        private sealed class DryRunDone : Exception { }

        /// <summary>Set again what the transaction does not hold, after a redo (false) or an undo (true).</summary>
        private void RunAfterEach(bool reverted)
        {
            //Undone in the reverse order they were made
            IEnumerable<Action<bool>> order = reverted ? Enumerable.Reverse(_afterEach) : _afterEach;
            foreach (Action<bool> action in order.ToList())
                action(reverted);
        }

        #region Following the user
        private static bool _hooked;
        private static DateTime _userMoved = DateTime.MinValue, _assistantShowed = DateTime.MinValue;
        private static Composite _userAt;

        /// <summary>Start watching which composite the editor shows, and who took it there (any thread; once).</summary>
        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            Singleton.OnCompositeSelected += composite =>
            {
                //A tool's work on the UI thread took it there; otherwise the user did
                if (System.Threading.Volatile.Read(ref McpDialogs.UiDepth) > 0)
                    _assistantShowed = DateTime.Now;
                else
                {
                    _userMoved = DateTime.Now;
                    _userAt = composite;
                }
            };
            //The composite the user was in belongs to the level closing: held here, it would keep that level's data alive
            Singleton.OnLevelClosing += content =>
            {
                _userAt = null;
                _userMoved = _assistantShowed = DateTime.MinValue;
            };
        }

        /// <summary>The composite the user went to since these tools last took the editor somewhere, or null (nowhere, or never shown yet).</summary>
        private static Composite UserWentTo()
        {
            if (_assistantShowed == DateTime.MinValue || _userMoved <= _assistantShowed || _userAt == null)
                return null;
            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            //Only while they are still there
            return display != null && !display.IsDisposed && display.Composite == _userAt ? _userAt : null;
        }
        #endregion

        /// <summary>Take the editor to a composite (if it is elsewhere) and select entities in it. UI thread.</summary>
        public static void Show(Composite composite, List<Entity> select)
        {
            CommandsEditor editor = Singleton.Editor;
            CompositeDisplay display = editor?.CompositeDisplay;
            if (editor == null || display == null || display.IsDisposed)
                return;
            //Opening a composite rewrites its links (compile, first-open purge): never while a save is writing them out
            if (UndoStack.Current.Blocked)
                return;
            _assistantShowed = DateTime.Now;
            if (!display.Populated || display.Composite != composite)
                display = editor.CompositeBrowser?.LoadComposite(composite) ?? display;
            if (display == null || display.IsDisposed || display.Composite != composite || select == null || select.Count == 0)
                return;
            List<Entity> chosen = select.Where(o => o != null && composite.GetEntityByID(o.shortGUID) == o).Take(64).ToList();
            //Deferred like the editor's own post-edit selection; guarded, since nothing is waiting to catch a failure here
            display.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (display.IsDisposed || display.Composite != composite) return;
                    chosen.RemoveAll(o => composite.GetEntityByID(o.shortGUID) != o);
                    if (chosen.Count == 1) display.LoadEntity(chosen[0], false);
                    else if (chosen.Count > 1) display.ApplyMultiSelection(chosen);
                }
                catch (Exception e)
                {
                    Debug.Log("MCP", "Could not select what an edit made: " + e.Message);
                }
            }));
        }

        private List<Entity> SelectionIn(Composite composite)
        {
            List<Entity> chosen = Select.Count != 0 ? Select : Created.Where(o => o.Composite == composite).Select(o => o.Entity).ToList();
            return chosen.Where(o => composite.GetEntityByID(o.shortGUID) == o).ToList();
        }

        /// <summary>
        /// The composite on screen, when this edit changed it without it being the step's own composite or one whose entities
        /// came or went: the step does not rebuild its pages then, and the stale ones would be compiled back over its links
        /// the next time they are read (or the editor leaves it). Rebuilt here, on every apply and undo.
        /// </summary>
        private void ReloadShown(Composite focus)
        {
            CompositeDisplay shown = Singleton.Editor?.CompositeDisplay;
            if (shown == null || shown.IsDisposed || !shown.Populated || shown.Composite == null || shown.Composite == focus)
                return;
            if (!_edited.Contains(shown.Composite) || Tx.TouchedComposites.Contains(shown.Composite))
                return;
            try
            {
                shown.ClearEntitySelection();
                shown.Reload(true);
            }
            catch (Exception e)
            {
                Debug.Log("MCP", "Could not rebuild the composite on screen after an edit: " + e.Message);
            }
        }

        /// <summary>Composites this edit changed get a new preview picture at the next save, as the editor's own edits do.</summary>
        private void MarkPreviewsStale()
        {
            foreach (Composite composite in _modes.Keys.Concat(CreatedComposites).Distinct())
                if (Commands.Entries.Contains(composite))
                    CompositePreviewManager.MarkEdited(composite);
        }

        private void AfterFirstApply()
        {
            foreach ((ShortGuid composite, ShortGuid entity) in _defaultsApplied)
                ParameterModificationTracker.SetDefaultsApplied(composite, entity);
        }

        private void ApplyModifiedMarks(bool reverted)
        {
            foreach (KeyValuePair<(ShortGuid composite, ShortGuid entity, ShortGuid parameter), (bool before, bool after)> mark in _modified)
            {
                bool on = reverted ? mark.Value.before : mark.Value.after;
                if (on) ParameterModificationTracker.SetParameterModified(mark.Key.composite, mark.Key.entity, mark.Key.parameter);
                else ParameterModificationTracker.ClearParameterModified(mark.Key.composite, mark.Key.entity, mark.Key.parameter);
            }
        }

        private void Mark(Composite composite, Entity entity, ShortGuid parameter, bool after)
        {
            var key = (composite.shortGUID, entity.shortGUID, parameter);
            bool before = _modified.TryGetValue(key, out var known) ? known.before : ParameterModificationTracker.IsParameterModified(composite.shortGUID, entity.shortGUID, parameter);
            _modified[key] = (before, after);
        }
        #endregion

        #region Preparing
        /// <summary>
        /// Decide, before a composite changes, what happens to its pages. Its entity set is only snapshotted
        /// (<see cref="TouchContents"/>) when entities come or go: a snapshot taken for nothing would put back,
        /// on undo, an entity that has since gone without an undo record (a released deep-select alias).
        /// </summary>
        public void Prepare(Composite composite)
        {
            if (composite == null || _modes.ContainsKey(composite))
                return;
            _modes[composite] = ModeOf(composite);
        }

        /// <summary>Snapshot which entities a composite holds, before one is added or removed (then call <see cref="Made"/> for each one added).</summary>
        public void TouchContents(Composite composite)
        {
            Prepare(composite);
            Tx.Touch(composite);
            _edited.Add(composite);
        }

        /// <summary>Snapshot an entity before it changes.</summary>
        public void Touch(Composite composite, Entity entity)
        {
            Prepare(composite);
            Tx.Touch(entity);
            _edited.Add(composite);
        }

        /// <summary>
        /// Something this edit changes that the transaction does not snapshot (a variable's name): <paramref name="set"/>
        /// puts the change's state (false) or the state before it (true), on every redo and undo and when the edit fails.
        /// </summary>
        public void OnEachApply(Action<bool> set)
        {
            _afterEach.Add(set);
        }

        /// <summary>
        /// How many times a composite is placed in the level: one for an entry point (root, GLOBAL, PAUSEMENU), otherwise
        /// the sum, over every instance of it, of how many times the composite holding that instance is placed. Deleted
        /// and template placements count too. UI thread.
        /// </summary>
        public static int PlacementCount(Commands commands, Composite composite)
        {
            Dictionary<ShortGuid, List<Composite>> holders = new Dictionary<ShortGuid, List<Composite>>();
            foreach (Composite holder in commands.Entries)
            {
                if (holder == null) continue;
                foreach (FunctionEntity function in holder.functions_dictionary.Values)
                {
                    if (function.function.IsFunctionType) continue;
                    if (!holders.TryGetValue(function.function, out List<Composite> list))
                        holders[function.function] = list = new List<Composite>();
                    list.Add(holder);
                }
            }
            Dictionary<Composite, long> memo = new Dictionary<Composite, long>();
            HashSet<Composite> onStack = new HashSet<Composite>();
            long Count(Composite c)
            {
                if (memo.TryGetValue(c, out long known)) return known;
                if (commands.EntryPoints.Contains(c)) return memo[c] = 1;
                if (!onStack.Add(c)) return 0;
                long total = 0;
                if (holders.TryGetValue(c.shortGUID, out List<Composite> list))
                    foreach (Composite holder in list)
                        total = Math.Min(int.MaxValue, total + Count(holder));
                onStack.Remove(c);
                return memo[c] = total;
            }
            return (int)Count(composite);
        }

        private PageMode ModeOf(Composite composite)
        {
            if (_newComposites.Contains(composite))
                return PageMode.Generate;
            if (FlowgraphLayoutManager.HasCompatibilityInfo(composite))
                return FlowgraphLayoutManager.IsCompatible(composite) ? PageMode.Carry : PageMode.DataOnly;
            if (FlowgraphLayoutManager.HasLayout(composite))
                return _pages.PagesCarryLinks(composite) ? PageMode.Carry : PageMode.DataOnly;
            return PageMode.Generate;
        }

        /// <summary>Whether a composite's links are kept drawn on pages ("pages") or held only as data ("links").</summary>
        public string PagesOf(Composite composite)
        {
            Prepare(composite);
            return _modes[composite] == PageMode.DataOnly ? "links only (its pages do not match its links, so the editor shows it without pages)" : "pages";
        }

        /// <summary>New links in this composite that cannot go beside what they join go on this page.</summary>
        public void UsePage(Composite composite, string page)
        {
            if (!string.IsNullOrWhiteSpace(page))
                _pageFor[composite] = page.Trim();
        }
        #endregion

        #region Composites
        public Composite AddComposite(string path)
        {
            string name = McpScript.NormalisePath(path);
            string problem = CreateCompositePlan.CheckName(Commands, name);
            if (problem != null)
                throw new McpError(problem);
            Tx.TouchEntries();
            Composite composite = Commands.AddComposite(name);
            _newComposites.Add(composite);
            TouchContents(composite);
            Tx.TouchPins(composite);
            CreatedComposites.Add(composite);
            _relink.Add(composite);
            _changed = true;
            return composite;
        }
        #endregion

        #region Entities
        private static readonly HashSet<FunctionType> _onePerComposite = new HashSet<FunctionType>() { FunctionType.PhysicsSystem, FunctionType.EnvironmentModelReference };

        public FunctionEntity AddFunction(Composite composite, FunctionType type, string name)
        {
            TouchContents(composite);
            if (_onePerComposite.Contains(type) && composite.functions.Any(o => o.function == type))
                throw new McpError(composite.name + " already has a " + type + ", and a composite can only have one.");
            //Without a name, numbered as the editor numbers one (TriggerSimple_1, then _2) - worked out before it's added, so it doesn't count itself
            string entityName = string.IsNullOrWhiteSpace(name) ? CompositeDisplay.NewFunctionName(Commands, composite, type) : name.Trim();
            FunctionEntity entity = composite.AddFunction(type);
            //What the Add Function dialog does: every parameter at its default, less the delete flag
            Commands.Utils.AddAllDefaultParameters(entity, composite);
            entity.RemoveParameter(ShortGuids.delete_me);
            //A box-shaped function's default size is zero: an empty volume, drawn as a speck. It starts at the size it is drawn
            //at instead, as one made in the editor does
            CompositeDisplay.GiveNewBoxItsShownSize(null, entity, type);
            Commands.Utils.SetEntityName(entity, entityName);
            Made(composite, entity);
            return entity;
        }

        public FunctionEntity AddInstance(Composite composite, Composite instanced, string name)
        {
            //The game starts the entry points itself; a folder placeholder holds nothing
            if (Commands.EntryPoints.Contains(instanced))
                throw new McpError(McpErrorCodes.Refused, instanced.name + " is one of the level's entry points (root, GLOBAL, PAUSEMENU), which the game starts itself: it cannot be placed as an instance.");
            if (McpScriptTools.IsFolderPlaceholder(instanced))
                throw new McpError(McpErrorCodes.Refused, "'" + instanced.name + "' is a folder placeholder (made by manage_composites create_folder), not a composite: it cannot be placed.");
            TouchContents(composite);
            if (Commands.Utils.WouldCreateCompositeInstanceCycle(composite, instanced))
                throw new McpError("An instance of " + instanced.name + " cannot go in " + composite.name + ": " + (composite == instanced ? "a composite cannot contain itself." : instanced.name + " already contains " + composite.name + ", so it would contain itself."));
            //Without a name, numbered as the editor numbers one (Door_Package_1, then _2), before it's added
            string entityName = string.IsNullOrWhiteSpace(name) ? CompositeDisplay.NewInstanceName(Commands, composite, instanced) : name.Trim();
            FunctionEntity entity = composite.AddFunction(instanced);
            //What the Add Composite Instance dialog does
            Commands.Utils.AddAllDefaultParameters(entity, composite, true, ParameterVariant.STATE_PARAMETER | ParameterVariant.PARAMETER);
            CompositeInstanceParameters.Ensure(entity, Commands);
            entity.RemoveParameter(ShortGuids.delete_me);
            Commands.Utils.SetEntityName(entity, entityName);
            Made(composite, entity);
            return entity;
        }

        public VariableEntity AddVariable(Composite composite, string name, CompositePinType pinType, string enumType)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new McpError("A variable needs a name.");
            name = name.Trim();
            TouchContents(composite);
            Tx.TouchPins(composite);
            ShortGuid nameId = ShortGuidUtils.Generate(name);
            if (composite.variables.Any(o => o.name == nameId))
                throw new McpError(composite.name + " already has a variable called '" + name + "'.");
            if (pinType == CompositePinType.CompositeInputVariablePin || pinType == CompositePinType.CompositeOutputVariablePin)
                throw new McpError("Pick a typed pin (e.g. input_float, output_bool); the untyped input/output pins cannot be created.");

            ShortGuid enumId = new ShortGuid(0);
            switch (pinType)
            {
                case CompositePinType.CompositeInputEnumVariablePin:
                case CompositePinType.CompositeOutputEnumVariablePin:
                    {
                        CathodeEnumTable.EnumDescriptor descriptor = string.IsNullOrWhiteSpace(enumType) ? null : Commands.Utils.GetEnum(enumType.Trim());
                        if (descriptor == null)
                            throw new McpError("An enum variable needs 'enum_type': the name of an enum (list_enums shows them).");
                        enumId = ShortGuidUtils.Generate(descriptor.Name);
                        break;
                    }
                case CompositePinType.CompositeInputEnumStringVariablePin:
                case CompositePinType.CompositeOutputEnumStringVariablePin:
                    {
                        if (string.IsNullOrWhiteSpace(enumType) || !Enum.TryParse(enumType.Trim(), true, out EnumStringType stringType))
                            throw new McpError("An enum-string variable needs 'enum_type': one of " + string.Join(", ", Enum.GetNames(typeof(EnumStringType))) + ".");
                        enumId = ShortGuidUtils.Generate(stringType.ToString());
                        break;
                    }
            }

            //What the Add Variable dialog does
            VariableEntity variable = composite.AddVariable(name, pinType.GetDataType());
            Commands.Utils.SetPinInfo(composite, new CompositePinInfoTable.PinInfo()
            {
                VariableGUID = variable.shortGUID,
                PinTypeGUID = new ShortGuid((uint)pinType),
                PinEnumTypeGUID = enumId,
            });
            Commands.Utils.AddAllDefaultParameters(variable, composite, true, ParameterVariant.REFERENCE_PIN | ParameterVariant.TARGET_PIN | ParameterVariant.STATE_PARAMETER | ParameterVariant.INPUT_PIN | ParameterVariant.OUTPUT_PIN | ParameterVariant.PARAMETER | ParameterVariant.INTERNAL | ParameterVariant.METHOD_FUNCTION | ParameterVariant.METHOD_PIN);
            if (variable.parameters.Count != 0 && variable.parameters[0].content is cEnum enumValue)
            {
                enumValue.enumID = enumId;
                enumValue.enumIndex = Commands.Utils.GetEnum(enumId)?.Entries.FirstOrDefault()?.Index ?? 0;
            }
            else if (variable.parameters.Count != 0 && variable.parameters[0].content is cEnumString enumString)
                enumString.enumID = enumId;
            Created.Add((composite, variable));
            _relink.Add(composite);
            _changed = true;
            return variable;
        }

        /// <summary>An alias in <paramref name="composite"/>; <paramref name="path"/> is from it, ending in the target and Invalid.</summary>
        public AliasEntity AddAlias(Composite composite, ShortGuid[] path, string name)
        {
            TouchContents(composite);
            AliasEntity alias = composite.AddAlias(path);
            if (!string.IsNullOrWhiteSpace(name))
                Commands.Utils.SetEntityName(alias, name.Trim());
            Made(composite, alias, defaults: false);
            return alias;
        }

        /// <summary>A proxy in <paramref name="composite"/>; <paramref name="pathFromRoot"/> starts in the root composite (without the root's own id).</summary>
        public ProxyEntity AddProxy(Composite composite, ShortGuid[] pathFromRoot, string name)
        {
            TouchContents(composite);
            ShortGuid[] path = new[] { Commands.EntryPoints[0].shortGUID }.Concat(pathFromRoot).ToArray();
            (Composite targetComposite, Entity target) = Commands.Utils.GetResolvedTarget(Commands.Utils.ResolveProxy(path));
            if (!(target is FunctionEntity))
                throw new McpError("A proxy has to point at a function entity or composite instance, reached from the root composite.");
            ProxyEntity proxy = composite.AddProxy(Commands, path);
            Commands.Utils.SetEntityName(proxy, string.IsNullOrWhiteSpace(name) ? Commands.Utils.GetEntityName(targetComposite, target) + " Proxy" : name.Trim());
            Made(composite, proxy, defaults: false);
            return proxy;
        }

        /// <summary>
        /// The alias in <paramref name="composite"/> with this path (from it, ending in the target and Invalid), or a new one
        /// when it has none: two aliases on one path apply in no set order when the level is built, so the editor's Paste
        /// Reference reuses one too. <paramref name="created"/> says which.
        /// </summary>
        public AliasEntity FindOrAddAlias(Composite composite, ShortGuid[] path, string name, out bool created)
        {
            AliasEntity existing = AliasesOn(composite, path).FirstOrDefault();
            created = existing == null;
            return existing ?? AddAlias(composite, path, name);
        }

        /// <summary>The proxy in <paramref name="composite"/> with this path from the root (without the root's own id), or a new one.</summary>
        public ProxyEntity FindOrAddProxy(Composite composite, ShortGuid[] pathFromRoot, string name, out bool created)
        {
            ShortGuid[] path = new[] { Commands.EntryPoints[0].shortGUID }.Concat(pathFromRoot).ToArray();
            ProxyEntity existing = composite.proxies_dictionary.Values.FirstOrDefault(o => SamePath(o.proxy?.path, path));
            created = existing == null;
            return existing ?? AddProxy(composite, pathFromRoot, name);
        }

        /// <summary>The aliases of <paramref name="composite"/> whose stored path is <paramref name="path"/> (a trailing Invalid on either is ignored).</summary>
        public static List<AliasEntity> AliasesOn(Composite composite, ShortGuid[] path)
        {
            return composite.aliases_dictionary.Values.Where(o => SamePath(o.alias?.path, path)).ToList();
        }

        /// <summary>Two stored paths name the same steps (a trailing Invalid on either is ignored).</summary>
        public static bool SamePath(ShortGuid[] a, ShortGuid[] b)
        {
            if (a == null || b == null) return false;
            int lengthA = a.Length > 0 && a[a.Length - 1] == ShortGuid.Invalid ? a.Length - 1 : a.Length;
            int lengthB = b.Length > 0 && b[b.Length - 1] == ShortGuid.Invalid ? b.Length - 1 : b.Length;
            if (lengthA != lengthB || lengthA == 0) return false;
            for (int i = 0; i < lengthA; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>
        /// Something this edit made itself (after <see cref="TouchContents"/> and adding it to the composite): it is
        /// selected afterwards and its composite's pages redrawn. <paramref name="defaults"/>: it was given every
        /// default parameter (as the Add dialogs do); false for a copy of an existing entity.
        /// </summary>
        public void Made(Composite composite, Entity entity, bool defaults = true)
        {
            Created.Add((composite, entity));
            if (defaults)
                _defaultsApplied.Add((composite.shortGUID, entity.shortGUID));
            _relink.Add(composite);
            _changed = true;
        }

        /// <summary>
        /// A TriggerSequence's entries (entity paths from its composite, each with its delay) and methods,
        /// replacing or adding to what it has. Works on a proxy of one too (it keeps its own copy).
        /// </summary>
        /// <remarks>
        /// Replacing the methods takes away the pins of those left out (M, M_relay, M_finished): links in the composite on
        /// them are refused, listed, unless <paramref name="dropLinks"/>, which removes them (and the pins' delays) too.
        /// </remarks>
        public void SetTriggerSequence(Composite composite, Entity entity, List<(ShortGuid[] path, float delay)> entries, List<string> methods, bool append, bool dropLinks = false)
        {
            if (methods != null && !append)
            {
                List<TriggerSequence.MethodEntry> before = (entity as TriggerSequence)?.methods ?? (entity as ProxyEntity)?.methods ?? new List<TriggerSequence.MethodEntry>();
                HashSet<ShortGuid> keeping = new HashSet<ShortGuid>(methods.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => ShortGuidUtils.Generate(o.Trim())));
                HashSet<ShortGuid> going = new HashSet<ShortGuid>(before.Where(o => !keeping.Contains(o.method)).SelectMany(o => new[] { o.method, o.relay, o.finished }));
                if (going.Count != 0)
                {
                    List<string> linked = LinksOnPins(composite, entity, going);
                    if (linked.Count != 0 && !dropLinks)
                        throw new McpError(McpErrorCodes.Conflict, "Replacing the methods takes away pins that are linked: " + string.Join("; ", linked.Take(12)) + (linked.Count > 12 ? " (and " + (linked.Count - 12) + " more)" : "") +
                            ". Pass drop_links: true to remove those links too, or keep the methods (append: true adds without removing).");
                    foreach (Entity owner in composite.GetEntities())
                    {
                        if (owner == entity)
                            RemoveLinksWhere(composite, owner, o => going.Contains(o.thisParamID));
                        RemoveLinksWhere(composite, owner, o => o.linkedEntityID == entity.shortGUID && going.Contains(o.linkedParamID));
                    }
                    //The pins' delays go with them
                    foreach (ShortGuid pin in going)
                        if (entity.GetParameter(pin) != null) RemoveParameter(composite, entity, McpScript.ParamName(pin));
                    if (linked.Count != 0)
                        Notes.Add("Removed " + linked.Count + " link" + (linked.Count == 1 ? "" : "s") + " on the methods taken away: " + string.Join("; ", linked.Take(12)) + (linked.Count > 12 ? "; ..." : "") + ".");
                }
            }
            List<TriggerSequence.SequenceEntry> sequence;
            List<TriggerSequence.MethodEntry> methodList;
            if (entity is TriggerSequence trigger)
            {
                sequence = trigger.sequence;
                methodList = trigger.methods;
            }
            else if (entity is ProxyEntity proxy && proxy.function == FunctionType.TriggerSequence)
            {
                sequence = proxy.sequence;
                methodList = proxy.methods;
            }
            else
                throw new McpError(McpScript.EntityName(Commands, composite, entity) + " is not a TriggerSequence.");

            Touch(composite, entity);
            if (entries != null)
            {
                if (!append) sequence.Clear();
                foreach ((ShortGuid[] path, float delay) in entries)
                    sequence.Add(new TriggerSequence.SequenceEntry() { timing = delay, connectedEntity = new EntityPath(path) });
            }
            if (methods != null)
            {
                if (!append) methodList.Clear();
                foreach (string method in methods)
                    if (!string.IsNullOrWhiteSpace(method) && !methodList.Any(o => o.method == ShortGuidUtils.Generate(method.Trim())))
                        methodList.Add(new TriggerSequence.MethodEntry(method.Trim()));
                //Its method pins changed: pages drawing it are brought in step
                _relink.Add(composite);
            }
            _changed = true;
        }

        /// <summary>The links of <paramref name="composite"/> on these pins of <paramref name="entity"/>, either way, as "A.p -> B.q".</summary>
        public List<string> LinksOnPins(Composite composite, Entity entity, HashSet<ShortGuid> pins)
        {
            List<string> found = new List<string>();
            foreach (Entity owner in composite.GetEntities())
                foreach (EntityConnector link in owner.childLinks)
                    if ((owner == entity && pins.Contains(link.thisParamID)) || (link.linkedEntityID == entity.shortGUID && pins.Contains(link.linkedParamID)))
                        found.Add(LinkText(composite, owner, link));
            return found;
        }

        /// <summary>A link as "Owner.param -> Target.param".</summary>
        public string LinkText(Composite composite, Entity owner, EntityConnector link)
        {
            Entity target = composite.GetEntityByID(link.linkedEntityID);
            return McpScript.EntityName(Commands, composite, owner) + "." + McpScript.ParamName(link.thisParamID) + " -> " +
                (target == null ? "(missing " + McpScript.Id(link.linkedEntityID) + ")" : McpScript.EntityName(Commands, composite, target)) + "." + McpScript.ParamName(link.linkedParamID);
        }

        /// <summary>Remove the links of <paramref name="owner"/> that match; returns them as text.</summary>
        private List<string> RemoveLinksWhere(Composite composite, Entity owner, Func<EntityConnector, bool> matches)
        {
            List<EntityConnector> going = owner.childLinks.Where(matches).ToList();
            if (going.Count == 0)
                return new List<string>();
            List<string> text = going.Select(o => LinkText(composite, owner, o)).ToList();
            Touch(composite, owner);
            owner.childLinks = owner.childLinks.Where(o => !matches(o)).ToList();
            _relink.Add(composite);
            _changed = true;
            return text;
        }

        public void Rename(Composite composite, Entity entity, string name)
        {
            if (entity is VariableEntity)
                throw new McpError(McpErrorCodes.Refused, "A composite's variable (pin) is named by its instances, their links and overrides: rename it with rename_pin, which changes all of those with it.");
            Touch(composite, entity);
            if (string.IsNullOrWhiteSpace(name))
                Commands.Utils.ClearEntityName(entity);
            else
                Commands.Utils.SetEntityName(entity, name.Trim());
            _changed = true;
        }

        /// <summary>What <see cref="RenamePin"/> changed.</summary>
        public sealed class PinRename
        {
            public int Instances, Links, Values, Overrides, Animations;
            public List<string> Composites = new List<string>();
        }

        /// <summary>
        /// Rename a composite's variable (a pin of its instances) and everything that names it: links on it inside the
        /// composite; on every instance of the composite, anywhere, the links on that pin, its value, and the overrides
        /// aliases and proxies of the instance hold for it (with their links); animation bindings of that parameter; and
        /// the script pages drawing those links. Refused when the new name is taken.
        /// </summary>
        public PinRename RenamePin(Composite composite, VariableEntity variable, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw McpError.Invalid("Give the pin a new name.");
            newName = newName.Trim();
            ShortGuid oldId = variable.name;
            ShortGuid newId = ShortGuidUtils.Generate(newName);
            PinRename report = new PinRename();
            if (oldId == newId)
                return report;
            VariableEntity taken = composite.variables_dictionary.Values.FirstOrDefault(o => o != variable && o.name == newId);
            if (taken != null)
                throw new McpError(McpErrorCodes.Conflict, composite.name + " already has a pin called '" + newName + "'.");

            //Who names the pin, by composite: the variable inside, the instances outside, and aliases and proxies standing for those instances
            Dictionary<Composite, HashSet<Entity>> naming = new Dictionary<Composite, HashSet<Entity>>() { [composite] = new HashSet<Entity>() { variable } };
            void Add(Composite holder, Entity entity)
            {
                if (!naming.TryGetValue(holder, out HashSet<Entity> set)) naming[holder] = set = new HashSet<Entity>();
                set.Add(entity);
            }
            HashSet<ShortGuid> instanceIds = new HashSet<ShortGuid>();
            foreach (Composite holder in Commands.Entries)
            {
                if (holder == null) continue;
                foreach (FunctionEntity function in holder.functions_dictionary.Values)
                {
                    if (function.function.IsFunctionType || function.function != composite.shortGUID) continue;
                    //A pin may not take the name of something every instance has already (position, deleted...)
                    if (report.Instances == 0 && Commands.Utils.GetAllParameters(function, holder).Any(o => o.Item1 == newId))
                        throw new McpError(McpErrorCodes.Conflict, "'" + newName + "' is already a parameter every instance of " + composite.name + " has: pick another name.");
                    Add(holder, function);
                    instanceIds.Add(function.shortGUID);
                    report.Instances++;
                }
            }
            bool StandsForInstance(Entity entity, Composite holder)
            {
                (Composite _, Entity target) = Commands.Utils.GetResolvedTarget(Commands.Utils.ResolveAliasOrProxy(entity, holder));
                return target is FunctionEntity function && !function.function.IsFunctionType && function.function == composite.shortGUID;
            }
            List<(Composite holder, CAGEAnimation animation, CAGEAnimation.Connection connection)> bindings = new List<(Composite, CAGEAnimation, CAGEAnimation.Connection)>();
            if (instanceIds.Count != 0)
            {
                foreach (Composite holder in Commands.Entries)
                {
                    if (holder == null) continue;
                    foreach (AliasEntity alias in holder.aliases_dictionary.Values)
                        if (alias.alias?.path != null && alias.alias.path.Any(instanceIds.Contains) && StandsForInstance(alias, holder)) Add(holder, alias);
                    foreach (ProxyEntity proxy in holder.proxies_dictionary.Values)
                        if (proxy.proxy?.path != null && proxy.proxy.path.Any(instanceIds.Contains) && StandsForInstance(proxy, holder)) Add(holder, proxy);
                    foreach (FunctionEntity function in holder.functions_dictionary.Values)
                    {
                        if (!(function is CAGEAnimation animation)) continue;
                        foreach (CAGEAnimation.Connection connection in animation.connections)
                        {
                            if (connection.target_param != oldId || connection.connectedEntity?.path == null || !connection.connectedEntity.path.Any(instanceIds.Contains)) continue;
                            List<Tuple<Composite, Entity>> resolved = Commands.Utils.ResolveEntityPath(connection.connectedEntity, holder);
                            if (resolved.Count != 0 && resolved[resolved.Count - 1].Item2 is FunctionEntity target && target.function == composite.shortGUID)
                                bindings.Add((holder, animation, connection));
                        }
                    }
                }
            }

            //A value under the new name already on one of them would be overwritten: refuse instead
            foreach (KeyValuePair<Composite, HashSet<Entity>> pair in naming)
                foreach (Entity entity in pair.Value)
                    if (entity.GetParameter(oldId) != null && entity.GetParameter(newId) != null)
                        throw new McpError(McpErrorCodes.Conflict, McpScript.EntityName(Commands, pair.Key, entity) + " in " + pair.Key.name + " already has a parameter called '" + newName + "' as well as '" + McpScript.ParamName(oldId) + "': remove one first.");

            foreach (KeyValuePair<Composite, HashSet<Entity>> pair in naming)
            {
                Composite holder = pair.Key;
                HashSet<ShortGuid> ids = new HashSet<ShortGuid>(pair.Value.Select(o => o.shortGUID));
                bool linksChanged = false;
                foreach (Entity owner in holder.GetEntities())
                {
                    bool ownerNames = pair.Value.Contains(owner);
                    bool Renames(EntityConnector link) => (ownerNames && link.thisParamID == oldId) || (ids.Contains(link.linkedEntityID) && link.linkedParamID == oldId);
                    int count = owner.childLinks.Count(Renames);
                    if (count == 0) continue;
                    Touch(holder, owner);
                    owner.childLinks = owner.childLinks.Select(o =>
                    {
                        if (!Renames(o)) return o;
                        EntityConnector renamed = o;
                        if (ownerNames && renamed.thisParamID == oldId) renamed.thisParamID = newId;
                        if (ids.Contains(renamed.linkedEntityID) && renamed.linkedParamID == oldId) renamed.linkedParamID = newId;
                        return renamed;
                    }).ToList();
                    report.Links += count;
                    linksChanged = true;
                }
                foreach (Entity entity in pair.Value)
                {
                    Parameter value = entity.GetParameter(oldId);
                    if (value == null) continue;
                    Touch(holder, entity);
                    value.name = newId;
                    if (entity is AliasEntity || entity is ProxyEntity) report.Overrides++;
                    else if (!(entity is VariableEntity)) report.Values++;
                }
                //The pages: connections on the renamed pin, and nodes showing it unlinked
                if (RenameOnPages(holder, ids, oldId, newId) || linksChanged)
                    _relink.Add(holder);
                if (holder != composite && (linksChanged || pair.Value.Any(o => o.GetParameter(newId) != null)))
                    report.Composites.Add(holder.name);
            }
            foreach ((Composite holder, CAGEAnimation animation, CAGEAnimation.Connection connection) in bindings)
            {
                Touch(holder, animation);
                connection.target_param = newId;
                report.Animations++;
            }

            //The variable's own name is not in the transaction's snapshot: set on every apply, put back on undo
            Touch(composite, variable);
            variable.name = newId;
            OnEachApply(reverted => variable.name = reverted ? oldId : newId);
            _relink.Add(composite);
            _changed = true;
            return report;
        }

        /// <summary>
        /// A composite's pages with a pin of some of its entities renamed, used in place of its own when its pages are
        /// brought in step (only where its pages carry its links). Whether anything on them changed.
        /// </summary>
        private bool RenameOnPages(Composite composite, HashSet<ShortGuid> entities, ShortGuid oldId, ShortGuid newId)
        {
            Prepare(composite);
            if (_modes[composite] != PageMode.Carry)
                return false;
            List<FlowgraphMeta> pages = _pageRewrites.TryGetValue(composite, out List<FlowgraphMeta> already) ? already : _pages.GetPages(composite);
            bool changed = false;
            foreach (FlowgraphMeta page in pages)
            {
                foreach (FlowgraphMeta.NodeMeta node in page.Nodes)
                {
                    bool mine = entities.Contains(node.EntityGUID);
                    foreach (FlowgraphMeta.NodeMeta.ConnectionMeta connection in node.ConnectionsOut)
                    {
                        if (mine && connection.ParameterGUID == oldId) { connection.ParameterGUID = newId; changed = true; }
                        if (entities.Contains(connection.ConnectedEntityGUID) && connection.ConnectedParameterGUID == oldId) { connection.ConnectedParameterGUID = newId; changed = true; }
                    }
                    if (mine)
                        foreach (FlowgraphMeta.NodeMeta.UnlinkedPinMeta pin in node.UnlinkedPins)
                            if (pin.ParameterGUID == oldId) { pin.ParameterGUID = newId; changed = true; }
                }
            }
            if (changed)
                _pageRewrites[composite] = pages;
            return changed;
        }

        /// <summary>
        /// Remove an entity as the editor's Delete does: links to it from the rest of its composite go, and
        /// so do trigger sequence and animation entries that point at it or through it. So does everything in the
        /// rest of the level that reaches it or reaches inside it - aliases, trigger sequence entries and animation
        /// bindings in other composites, and (for a variable) its instances' links and values on that pin - in this
        /// same step, where opening those composites later would drop them where undo cannot bring them back.
        /// Proxies that reach it stay, links and all, as dead proxies (as the editor's Delete leaves them, and the
        /// purge on opening keeps them): they can be pointed somewhere else. Returns what went with it; the proxies
        /// left go in <paramref name="leftDead"/>, or in what it returns when that is null.
        /// </summary>
        public List<string> Delete(Composite composite, Entity entity, List<string> leftDead = null)
        {
            TouchContents(composite);
            if (composite.GetEntityByID(entity.shortGUID) != entity)
                return new List<string>();
            List<string> alsoRemoved = new List<string>();
            //Found while it is still there: a stored path resolves only through what exists
            List<Reach> reaching = ReachingThrough(composite, entity);
            composite.RemoveEntity(entity);

            foreach (Entity other in composite.GetEntities())
            {
                if (!other.childLinks.Any(o => o.linkedEntityID == entity.shortGUID)) continue;
                Tx.Touch(other);
                other.childLinks = other.childLinks.Where(o => o.linkedEntityID != entity.shortGUID).ToList();
            }

            foreach (Reach reach in reaching)
            {
                if (reach.Composite.GetEntityByID(reach.Owner.shortGUID) != reach.Owner)
                    continue;
                string where = reach.Composite == composite ? "" : " in " + reach.Composite.name;
                string ownerName = reach.Name;
                switch (reach.Kind)
                {
                    case ReachKind.Alias:
                        alsoRemoved.Add(ownerName + " (alias reaching it" + where + ")");
                        alsoRemoved.AddRange(Delete(reach.Composite, reach.Owner, leftDead));
                        break;
                    case ReachKind.Proxy:
                        (leftDead ?? alsoRemoved).Add(ownerName + " (proxy reaching it" + where + ": left dead, with its links, to be pointed elsewhere or deleted)");
                        break;
                    case ReachKind.Sequence:
                        {
                            Touch(reach.Composite, reach.Owner);
                            if (reach.Owner is TriggerSequence trigger) trigger.sequence = trigger.sequence.Where(o => !reach.Entries.Contains(o)).ToList();
                            else if (reach.Owner is ProxyEntity proxy) proxy.sequence = proxy.sequence.Where(o => !reach.Entries.Contains(o)).ToList();
                            alsoRemoved.Add(reach.Entries.Count + " entr" + (reach.Entries.Count == 1 ? "y" : "ies") + " of trigger sequence " + ownerName + where);
                            break;
                        }
                    case ReachKind.Animation:
                        {
                            Touch(reach.Composite, reach.Owner);
                            CAGEAnimation animation = (CAGEAnimation)reach.Owner;
                            animation.connections = animation.connections.Where(o => !reach.Connections.Contains(o)).ToList();
                            alsoRemoved.Add(reach.Connections.Count + " binding" + (reach.Connections.Count == 1 ? "" : "s") + " of CAGEAnimation " + ownerName + where);
                            break;
                        }
                    case ReachKind.PinUse:
                        {
                            //An instance of the composite the variable was a pin of: its links and value on that pin go
                            VariableEntity variable = (VariableEntity)entity;
                            List<string> links = new List<string>();
                            foreach (Entity owner in reach.Composite.GetEntities())
                            {
                                if (owner == reach.Owner)
                                    links.AddRange(RemoveLinksWhere(reach.Composite, owner, o => o.thisParamID == variable.name));
                                links.AddRange(RemoveLinksWhere(reach.Composite, owner, o => o.linkedEntityID == reach.Owner.shortGUID && o.linkedParamID == variable.name));
                            }
                            bool valued = RemoveParameter(reach.Composite, reach.Owner, McpScript.ParamName(variable.name));
                            if (links.Count != 0)
                                alsoRemoved.Add("link" + (links.Count == 1 ? " " : "s ") + string.Join("; ", links.Take(6)) + (links.Count > 6 ? " (and " + (links.Count - 6) + " more)" : "") + where);
                            if (valued)
                                alsoRemoved.Add("the value of " + ownerName + "." + McpScript.ParamName(variable.name) + where);
                            break;
                        }
                }
            }

            //As the editor's Delete does: composites elsewhere that reached into it are purged again on their next open
            if (!DryRun)
                Commands.Utils.PurgedComposites.purged.Clear();
            _relink.Add(composite);
            _changed = true;
            return alsoRemoved;
        }

        private enum ReachKind { Alias, Proxy, Sequence, Animation, PinUse }

        /// <summary>Something stored elsewhere that reaches an entity (or inside it).</summary>
        private sealed class Reach
        {
            public ReachKind Kind;
            public Composite Composite;
            public Entity Owner;
            public string Name;     //read while what it reaches is still there (an alias is named after its target)
            public List<TriggerSequence.SequenceEntry> Entries;
            public List<CAGEAnimation.Connection> Connections;
        }

        /// <summary>
        /// What in the level reaches <paramref name="entity"/> of <paramref name="composite"/>, or something inside it: aliases
        /// and proxies whose path passes through it, trigger sequence entries and animation bindings whose path does, and
        /// for a variable, the instances of its composite (whose links and values name the pin). Paths are resolved as
        /// the editor resolves them, so a step that merely shares an id with it does not count.
        /// </summary>
        private List<Reach> ReachingThrough(Composite composite, Entity entity)
        {
            List<Reach> found = new List<Reach>();
            ShortGuid id = entity.shortGUID;
            bool Through(List<Tuple<Composite, Entity>> resolved) => resolved != null && resolved.Any(o => o.Item2 == entity && o.Item1 == composite);
            //Paths run through instances to what they name: nothing elsewhere reaches through an alias or a proxy
            IEnumerable<Composite> scanning = entity is AliasEntity || entity is ProxyEntity ? new[] { composite } : (IEnumerable<Composite>)Commands.Entries;
            foreach (Composite other in scanning)
            {
                if (other == null) continue;
                foreach (AliasEntity alias in other.aliases_dictionary.Values)
                {
                    if (alias.alias?.path == null || !alias.alias.path.Contains(id) || (other == composite && alias == entity)) continue;
                    if (Through(Commands.Utils.ResolveAlias(alias, other)))
                        found.Add(new Reach() { Kind = ReachKind.Alias, Composite = other, Owner = alias });
                }
                foreach (ProxyEntity proxy in other.proxies_dictionary.Values)
                {
                    if (proxy.proxy?.path == null || !proxy.proxy.path.Contains(id) || (other == composite && proxy == entity)) continue;
                    if (Through(Commands.Utils.ResolveProxy(proxy)))
                        found.Add(new Reach() { Kind = ReachKind.Proxy, Composite = other, Owner = proxy });
                }
                foreach (FunctionEntity function in other.functions_dictionary.Values)
                {
                    if (function is TriggerSequence trigger)
                    {
                        List<TriggerSequence.SequenceEntry> entries = trigger.sequence.Where(o => o.connectedEntity?.path != null && o.connectedEntity.path.Contains(id) && Through(Commands.Utils.ResolveEntityPath(o.connectedEntity, other))).ToList();
                        if (entries.Count != 0)
                            found.Add(new Reach() { Kind = ReachKind.Sequence, Composite = other, Owner = trigger, Entries = entries });
                    }
                    else if (function is CAGEAnimation animation)
                    {
                        List<CAGEAnimation.Connection> connections = animation.connections.Where(o => o.connectedEntity?.path != null && o.connectedEntity.path.Contains(id) && Through(Commands.Utils.ResolveEntityPath(o.connectedEntity, other))).ToList();
                        if (connections.Count != 0)
                            found.Add(new Reach() { Kind = ReachKind.Animation, Composite = other, Owner = animation, Connections = connections });
                    }
                    else if (entity is VariableEntity && !function.function.IsFunctionType && function.function == composite.shortGUID)
                        found.Add(new Reach() { Kind = ReachKind.PinUse, Composite = other, Owner = function });
                }
                //A proxy of a trigger sequence keeps its own list of entries
                foreach (ProxyEntity proxy in other.proxies_dictionary.Values)
                {
                    if (proxy.function != FunctionType.TriggerSequence || proxy.sequence == null) continue;
                    List<TriggerSequence.SequenceEntry> entries = proxy.sequence.Where(o => o.connectedEntity?.path != null && o.connectedEntity.path.Contains(id) && Through(Commands.Utils.ResolveEntityPath(o.connectedEntity, other))).ToList();
                    if (entries.Count != 0)
                        found.Add(new Reach() { Kind = ReachKind.Sequence, Composite = other, Owner = proxy, Entries = entries });
                }
            }
            foreach (Reach reach in found)
                reach.Name = McpScript.EntityName(Commands, reach.Composite, reach.Owner) + " (" + McpScript.Id(reach.Owner.shortGUID) + ")";
            //Aliases first: deleting one takes its own links with it (proxies are only reported)
            return found.OrderBy(o => o.Kind == ReachKind.Alias || o.Kind == ReachKind.Proxy ? 0 : 1).ToList();
        }

        /// <summary>
        /// Say the edit changed something itself (after <see cref="Touch"/> / <see cref="TouchContents"/>), for changes
        /// made directly rather than through the methods here. <paramref name="links"/>: its links changed, so its
        /// pages are brought in step.
        /// </summary>
        public void Changed(Composite composite, bool links = false)
        {
            Prepare(composite);
            _edited.Add(composite);
            if (links) _relink.Add(composite);
            _changed = true;
        }
        #endregion

        #region Parameters
        /// <summary>
        /// Set a parameter to a JSON value, converted to the kind of data the parameter holds (its current
        /// value's kind, else its default's). Returns the value written.
        /// </summary>
        public ParameterData SetParameter(Composite composite, Entity entity, string parameterName, Newtonsoft.Json.Linq.JToken value, bool allowCustom = false)
        {
            ShortGuid id = McpScript.ParamId(parameterName);
            string name = McpScript.ParamName(id);
            if (id == ShortGuids.name)
            {
                Rename(composite, entity, McpValues.ReadString(value));
                return entity.GetParameter(ShortGuids.name)?.content;
            }

            Parameter existing = entity.GetParameter(id);
            (ParameterVariant? variant, DataType? type, ShortGuid _) = Commands.Utils.GetParameterMetadata(entity, id, composite);
            ParameterData template = existing?.content;
            //An alias or proxy with no value of its own passes on what it stands for: a part of a transform or vector given
            //to it keeps the rest of that value (rotation only keeps the target's position), not of the type's default
            if (template == null && (entity is AliasEntity || entity is ProxyEntity))
                template = InheritedValue(composite, entity, id);
            if (template == null)
            {
                if (variant == null && type == null)
                {
                    //A method's relay (LogicCounter's on_Up) is not in the schema, but holds a delay as the flowgraph gives it one
                    if (NodeUtils.GetMethodRelays(entity, composite, Commands).ContainsKey(id))
                        variant = ParameterVariant.TARGET_PIN;
                    else if (!allowCustom)
                        throw new McpError(McpScript.TypeName(Commands, composite, entity) + " has no parameter '" + name + "'." + Suggest(composite, entity, name) + " (To add a parameter it does not normally have, pass allow_custom: true.)");
                }
                else
                {
                    template = Commands.Utils.CreateDefaultParameterData(entity, composite, id);
                    if (template == null)
                        throw new McpError("'" + name + "' on " + McpScript.TypeName(Commands, composite, entity) + " is a " + McpValues.PinKind(variant ?? ParameterVariant.PARAMETER) + " pin that only takes a link, not a value. " +
                            (type != null && CommandsUtils.IsPointerType(type.Value)
                                ? "It points at another entity by a link from this pin to that entity's reference pin: add_links with {from: '" + McpScript.EntityName(Commands, composite, entity) + "', param: '" + name + "', to: <what it points at>, to_param: 'reference'}." +
                                  (type == DataType.ZONE || type == DataType.ZONE_LINK ? " create_zone_link wires a ZoneLink's zones and door for you." : "")
                                : "Use add_links to connect something to it."));
                }
            }
            if (variant == ParameterVariant.REFERENCE_PIN || variant == ParameterVariant.METHOD_FUNCTION)
                throw new McpError("'" + name + "' on " + McpScript.TypeName(Commands, composite, entity) + " is a " + McpValues.PinKind(variant.Value) + " pin: it only takes a link, not a value. Use add_links to connect something to it.");
            if (variant == ParameterVariant.METHOD_PIN || variant == ParameterVariant.TARGET_PIN)
            {
                //A value on a method or relay pin is its delay
                if (template == null || template is cFloat) template = new cFloat();
            }

            ParameterData data = McpValues.FromJson(value, template, name, Commands);
            Touch(composite, entity);
            //Always a new value object: the undo snapshot holds the old one by reference, so writing into it would change the "before" too
            if (existing != null)
                existing.content = data;
            else
                entity.AddParameter(id, data, variant ?? ParameterVariant.PARAMETER);
            Mark(composite, entity, id, true);
            _changed = true;
            return entity.GetParameter(id)?.content;
        }

        /// <summary>
        /// The value an alias or proxy with none of its own passes on for a parameter: that of an alias further along
        /// the same path (in a composite it passes through, outermost first) that sets it, else the target's own. Null
        /// when none of them has one (the type's default applies).
        /// </summary>
        public ParameterData InheritedValue(Composite composite, Entity entity, ShortGuid id)
        {
            List<Tuple<Composite, Entity>> resolved = Commands.Utils.ResolveAliasOrProxy(entity, composite);
            if (resolved == null || resolved.Count == 0 || resolved.Any(o => o.Item2 == null))
                return null;
            for (int k = entity is AliasEntity ? 1 : 0; k < resolved.Count; k++)
            {
                ShortGuid[] rest = resolved.Skip(k).Select(o => o.Item2.shortGUID).ToArray();
                foreach (AliasEntity inner in AliasesOn(resolved[k].Item1, rest))
                    if (inner != entity && inner.GetParameter(id)?.content != null)
                        return inner.GetParameter(id).content;
            }
            return resolved[resolved.Count - 1].Item2.GetParameter(id)?.content;
        }

        /// <summary>A parameter's value as things stand: the entity's own, else what an alias or proxy passes on, else the type's default (null if it has none).</summary>
        public ParameterData CurrentValue(Composite composite, Entity entity, ShortGuid id)
        {
            ParameterData value = entity.GetParameter(id)?.content;
            if (value == null && (entity is AliasEntity || entity is ProxyEntity))
                value = InheritedValue(composite, entity, id);
            if (value == null)
            {
                try { value = Commands.Utils.CreateDefaultParameterData(entity, composite, id); }
                catch { }
            }
            return value;
        }

        public bool RemoveParameter(Composite composite, Entity entity, string parameterName)
        {
            ShortGuid id = McpScript.ParamId(parameterName);
            if (entity.GetParameter(id) == null)
                return false;
            Touch(composite, entity);
            entity.RemoveParameter(id);
            Mark(composite, entity, id, false);
            _changed = true;
            return true;
        }

        private string Suggest(Composite composite, Entity entity, string name, bool relays = true)
        {
            IEnumerable<ShortGuid> pins = Commands.Utils.GetAllParameters(entity, composite).Select(o => o.Item1)
                .Concat(NodeUtils.GetDynamicPinParameters(entity, composite, Commands));
            if (relays)
                pins = pins.Concat(NodeUtils.GetMethodRelays(entity, composite, Commands).Keys);
            List<string> names = pins.Select(McpScript.ParamName).Distinct().ToList();
            List<string> near = names.Where(o => o.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf(o, StringComparison.OrdinalIgnoreCase) >= 0).Take(8).ToList();
            if (near.Count != 0) return " Did you mean: " + string.Join(", ", near) + "?";
            return names.Count == 0 ? "" : " It has: " + string.Join(", ", names.Take(40)) + (names.Count > 40 ? ", ..." : "") + ".";
        }
        #endregion

        #region Links
        /// <summary>
        /// Link <paramref name="owner"/>.<paramref name="parameter"/> to <paramref name="target"/>.<paramref name="targetParameter"/>.
        /// False if that link is already there.
        /// </summary>
        public bool AddLink(Composite composite, Entity owner, string parameter, Entity target, string targetParameter, bool allowCustom = false)
        {
            ShortGuid from = McpScript.ParamId(parameter);
            ShortGuid to = McpScript.ParamId(targetParameter);
            if (owner.childLinks.Any(o => o.thisParamID == from && o.linkedEntityID == target.shortGUID && o.linkedParamID == to))
                return false;
            if (!allowCustom)
            {
                CheckPin(composite, owner, from, source: true);
                CheckPin(composite, target, to, source: false);
                CheckPointerDirection(composite, owner, from, target, to);
                CheckDirection(composite, owner, from, target, to);
            }
            Prepare(composite);
            if (_modes[composite] == PageMode.Carry || (_modes[composite] == PageMode.Generate && _newComposites.Contains(composite)))
            {
                string problem = Undrawable(composite, owner, from, target, to);
                if (problem != null)
                    throw new McpError(problem);
            }
            Touch(composite, owner);
            owner.AddParameterLink(from, target.shortGUID, to);
            _relink.Add(composite);
            _changed = true;
            return true;
        }

        /// <summary>Remove links from <paramref name="owner"/>; null filters match anything. Returns how many went.</summary>
        public int RemoveLinks(Composite composite, Entity owner, string parameter, Entity target, string targetParameter)
        {
            return RemoveLinksListed(composite, owner, parameter, target, targetParameter).Count;
        }

        /// <summary>As <see cref="RemoveLinks"/>, returning the links that went as "A.p -> B.q".</summary>
        public List<string> RemoveLinksListed(Composite composite, Entity owner, string parameter, Entity target, string targetParameter)
        {
            ShortGuid? from = parameter == null ? (ShortGuid?)null : McpScript.ParamId(parameter);
            ShortGuid? to = targetParameter == null ? (ShortGuid?)null : McpScript.ParamId(targetParameter);
            return RemoveLinksWhere(composite, owner, link =>
                (from == null || link.thisParamID == from) && (target == null || link.linkedEntityID == target.shortGUID) && (to == null || link.linkedParamID == to));
        }

        /// <summary>
        /// Data is read by the entity that uses it, through a link it holds from its own pin to what provides the value
        /// (Checkpoint.player_spawn_position -> PositionMarker.reference), or to a variable of its composite. A link into a
        /// function's or instance's value pin is not how the game passes data - no vanilla level has one - and a method pin
        /// is called by a link into it, never used as a source. Both are refused, with the link to make instead.
        /// </summary>
        private void CheckDirection(Composite composite, Entity owner, ShortGuid from, Entity target, ShortGuid to)
        {
            string ownerName = McpScript.EntityName(Commands, composite, owner);
            string targetName = McpScript.EntityName(Commands, composite, target);
            string link = ownerName + "." + McpScript.ParamName(from) + " -> " + targetName + "." + McpScript.ParamName(to);
            if (!(owner is VariableEntity))
            {
                (ParameterVariant? fromVariant, DataType? _, ShortGuid __) = Commands.Utils.GetParameterMetadata(owner, from, composite);
                if (fromVariant == ParameterVariant.METHOD_PIN)
                {
                    ShortGuid relay = NodeUtils.GetMethodRelays(owner, composite, Commands).Where(o => o.Value == from).Select(o => o.Key).FirstOrDefault();
                    throw new McpError(McpErrorCodes.Refused, "The link " + link + " starts at a method pin. '" + McpScript.ParamName(from) + "' is an input: a link INTO it calls it. " +
                        (relay != ShortGuid.Invalid ? "To act when it runs, link from its relay '" + McpScript.ParamName(relay) + "' instead. " : "To act when " + ownerName + " does something, link from one of its relay or target pins (describe_entity lists them). ") +
                        "(allow_custom: true writes it anyway.)");
                }
            }
            if (target is VariableEntity)
                return;
            (ParameterVariant? toVariant, DataType? toType, ShortGuid ___) = Commands.Utils.GetParameterMetadata(target, to, composite);
            if (toVariant != ParameterVariant.PARAMETER && toVariant != ParameterVariant.INPUT_PIN && toVariant != ParameterVariant.STATE_PARAMETER)
                return;
            bool sourceIsReference = Commands.Utils.GetAllParameters(owner, composite).Any(o => o.Item1 == ShortGuids.reference);
            throw new McpError(McpErrorCodes.Refused, "The link " + link + " runs into a value pin, which is not how the game passes data: a value is read by the entity that uses it, through a link it holds " +
                "from its own pin to what provides the value (no vanilla level links into a function's or instance's parameter). " +
                (sourceIsReference ? "For " + targetName + " to take its " + McpScript.ParamName(to) + " from " + ownerName + ", link " + targetName + "." + McpScript.ParamName(to) + " -> " + ownerName + ".reference. " : "Link " + targetName + "." + McpScript.ParamName(to) + " to what provides it (its 'reference' pin, or a variable of the composite). ") +
                "For an event, link to a method pin instead. (allow_custom: true writes it anyway.)");
        }

        /// <summary>
        /// Why the game will not use a value just written on <paramref name="entity"/> in some or all placements: an alias elsewhere
        /// overrides it, or feeds it through a link it holds out of that parameter (instancing reads a value parameter with a link
        /// out of it from the link's other end, the outermost alias's link last and winning), or a CAGEAnimation drives it. Empty when
        /// nothing does. The entity's own links out of the parameter are <see cref="McpScript.FedBy"/>'s to report.
        /// </summary>
        public List<string> WhyValueIgnored(Composite composite, Entity entity, ShortGuid parameter)
        {
            List<string> reasons = new List<string>();
            (ParameterVariant? variant, DataType? _, ShortGuid __) = Commands.Utils.GetParameterMetadata(entity, parameter, composite);
            //A number on an event pin is its delay: nothing replaces that
            if (variant == ParameterVariant.METHOD_PIN || variant == ParameterVariant.TARGET_PIN || variant == ParameterVariant.METHOD_FUNCTION || variant == ParameterVariant.REFERENCE_PIN)
                return reasons;
            string name = McpScript.ParamName(parameter);

            if (!(entity is AliasEntity) && !(entity is ProxyEntity))
            {
                BuildOverrideIndex();
                if (_overridesByTarget.TryGetValue(entity.shortGUID, out List<(Composite, AliasEntity)> aliases))
                {
                    foreach ((Composite holder, AliasEntity alias) in aliases)
                    {
                        bool overrides = alias.GetParameter(parameter) != null;
                        List<EntityConnector> feeding = McpScript.HoldsValue(variant) ? alias.childLinks.Where(o => o.thisParamID == parameter && holder.GetEntityByID(o.linkedEntityID) != null).ToList() : new List<EntityConnector>();
                        if (!overrides && feeding.Count == 0) continue;
                        (Composite targetComposite, Entity target) = Commands.Utils.GetResolvedTarget(Commands.Utils.ResolveAlias(alias, holder));
                        if (target != entity || targetComposite != composite) continue;
                        string aliasName = "alias '" + McpScript.EntityName(Commands, holder, alias) + "' (" + McpScript.Id(alias.shortGUID) + ") in " + holder.name;
                        string where = " in the placements through " + (holder == composite ? "this composite" : holder.name);
                        if (feeding.Count != 0)
                            reasons.Add(aliasName + " links it (" + LinkText(holder, alias, feeding[feeding.Count - 1]) + "), so the game reads that" + where);
                        else
                            reasons.Add(aliasName + " overrides it" + where + " (remove_parameters on that alias drops the override)");
                        if (reasons.Count >= 6) break;
                    }
                }
                if (_animatedByTarget.TryGetValue(entity.shortGUID, out List<(Composite, CAGEAnimation, CAGEAnimation.Connection)> bindings))
                {
                    foreach ((Composite holder, CAGEAnimation animation, CAGEAnimation.Connection connection) in bindings)
                    {
                        if (connection.target_param != parameter) continue;
                        List<Tuple<Composite, Entity>> resolved = Commands.Utils.ResolveEntityPath(connection.connectedEntity, holder);
                        if (resolved.Count == 0 || resolved[resolved.Count - 1].Item2 != entity) continue;
                        reasons.Add("CAGEAnimation '" + McpScript.EntityName(Commands, holder, animation) + "' in " + holder.name + " animates its " + name + " while it plays");
                        break;
                    }
                }
            }
            return reasons;
        }

        //Aliases holding parameters or links, and animation bindings, by the id of the entity their path ends at: built once per edit
        private Dictionary<ShortGuid, List<(Composite, AliasEntity)>> _overridesByTarget;
        private Dictionary<ShortGuid, List<(Composite, CAGEAnimation, CAGEAnimation.Connection)>> _animatedByTarget;

        private void BuildOverrideIndex()
        {
            if (_overridesByTarget != null)
                return;
            _overridesByTarget = new Dictionary<ShortGuid, List<(Composite, AliasEntity)>>();
            _animatedByTarget = new Dictionary<ShortGuid, List<(Composite, CAGEAnimation, CAGEAnimation.Connection)>>();
            ShortGuid Last(ShortGuid[] path)
            {
                if (path == null || path.Length == 0) return ShortGuid.Invalid;
                return path[path.Length - 1] == ShortGuid.Invalid ? (path.Length > 1 ? path[path.Length - 2] : ShortGuid.Invalid) : path[path.Length - 1];
            }
            foreach (Composite holder in Commands.Entries)
            {
                if (holder == null) continue;
                foreach (AliasEntity alias in holder.aliases_dictionary.Values)
                {
                    if (!alias.parameters.Any(o => o.name != ShortGuids.name) && alias.childLinks.Count == 0) continue;
                    ShortGuid target = Last(alias.alias?.path);
                    if (target == ShortGuid.Invalid) continue;
                    if (!_overridesByTarget.TryGetValue(target, out List<(Composite, AliasEntity)> list))
                        _overridesByTarget[target] = list = new List<(Composite, AliasEntity)>();
                    list.Add((holder, alias));
                }
                foreach (FunctionEntity function in holder.functions_dictionary.Values)
                {
                    if (!(function is CAGEAnimation animation)) continue;
                    foreach (CAGEAnimation.Connection connection in animation.connections)
                    {
                        ShortGuid target = Last(connection.connectedEntity?.path);
                        if (target == ShortGuid.Invalid) continue;
                        if (!_animatedByTarget.TryGetValue(target, out List<(Composite, CAGEAnimation, CAGEAnimation.Connection)> list))
                            _animatedByTarget[target] = list = new List<(Composite, CAGEAnimation, CAGEAnimation.Connection)>();
                        list.Add((holder, animation, connection));
                    }
                }
            }
        }

        private void CheckPin(Composite composite, Entity entity, ShortGuid pin, bool source)
        {
            if (Commands.Utils.GetAllParameters(entity, composite).Any(o => o.Item1 == pin))
                return;
            if (entity.GetParameter(pin) != null)
                return;
            //A trigger sequence's method pins and an animation's event pins come from its data
            if (NodeUtils.GetDynamicPinParameters(entity, composite, Commands).Contains(pin))
                return;
            string name = McpScript.ParamName(pin);
            string described = McpScript.EntityName(Commands, composite, entity) + " (" + McpScript.TypeName(Commands, composite, entity) + ")";
            //A method's relay (LogicCounter's Up fires on_Up) comes from the relay table, and only links out
            if (NodeUtils.GetMethodRelays(entity, composite, Commands).TryGetValue(pin, out ShortGuid method))
            {
                if (source)
                    return;
                throw new McpError("'" + name + "' on the target " + described + " is the relay its method '" + McpScript.ParamName(method) + "' fires, so it can only be a link's source. To trigger that method, link to '" + McpScript.ParamName(method) + "'.");
            }
            throw new McpError("The " + (source ? "source" : "target") + " " + described + " has no pin '" + name + "'." + Suggest(composite, entity, name, relays: source) + " (describe_entity lists its pins; to link a pin it does not normally have, pass allow_custom: true.)");
        }

        /// <summary>
        /// A zone or zone link pointer pin takes a link from itself to the reference pin of what it points at
        /// (ZoneLink.ZoneA -> Zone.reference, Door.zone_link -> ZoneLink.reference), and it is read from the pointer
        /// pin's owner. The other way round is accepted by a composite shown without pages, but is not where the
        /// link is read from - so it is refused wherever it is.
        /// </summary>
        private void CheckPointerDirection(Composite composite, Entity owner, ShortGuid from, Entity target, ShortGuid to)
        {
            (ParameterVariant? fromVariant, DataType? _, ShortGuid __) = Commands.Utils.GetParameterMetadata(owner, from, composite);
            if (fromVariant != ParameterVariant.REFERENCE_PIN)
                return;
            (ParameterVariant? toVariant, DataType? toType, ShortGuid ___) = Commands.Utils.GetParameterMetadata(target, to, composite);
            if (toVariant != ParameterVariant.INPUT_PIN || (toType != DataType.ZONE && toType != DataType.ZONE_LINK))
                return;
            string ownerName = McpScript.EntityName(Commands, composite, owner);
            string targetName = McpScript.EntityName(Commands, composite, target);
            throw new McpError("The link " + ownerName + "." + McpScript.ParamName(from) + " -> " + targetName + "." + McpScript.ParamName(to) + " runs the wrong way. A " + (toType == DataType.ZONE ? "zone" : "zone link") +
                " pointer pin links from itself to the reference pin of what it points at: " + targetName + "." + McpScript.ParamName(to) + " -> " + ownerName + "." + McpScript.ParamName(from) + ". " +
                "(create_zone_link wires ZoneLinks and doors for you.)");
        }

        private readonly Dictionary<Composite, McpPins> _pins = new Dictionary<Composite, McpPins>();
        private McpPins PinsFor(Composite composite) => _pins.TryGetValue(composite, out McpPins pins) ? pins : (_pins[composite] = new McpPins(Commands, composite));

        /// <summary>Why the script pages could not draw this link, or null if they can.</summary>
        private string Undrawable(Composite composite, Entity owner, ShortGuid from, Entity target, ShortGuid to) => PinsFor(composite).Undrawable(owner, from, target, to);
        #endregion

        #region Pages
        private void BuildPages(RefactorResult result)
        {
            foreach (KeyValuePair<Composite, PageMode> prepared in _modes)
            {
                Composite composite = prepared.Key;
                PageMode mode = prepared.Value;
                //No pages for one that cannot be drawn whole: the editor shows it as links, which keeps every one of them
                bool redraw = _relink.Contains(composite) && mode != PageMode.DataOnly && (mode != PageMode.Generate || PinsFor(composite).UndrawableLinks().Count == 0);
                if (!redraw)
                {
                    //Its pages as they are - so the step holds its page state and verdict, and undo puts them back
                    //even if it is opened (and judged) in between
                    result.Pages[composite] = _pages.GetPages(composite);
                    continue;
                }

                List<FlowgraphMeta> current = mode != PageMode.Carry ? new List<FlowgraphMeta>()
                    : _pageRewrites.TryGetValue(composite, out List<FlowgraphMeta> rewritten) ? rewritten : _pages.GetPages(composite);
                string pageName = _pageFor.TryGetValue(composite, out string chosen) ? chosen
                    : current.Count != 0 ? (FlowgraphLayoutManager.GetSelectedPage(composite) is string selected && current.Any(o => o.Name == selected) ? selected : current[0].Name)
                    : McpScript.CompositeLeaf(composite);
                List<FlowgraphMeta> pages = RefactorPages.DrawLinks(composite, current, pageName, Commands);
                if (pages.Count == 0)
                    pages.Add(new FlowgraphMeta() { CompositeGUID = composite.shortGUID, Name = pageName, CanvasScale = 1f });
                GiveNodes(composite, pages, pageName);
                result.Pages[composite] = pages;
            }
        }

        private readonly Dictionary<Entity, (string page, System.Drawing.Point? at)> _nodes = new Dictionary<Entity, (string, System.Drawing.Point?)>();

        /// <summary>Where an entity this edit makes gets its flowgraph node: a page, a position, or both.</summary>
        public void PlaceNode(Entity entity, string page, System.Drawing.Point? at)
        {
            _nodes[entity] = (string.IsNullOrWhiteSpace(page) ? null : page.Trim(), at);
        }

        /// <summary>
        /// Entities this edit made get a node, as the editor's Add does for the page on screen: those the links drew
        /// stay beside what they join (moved if a position was asked for), the rest go where asked or in a column to
        /// the right of the page's nodes, showing every pin when the editor is set to populate new nodes.
        /// </summary>
        private void GiveNodes(Composite composite, List<FlowgraphMeta> pages, string defaultPage)
        {
            List<Entity> made = Created.Where(o => o.Composite == composite && composite.GetEntityByID(o.Entity.shortGUID) == o.Entity).Select(o => o.Entity).Distinct().ToList();
            if (made.Count == 0)
                return;
            bool allPins = SettingsManager.GetBool(Settings.PopulateAllPinsOnCreateNode);
            Dictionary<FlowgraphMeta, (int x, int y)> column = new Dictionary<FlowgraphMeta, (int, int)>();
            foreach (Entity entity in made)
            {
                _nodes.TryGetValue(entity, out (string page, System.Drawing.Point? at) wanted);
                List<FlowgraphMeta.NodeMeta> drawn = pages.SelectMany(o => o.Nodes).Where(o => o.EntityGUID == entity.shortGUID).ToList();
                if (drawn.Count != 0)
                {
                    if (wanted.at != null) drawn[0].Position = wanted.at.Value;
                    continue;
                }

                string name = wanted.page ?? defaultPage;
                FlowgraphMeta page = pages.FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
                if (page == null)
                    pages.Add(page = new FlowgraphMeta() { CompositeGUID = composite.shortGUID, Name = name, CanvasScale = 1f });

                List<NodeUtils.PinPositionInfo> pins = new List<NodeUtils.PinPositionInfo>();
                if (allPins)
                {
                    try { pins = new STNode() { Entity = entity }.GetAllPinPositions(composite, Commands); }
                    catch { }
                }
                System.Drawing.Point at;
                if (wanted.at != null)
                    at = wanted.at.Value;
                else
                {
                    if (!column.TryGetValue(page, out (int x, int y) next))
                        next = page.Nodes.Count == 0 ? (0, 0) : (page.Nodes.Max(o => o.Position.X) + 350, page.Nodes.Min(o => o.Position.Y));
                    at = new System.Drawing.Point(next.x, next.y);
                    column[page] = (next.x, next.y + 80 + 18 * Math.Max(1, pins.Count));
                }
                page.Nodes.Add(new FlowgraphMeta.NodeMeta()
                {
                    EntityGUID = entity.shortGUID,
                    NodeID = page.Nodes.Count == 0 ? 0 : page.Nodes.Max(o => o.NodeID) + 1,
                    Position = at,
                    UnlinkedPins = pins.Select(o => new FlowgraphMeta.NodeMeta.UnlinkedPinMeta() { ParameterGUID = o.ParameterGUID, PinLocation = (byte)o.Location, PinStyle = (byte)o.Style }).ToList(),
                });
            }
        }
        #endregion
    }

    /// <summary>
    /// Which side of its flowgraph node each pin of a composite's entities is drawn on, and so which links
    /// the flowgraph can draw: from a right-hand pin to a left-hand one, or from the top to the bottom.
    /// A pin with no place in its entity's schema gets one when first drawn (right as a source, left as a
    /// target), after which it keeps it - so it cannot serve as both.
    /// </summary>
    internal sealed class McpPins
    {
        private readonly Commands _commands;
        private readonly Composite _composite;
        private readonly Dictionary<(Entity, ShortGuid), PinLocation?> _sides = new Dictionary<(Entity, ShortGuid), PinLocation?>();

        public McpPins(Commands commands, Composite composite)
        {
            _commands = commands;
            _composite = composite;
        }

        public PinLocation? SideOf(Entity entity, ShortGuid parameter)
        {
            if (_sides.TryGetValue((entity, parameter), out PinLocation? known))
                return known;
            PinLocation? side = null;
            try
            {
                STNode probe = new STNode() { Entity = entity };
                side = probe.GetAllPinPositions(_composite, _commands).FirstOrDefault(o => o.ParameterGUID == parameter)?.Location;
            }
            catch { }
            _sides[(entity, parameter)] = side;
            return side;
        }

        /// <summary>Why the flowgraph could not draw this link, or null if it can.</summary>
        public string Undrawable(Entity owner, ShortGuid from, Entity target, ShortGuid to)
        {
            string Name(Entity e, ShortGuid p) => McpScript.EntityName(_commands, _composite, e) + "." + McpScript.ParamName(p);
            if (owner == target && from == to)
                return "A pin cannot be linked to itself (" + Name(owner, from) + ").";

            PinLocation? ownerKnown = SideOf(owner, from);
            PinLocation? targetKnown = SideOf(target, to);
            if (ownerKnown == null && _composite.GetEntities().Any(e => e.childLinks.Any(l => l.linkedEntityID == owner.shortGUID && l.linkedParamID == from)))
                return Name(owner, from) + " is not a pin " + McpScript.TypeName(_commands, _composite, owner) + " normally has, and it is already linked to as a target; the flowgraph can only draw such a pin one way round.";
            if (targetKnown == null && target.childLinks.Any(l => l.thisParamID == to))
                return Name(target, to) + " is not a pin " + McpScript.TypeName(_commands, _composite, target) + " normally has, and it already links out as a source; the flowgraph can only draw such a pin one way round.";

            PinLocation ownerSide = ownerKnown ?? PinLocation.Right;
            PinLocation targetSide = targetKnown ?? PinLocation.Left;
            if ((ownerSide == PinLocation.Right && targetSide == PinLocation.Left) || (ownerSide == PinLocation.Top && targetSide == PinLocation.Bottom))
                return null;
            string Describe(Entity e, ShortGuid p, PinLocation side)
            {
                (ParameterVariant? variant, DataType? _, ShortGuid __) = _commands.Utils.GetParameterMetadata(e, p, _composite);
                return Name(e, p) + " (a " + McpValues.PinKind(variant ?? ParameterVariant.PARAMETER) + " pin, drawn on the " + side.ToString().ToLowerInvariant() + " of its node)";
            }
            bool reversed = (ownerSide == PinLocation.Left && targetSide == PinLocation.Right) || (ownerSide == PinLocation.Bottom && targetSide == PinLocation.Top);
            return "The flowgraph cannot draw a link from " + Describe(owner, from, ownerSide) + " to " + Describe(target, to, targetSide) + ". " +
                (reversed ? "It runs the wrong way: link from " + Name(target, to) + " to " + Name(owner, from) + " instead. " : "") +
                "Links go from a relay or target pin (right side) to a method pin (left side), or from a parameter, input, output or state (top) to a reference or variable pin (bottom).";
        }

        /// <summary>Every link of the composite the flowgraph could not draw, as "A.p -> B.q".</summary>
        public List<string> UndrawableLinks()
        {
            List<string> problems = new List<string>();
            foreach (Entity owner in _composite.GetEntities())
            {
                foreach (EntityConnector link in owner.childLinks)
                {
                    Entity target = _composite.GetEntityByID(link.linkedEntityID);
                    if (target == null || Undrawable(owner, link.thisParamID, target, link.linkedParamID) == null) continue;
                    problems.Add(McpScript.EntityName(_commands, _composite, owner) + "." + McpScript.ParamName(link.thisParamID) + " -> " + McpScript.EntityName(_commands, _composite, target) + "." + McpScript.ParamName(link.linkedParamID));
                }
            }
            return problems;
        }
    }
}
