using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.Scripting.Refactor;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using OpenCAGE.Undo;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.Prefabs
{
    /// <summary>What to place: a prefab, where, and how it is set up.</summary>
    internal sealed class PrefabPlacement
    {
        public Prefab Prefab;
        /// <summary>The composite the instance goes in.</summary>
        public Composite Into;
        /// <summary>The instance's position and rotation (degrees: pitch, yaw, roll), relative to <see cref="Into"/>.</summary>
        public Vector3 Position, Rotation;
        /// <summary>The instance's name (default: the composite's name, numbered).</summary>
        public string Name;
        /// <summary>An NPC archetype to use in place of the prefab's own (a prefab with a character slot only); null keeps it.</summary>
        public Composite Character;
        /// <summary>A box size for this placement (a prefab with a box slot only); null keeps the prefab's.</summary>
        public Vector3? BoxHalfDimensions;
        /// <summary>Values for the instance's input pins, by pin name.</summary>
        public JObject Pins;
        /// <summary>Where its flowgraph node goes: a page and/or a position on it (null: beside the page's nodes).</summary>
        public string Page;
        public System.Drawing.Point? NodeAt;
        /// <summary>What the undo history calls the step, before its label ("AI: " from a tool).</summary>
        public string LabelPrefix = "";
        /// <summary>Take the editor to the new instance and select it.</summary>
        public bool Show = true;
        /// <summary>When the level lacks the NPC archetype a character prefab is packaged with, bring it in from a level of this install that has it (else refuse).</summary>
        public bool BringCharacter = true;
    }

    /// <summary>What placing a prefab did.</summary>
    internal sealed class PrefabPlaced
    {
        public FunctionEntity Instance;
        /// <summary>The composite the instance places (the prefab's own, or its copy for another character).</summary>
        public Composite Composite;
        /// <summary>Composites the package brought into the level (0 when the level had the prefab already).</summary>
        public int Imported;
        /// <summary>Composites brought in from another level of the install for a character prefab's NPC archetype.</summary>
        public int CharacterImported;
        /// <summary>The copy of the prefab made for another character, when one was made now.</summary>
        public Composite VariantMade;
        /// <summary>The alias that holds this placement's box size, when one was set.</summary>
        public AliasEntity BoxAlias;
        public List<string> Notes = new List<string>();
    }

    /// <summary>
    /// Insert Prefab and place_prefab: the package is imported when the level does not hold its composite yet (not undoable, as any
    /// composite import - the level is written at the next save), then the instance is made as one undo step. A different NPC
    /// archetype makes (once, its own undo step) a copy of the prefab whose NPC is that archetype: "Prefabs\EnemySpawner_Android_NPC".
    /// Call on the UI thread.
    /// </summary>
    internal static class PrefabPlacer
    {
        private static readonly ShortGuid NpcReference = ShortGuidUtils.Generate("npc_reference");
        private static readonly ShortGuid SpawnNpc = ShortGuidUtils.Generate("spawn_npc");

        /// <summary>An NPC archetype: the npc_reference and spawn_npc pins every retail NPC has (NPC_Logic and the prefabs themselves aside).</summary>
        public static bool IsNpcArchetype(Composite composite) =>
            composite != null && composite.variables.Any(o => o.name == NpcReference) && composite.variables.Any(o => o.name == SpawnNpc)
            && composite.name.IndexOf("Misc_Subarchetypes", StringComparison.OrdinalIgnoreCase) < 0
            && !composite.name.StartsWith(PrefabLibrary.FolderName + "\\", StringComparison.OrdinalIgnoreCase);

        /// <summary>The level's NPC archetypes, by path.</summary>
        public static List<Composite> NpcArchetypes(Commands commands) =>
            commands.Entries.Where(o => o != null && IsNpcArchetype(o)).OrderBy(o => o.name, StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>The prefab's composite in the level, or null when the level does not have it yet.</summary>
        public static Composite InLevel(Prefab prefab, Commands commands) => commands?.GetComposite(prefab.Root);

        /// <summary>The NPC archetype instance a prefab's character slot names, in <paramref name="composite"/> (the prefab or a copy of it).</summary>
        public static FunctionEntity CharacterSlot(Prefab prefab, Composite composite, Commands commands)
        {
            string slot = prefab?.Catalog?.CharacterEntity;
            if (slot == null || composite == null) return null;
            return composite.functions.FirstOrDefault(o => !o.function.IsFunctionType && commands.GetComposite(o.function) != null
                && string.Equals(commands.Utils.GetEntityName(composite, o), slot, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The archetype a prefab places as packaged (from its composite in the level), or null.</summary>
        public static Composite PackagedCharacter(Prefab prefab, Commands commands)
        {
            Composite composite = InLevel(prefab, commands);
            FunctionEntity slot = CharacterSlot(prefab, composite, commands);
            return slot == null ? null : commands.GetComposite(slot.function);
        }

        /// <summary>The copy of a prefab made for an NPC archetype: its path.</summary>
        public static string VariantName(string prefabComposite, Composite archetype) =>
            McpScript.NormalisePath(prefabComposite) + "_" + McpScript.CompositeLeaf(archetype);

        /// <summary>
        /// Bring the prefab's composite (with what it places) into the level when it is not there: the import the Import From Disk
        /// window makes, composites the level already has kept as they are. Returns how many composites arrived.
        /// </summary>
        public static int EnsureInLevel(Prefab prefab, LevelContent content)
        {
            Level level = content.Level;
            if (InLevel(prefab, level.Commands) != null)
                return 0;
            List<Composite> ported = new List<Composite>();
            CompositeArchive.Result result;
            Send.BeginSceneBatch();
            try
            {
                Singleton.OnCompositeAddPending?.Invoke();
                result = CompositeArchive.Import(prefab.File, new[] { prefab.RootGuid }, new CompositeArchive.ImportOptions() { OverwriteComposites = false, OverwriteAssets = false }, level, (copy, layouts) =>
                {
                    ported.Add(copy);
                    //Registered as a newly made composite is (dirty flag, compatibility entry, viewer); its own pages replace the default one
                    Singleton.OnCompositeAdded?.Invoke(copy);
                    if (copy.shortGUID.AsUInt32 == prefab.RootGuid)
                        CompositePreviewManager.MarkEdited(copy);
                    FlowgraphLayoutManager.ImportLayouts(copy, layouts);
                    EditHistory.CompositesChanged(level.Commands, new[] { copy.shortGUID });
                });
            }
            finally
            {
                Send.EndSceneBatch();
            }
            ViewerResourceSync.SyncImmediately();
            Singleton.Editor?.CompositeBrowser?.RefreshList();
            if (InLevel(prefab, level.Commands) == null)
                throw new McpError("The prefab package " + prefab.Key + " was read, but its composite " + prefab.RootName + " did not arrive in the level.");
            return result.PortedCount;
        }

        /// <summary>
        /// The NPC archetype a character prefab is packaged with, when the level does not have it (null when it does, or the prefab has
        /// none). The package leaves it out - it is the game's own - so it must be in the level before the prefab can come in.
        /// </summary>
        public static string MissingCharacter(Prefab prefab, Commands commands)
        {
            string id = prefab?.Catalog?.CharacterId;
            if (id == null || commands == null || commands.GetComposite(new ShortGuid(id)) != null)
                return null;
            //The prefab already in the level places it, so it is there
            return prefab.Catalog.CharacterArchetype;
        }

        /// <summary>The level of this install <see cref="BringCharacter"/> would take a prefab's NPC archetype from, or null when none has it.</summary>
        public static string CharacterSource(Prefab prefab)
        {
            foreach (string level in prefab?.Catalog?.CharacterLevels ?? new string[0])
                if (System.IO.File.Exists(System.IO.Path.Combine(Singleton.PathToAI, "DATA", "ENV", level, "WORLD", "COMMANDS.PAK")))
                    return level;
            return null;
        }

        /// <summary>
        /// Bring a character prefab's NPC archetype into the level from a level of this install that has it, as Import Composites From
        /// Level (or port_composites) would: with the composites it places and its Characters' display models and accessories. Loads that
        /// level (it takes a while); not undoable, as any port. Returns how many composites arrived.
        /// </summary>
        public static int BringCharacter(Prefab prefab, LevelContent content, List<string> notes)
        {
            string wanted = MissingCharacter(prefab, content.Level.Commands);
            if (wanted == null)
                return 0;
            string from = CharacterSource(prefab) ?? throw new McpError(prefab.Name + " needs " + wanted + ", which this level does not have, and none of the levels that have it (" +
                string.Join(", ", prefab.Catalog.CharacterLevels) + ") is in this install. Import an NPC archetype from another level and pick it as the prefab's character.");
            Level source = CompositeImporter.LoadLevel(from);
            Composite archetype = source.Commands.GetComposite(new ShortGuid(prefab.Catalog.CharacterId))
                ?? throw new McpError(from + " has no " + wanted + ", so it could not be brought in for " + prefab.Name + ".");
            Level destination = content.Level;
            CompositeFlowgraphTable sourceLayouts = CustomTable.ReadTable(source.Commands.Filepath, CustomTableType.COMPOSITE_FLOWGRAPHS) as CompositeFlowgraphTable;
            HashSet<ShortGuid> have = new HashSet<ShortGuid>(destination.Commands.Entries.Where(o => o != null).Select(o => o.shortGUID));
            List<Composite> ported = new List<Composite>();
            CompositePorter porter;
            Send.BeginSceneBatch();
            try
            {
                Singleton.OnCompositeAddPending?.Invoke();
                porter = new CompositePorter(source, destination) { OverwriteComposites = false, OverwriteAssets = false, Recurse = true };
                porter.DoNotDescendInto.Add(McpPortingTools.GlobalId);
                porter.DoNotDescendInto.Add(McpPortingTools.PauseMenuId);
                if (destination.Commands.EntryPoints[1] != null) porter.DoNotDescendInto.Add(destination.Commands.EntryPoints[1].shortGUID);
                if (destination.Commands.EntryPoints[2] != null) porter.DoNotDescendInto.Add(destination.Commands.EntryPoints[2].shortGUID);
                porter.OnCompositePorted = (original, copy) =>
                {
                    ported.Add(copy);
                    Singleton.OnCompositeAdded?.Invoke(copy);
                    FlowgraphLayoutManager.ImportLayouts(copy, FlowgraphLayoutManager.GetLayoutsForPort(original, sourceLayouts, FlowgraphLayoutManager.BundledLevelName(source.Commands, from), source.Commands));
                    EditHistory.CompositesChanged(destination.Commands, new[] { copy });
                };
                porter.Port(archetype);
                foreach (Composite model in McpPortingTools.DisplayModelsFor(source.Commands, new List<Composite>() { archetype }, true, have, false))
                    porter.Port(model);
            }
            finally
            {
                Send.EndSceneBatch();
            }
            ViewerResourceSync.SyncImmediately();
            Singleton.Editor?.CompositeBrowser?.RefreshList();
            source = null;
            GC.Collect();
            if (destination.Commands.GetComposite(new ShortGuid(prefab.Catalog.CharacterId)) == null)
                throw new McpError(wanted + " was ported from " + from + " but is not in the level.");
            notes.Add("Brought " + wanted + " in from " + from + " (" + ported.Count + " composite(s), " + porter.RenderablesPorted + " model(s); not undoable, as any import): " + prefab.Name + "'s NPC is the game's own archetype, which packages leave out.");
            return ported.Count;
        }

        /// <summary>Place a prefab (see the class summary). Refuses with an <see cref="McpError"/>; what was imported stays imported.</summary>
        public static PrefabPlaced Place(LevelContent content, PrefabPlacement p)
        {
            Commands commands = content.Level.Commands;
            if (p.Into == null || !commands.Entries.Contains(p.Into))
                throw new McpError("The composite to place the prefab in is not in the level.");
            PrefabPlaced placed = new PrefabPlaced();
            string missing = MissingCharacter(p.Prefab, commands);
            if (missing != null)
            {
                if (!p.BringCharacter)
                    throw new McpError(p.Prefab.Name + " places " + missing + ", which this level does not have" + (CharacterSource(p.Prefab) is string from ? " (" + from + " has it)" : "") + ". Bring it in first (Import Composites From Level, or port_composites), or let Insert Prefab do it.");
                placed.CharacterImported = BringCharacter(p.Prefab, content, placed.Notes);
            }
            placed.Imported = EnsureInLevel(p.Prefab, content);
            if (placed.Imported != 0)
                placed.Notes.Add("Imported " + placed.Imported + " composite(s) from " + p.Prefab.Key + PackageFiles.CompositeExtension + " (" + p.Prefab.RootName + (placed.Imported > 1 ? " and what it places" : "") + "); the level keeps them at the next save.");
            Composite composite = InLevel(p.Prefab, commands);

            //Another character: a copy of the prefab whose NPC is that archetype, made once and reused after
            bool repoint = false;
            FunctionEntity slot = null;
            if (p.Character != null)
            {
                if (p.Prefab.Catalog?.CharacterEntity == null)
                    throw new McpError(p.Prefab.Name + " has no character to change.");
                if (!IsNpcArchetype(p.Character))
                    throw new McpError(p.Character.name + " is not an NPC archetype (it has no npc_reference and spawn_npc pins).");
                FunctionEntity packaged = CharacterSlot(p.Prefab, composite, commands);
                if (packaged == null)
                    throw new McpError(composite.name + " has no '" + p.Prefab.Catalog.CharacterEntity + "' NPC in it any more, so its character cannot be changed.");
                if (packaged.function != p.Character.shortGUID)
                {
                    string variantName = VariantName(composite.name, p.Character);
                    Composite variant = commands.GetComposite(variantName);
                    if (variant == null)
                    {
                        CompositeRefactoring.PrepareToDuplicate(commands, composite);
                        DuplicateCompositePlan plan = DuplicateCompositePlan.Plan(commands, composite, variantName);
                        if (!plan.CanApply)
                            throw new McpError("A copy of " + composite.name + " for " + p.Character.name + " cannot be made: " + string.Join(" ", plan.Issues.Where(o => o.Blocking).Select(o => o.Message)));
                        RefactorEdit copy = CompositeRefactoring.DuplicateEdit(plan, p.LabelPrefix + "Insert Prefab: copy " + EditorUtils.GetCompositeName(composite) + " for " + McpScript.CompositeLeaf(p.Character));
                        UndoStack.Current.Apply(copy);
                        variant = copy.Result?.CreatedComposite;
                        if (variant == null)
                            throw new McpError("The copy of " + composite.name + " for " + p.Character.name + " was not made.");
                        placed.VariantMade = variant;
                        placed.Notes.Add("Made " + variant.name + ": a copy of " + composite.name + " whose " + p.Prefab.Catalog.CharacterEntity + " is " + p.Character.name + " (its own undo step). Later placements for that archetype reuse it.");
                    }
                    slot = CharacterSlot(p.Prefab, variant, commands);
                    if (slot == null)
                        throw new McpError(variant.name + " has no '" + p.Prefab.Catalog.CharacterEntity + "' NPC in it.");
                    repoint = slot.function != p.Character.shortGUID;
                    composite = variant;
                }
            }
            placed.Composite = composite;

            Composite target = composite;
            string label = "Insert Prefab: " + p.Prefab.Name + (target != InLevel(p.Prefab, commands) ? " (" + McpScript.CompositeLeaf(p.Character) + ")" : "");
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run(label, p.Into, edit =>
            {
                if (repoint)
                    Repoint(edit, target, slot, p.Character, placed.Notes);

                FunctionEntity instance = edit.AddInstance(p.Into, target, p.Name);
                edit.SetParameter(p.Into, instance, "position", new JObject()
                {
                    ["position"] = new JArray(p.Position.X, p.Position.Y, p.Position.Z),
                    ["rotation"] = new JArray(p.Rotation.X, p.Rotation.Y, p.Rotation.Z),
                });
                if (p.Pins != null)
                {
                    foreach (JProperty pin in p.Pins.Properties())
                    {
                        if (!target.variables.Any(o => ShortGuidUtils.FindString(o.name) == pin.Name))
                            throw new McpError(p.Prefab.Name + " has no pin '" + pin.Name + "'. Its input pins: " + string.Join(", ", InputPins(commands, target)) + ".");
                        edit.SetParameter(p.Into, instance, pin.Name, pin.Value);
                    }
                }
                if (p.BoxHalfDimensions != null)
                {
                    string boxName = p.Prefab.Catalog?.BoxEntity ?? throw new McpError(p.Prefab.Name + " has no box to size.");
                    Entity box = target.GetEntities().FirstOrDefault(o => string.Equals(commands.Utils.GetEntityName(target, o), boxName, StringComparison.OrdinalIgnoreCase))
                        ?? throw new McpError(target.name + " has no '" + boxName + "' in it any more.");
                    Vector3 size = p.BoxHalfDimensions.Value;
                    if (size.X <= 0 || size.Y <= 0 || size.Z <= 0)
                        throw new McpError("A box's half sizes must all be above zero.");
                    //Retail's per-placement override: an alias in the placing composite, on the box inside this instance
                    AliasEntity alias = edit.FindOrAddAlias(p.Into, new[] { instance.shortGUID, box.shortGUID, ShortGuid.Invalid }, commands.Utils.GetEntityName(p.Into, instance) + "_" + boxName, out bool _);
                    edit.SetParameter(p.Into, alias, "half_dimensions", new JArray(size.X, size.Y, size.Z));
                    placed.BoxAlias = alias;
                }
                if (p.Page != null || p.NodeAt != null)
                    edit.PlaceNode(instance, p.Page, p.NodeAt);
                edit.Select.Clear();
                edit.Select.Add(instance);
                placed.Instance = instance;
            }, new McpScriptEdit.RunOptions() { Show = p.Show, LabelPrefix = p.LabelPrefix, AlsoTouches = repoint ? new[] { target } : null });
            placed.Notes.AddRange(outcome.Notes);
            return placed;
        }

        /// <summary>The copy's NPC becomes the archetype: links to pins the archetype does not have go (named in the notes).</summary>
        private static void Repoint(McpScriptEdit edit, Composite composite, FunctionEntity slot, Composite archetype, List<string> notes)
        {
            Commands commands = edit.Commands;
            HashSet<ShortGuid> pins = new HashSet<ShortGuid>(archetype.variables.Select(o => o.name));
            edit.Touch(composite, slot);
            slot.function = archetype.shortGUID;
            List<string> dropped = new List<string>();
            foreach (Entity entity in composite.GetEntities().ToList())
            {
                foreach (EntityConnector link in entity.childLinks.ToList())
                {
                    bool outOfSlot = entity == slot && !pins.Contains(link.thisParamID);
                    bool intoSlot = link.linkedEntityID == slot.shortGUID && !pins.Contains(link.linkedParamID);
                    if (!outOfSlot && !intoSlot)
                        continue;
                    Entity other = composite.GetEntityByID(link.linkedEntityID);
                    if (other == null)
                        continue;
                    edit.RemoveLinks(composite, entity, ShortGuidUtils.FindString(link.thisParamID), other, ShortGuidUtils.FindString(link.linkedParamID));
                    dropped.Add(commands.Utils.GetEntityName(composite, entity) + "." + ShortGuidUtils.FindString(link.thisParamID) + " -> " + commands.Utils.GetEntityName(composite, other) + "." + ShortGuidUtils.FindString(link.linkedParamID));
                }
            }
            edit.Changed(composite, links: true);
            if (dropped.Count != 0)
                notes.Add(archetype.name + " has no pin for " + dropped.Count + " link(s) the prefab's NPC had, so they were left out of the copy: " + string.Join("; ", dropped.Take(8)) + (dropped.Count > 8 ? "; ..." : "") + ".");
        }

        /// <summary>A composite's input pins (the ones an instance holds a value for), by name.</summary>
        public static List<string> InputPins(Commands commands, Composite composite)
        {
            List<string> names = new List<string>();
            foreach (VariableEntity variable in composite.variables)
            {
                CompositePinInfoTable.PinInfo info = commands.Utils.GetPinInfo(composite, variable);
                if (info != null && ((CompositePinType)info.PinTypeGUID.AsUInt32).HoldsValue() && !IsOutput((CompositePinType)info.PinTypeGUID.AsUInt32))
                    names.Add(ShortGuidUtils.FindString(variable.name));
            }
            return names;
        }

        /// <summary>A composite's pins by kind, as Insert Prefab and list_prefabs describe them.</summary>
        public static List<(string Name, string Kind)> Pins(Commands commands, Composite composite)
        {
            List<(string, string)> pins = new List<(string, string)>();
            foreach (VariableEntity variable in composite.variables)
            {
                CompositePinInfoTable.PinInfo info = commands.Utils.GetPinInfo(composite, variable);
                CompositePinType type = info == null ? CompositePinType.CompositeInputVariablePin : (CompositePinType)info.PinTypeGUID.AsUInt32;
                string kind;
                switch (type)
                {
                    case CompositePinType.CompositeMethodPin: kind = "method"; break;
                    case CompositePinType.CompositeTargetPin: kind = "event"; break;
                    case CompositePinType.CompositeReferencePin: kind = "reference"; break;
                    default: kind = (IsOutput(type) ? "output " : "input ") + type.GetDataType().ToUIString().ToLowerInvariant(); break;
                }
                pins.Add((ShortGuidUtils.FindString(variable.name), kind));
            }
            return pins;
        }

        private static bool IsOutput(CompositePinType type) => type.ToString().StartsWith("CompositeOutput", StringComparison.Ordinal);
    }
}
