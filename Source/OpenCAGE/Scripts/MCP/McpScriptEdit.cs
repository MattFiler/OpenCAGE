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
    /// It rides on the machinery De-instance and Create Composite use (<see cref="RefactorEdit"/>): what is
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

        //The inspector's "set by hand" mark per parameter this edit wrote or removed: (before, after), put back on undo
        private readonly Dictionary<(ShortGuid composite, ShortGuid entity, ShortGuid parameter), (bool before, bool after)> _modified = new Dictionary<(ShortGuid, ShortGuid, ShortGuid), (bool, bool)>();
        private readonly List<(ShortGuid composite, ShortGuid entity)> _defaultsApplied = new List<(ShortGuid, ShortGuid)>();
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
        }

        /// <summary>
        /// Make the change <paramref name="body"/> describes as one undo step. <paramref name="focus"/> is the
        /// composite it is about: undo opens it, and (when <paramref name="show"/>) the editor goes to it and
        /// selects what was made. The body throws <see cref="McpError"/> to refuse; nothing is left changed.
        /// Call from a tool's thread.
        /// </summary>
        public static Outcome Run(string label, Composite focus, Action<McpScriptEdit> body, bool show = true)
        {
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
                if (!commands.Utils.PurgedComposites.purged.Contains(focus.shortGUID))
                {
                    commands.Utils.PurgeDeadLinks(focus);
                    commands.Utils.PurgedComposites.purged.Add(focus.shortGUID);
                }

                McpScriptEdit edit = null;
                RefactorEdit step = new RefactorEdit("AI: " + label, focus, pageSource =>
                {
                    edit = new McpScriptEdit(commands, pageSource);
                    RefactorResult result;
                    try
                    {
                        body(edit);
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
                        throw;
                    }
                    return result;
                },
                (result, reverted) => reverted ? new List<Entity>() : edit.SelectionIn(focus),
                result => edit.AfterFirstApply(),
                (result, reverted) =>
                {
                    edit.ApplyModifiedMarks(reverted);
                    edit.MarkPreviewsStale();
                });

                try
                {
                    UndoStack.Current.Apply(step);
                }
                catch (NothingToDo)
                {
                    return new Outcome() { Edit = edit, Changed = false, Label = label };
                }
                catch (Exception e) when (step.Result != null && !(e is McpError))
                {
                    //The change went in and then bringing the editor up to date failed: keep it on the history, so it can be undone
                    UndoStack.Current.Record(step);
                    Debug.Log("MCP", "An edit was made but updating the editor failed: " + e);
                    throw new McpError("The change was made, but OpenCAGE failed while updating its views (" + e.GetType().Name + ": " + e.Message + "). It is on the undo history as 'AI: " + label + "' - undo it if anything looks wrong.");
                }

                if (show)
                    Show(focus, edit.SelectionIn(focus));
                return new Outcome() { Edit = edit, Changed = true, Label = label };
            });
        }

        private sealed class NothingToDo : Exception { }

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
        }

        /// <summary>Snapshot an entity before it changes.</summary>
        public void Touch(Composite composite, Entity entity)
        {
            Prepare(composite);
            Tx.Touch(entity);
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
            FunctionEntity entity = composite.AddFunction(type);
            //What the Add Function dialog does: every parameter at its default, less the delete flag
            Commands.Utils.AddAllDefaultParameters(entity, composite);
            entity.RemoveParameter(ShortGuids.delete_me);
            //A box-shaped function's default size is zero: an empty volume, drawn as a speck. It starts at the size it is drawn
            //at instead, as one made in the editor does
            CompositeDisplay.GiveNewBoxItsShownSize(null, entity, type);
            Commands.Utils.SetEntityName(entity, string.IsNullOrWhiteSpace(name) ? UniqueName(composite, type.ToString() + "_", 1) : name.Trim());
            Made(composite, entity);
            return entity;
        }

        public FunctionEntity AddInstance(Composite composite, Composite instanced, string name)
        {
            TouchContents(composite);
            if (Commands.Utils.WouldCreateCompositeInstanceCycle(composite, instanced))
                throw new McpError("An instance of " + instanced.name + " cannot go in " + composite.name + ": " + (composite == instanced ? "a composite cannot contain itself." : instanced.name + " already contains " + composite.name + ", so it would contain itself."));
            FunctionEntity entity = composite.AddFunction(instanced);
            //What the Add Composite Instance dialog does
            Commands.Utils.AddAllDefaultParameters(entity, composite, true, ParameterVariant.STATE_PARAMETER | ParameterVariant.PARAMETER);
            CompositeInstanceParameters.Ensure(entity, Commands);
            entity.RemoveParameter(ShortGuids.delete_me);
            string leaf = McpScript.CompositeLeaf(instanced);
            Commands.Utils.SetEntityName(entity, string.IsNullOrWhiteSpace(name) ? (NameTaken(composite, leaf) ? UniqueName(composite, leaf + "_", 2) : leaf) : name.Trim());
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
        public void SetTriggerSequence(Composite composite, Entity entity, List<(ShortGuid[] path, float delay)> entries, List<string> methods, bool append)
        {
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
            }
            _changed = true;
        }

        public void Rename(Composite composite, Entity entity, string name)
        {
            if (entity is VariableEntity)
                throw new McpError("A composite's variables cannot be renamed: instances of the composite refer to them by name.");
            Touch(composite, entity);
            if (string.IsNullOrWhiteSpace(name))
                Commands.Utils.ClearEntityName(entity);
            else
                Commands.Utils.SetEntityName(entity, name.Trim());
            _changed = true;
        }

        /// <summary>
        /// Remove an entity as the editor's Delete does: links to it from the rest of its composite go, and
        /// so do trigger sequence and animation entries that point through it. Aliases in the composite
        /// that reach through it (dead now) go too. Returns the names of what went with it.
        /// </summary>
        public List<string> Delete(Composite composite, Entity entity)
        {
            TouchContents(composite);
            if (composite.GetEntityByID(entity.shortGUID) != entity)
                return new List<string>();
            List<string> alsoRemoved = new List<string>();
            composite.RemoveEntity(entity);

            foreach (Entity other in composite.GetEntities())
            {
                bool linked = other.childLinks.Any(o => o.linkedEntityID == entity.shortGUID);
                bool sequenced = other is TriggerSequence sequence && sequence.sequence.Any(o => PointsThrough(o.connectedEntity, entity));
                bool animated = other is CAGEAnimation animation && animation.connections.Any(o => PointsThrough(o.connectedEntity, entity));
                if (!linked && !sequenced && !animated) continue;
                Tx.Touch(other);
                if (linked) other.childLinks = other.childLinks.Where(o => o.linkedEntityID != entity.shortGUID).ToList();
                if (sequenced) ((TriggerSequence)other).sequence = ((TriggerSequence)other).sequence.Where(o => !PointsThrough(o.connectedEntity, entity)).ToList();
                if (animated) ((CAGEAnimation)other).connections = ((CAGEAnimation)other).connections.Where(o => !PointsThrough(o.connectedEntity, entity)).ToList();
            }

            foreach (AliasEntity alias in composite.aliases.Where(o => o.alias?.path != null && o.alias.path.Length > 1 && o.alias.path[0] == entity.shortGUID).ToList())
            {
                alsoRemoved.Add(McpScript.EntityName(Commands, composite, alias) + " (alias through it)");
                alsoRemoved.AddRange(Delete(composite, alias));
            }
            //As the editor's Delete does: composites elsewhere that reached into it are purged again on their next open
            Commands.Utils.PurgedComposites.purged.Clear();
            _relink.Add(composite);
            _changed = true;
            return alsoRemoved;
        }

        private static bool PointsThrough(EntityPath path, Entity entity)
        {
            return path?.path != null && path.path.Length >= 2 && path.path[path.path.Length - 2] == entity.shortGUID;
        }

        private bool NameTaken(Composite composite, string name) => composite.GetEntities().Any(o => string.Equals(McpScript.EntityName(Commands, composite, o), name, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Say the edit changed something itself (after <see cref="Touch"/> / <see cref="TouchContents"/>), for changes
        /// made directly rather than through the methods here. <paramref name="links"/>: its links changed, so its
        /// pages are brought in step.
        /// </summary>
        public void Changed(Composite composite, bool links = false)
        {
            Prepare(composite);
            if (links) _relink.Add(composite);
            _changed = true;
        }

        public string UniqueName(Composite composite, string stem, int first)
        {
            HashSet<string> taken = new HashSet<string>(composite.GetEntities().Select(o => McpScript.EntityName(Commands, composite, o)), StringComparer.OrdinalIgnoreCase);
            for (int i = first; ; i++)
                if (!taken.Contains(stem + i)) return stem + i;
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
                        throw new McpError("'" + name + "' on " + McpScript.TypeName(Commands, composite, entity) + " is a " + McpValues.PinKind(variant ?? ParameterVariant.PARAMETER) + " pin that only takes a link, not a value. Use add_links to connect something to it.");
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
            ShortGuid? from = parameter == null ? (ShortGuid?)null : McpScript.ParamId(parameter);
            ShortGuid? to = targetParameter == null ? (ShortGuid?)null : McpScript.ParamId(targetParameter);
            bool Matches(EntityConnector link) =>
                (from == null || link.thisParamID == from) && (target == null || link.linkedEntityID == target.shortGUID) && (to == null || link.linkedParamID == to);
            int count = owner.childLinks.Count(Matches);
            if (count == 0)
                return 0;
            Touch(composite, owner);
            owner.childLinks = owner.childLinks.Where(o => !Matches(o)).ToList();
            _relink.Add(composite);
            _changed = true;
            return count;
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

                List<FlowgraphMeta> current = mode == PageMode.Carry ? _pages.GetPages(composite) : new List<FlowgraphMeta>();
                string pageName = _pageFor.TryGetValue(composite, out string chosen) ? chosen
                    : current.Count != 0 ? (FlowgraphLayoutManager.GetSelectedPage(composite) is string selected && current.Any(o => o.Name == selected) ? selected : current[0].Name)
                    : McpScript.CompositeLeaf(composite);
                List<FlowgraphMeta> pages = RefactorPages.DrawLinks(composite, current, pageName);
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
