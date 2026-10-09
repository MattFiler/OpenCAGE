using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CathodeLib.NavMesh;
using Newtonsoft.Json.Linq;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Anim = CathodeLib.Animation;

namespace OpenCAGE.MCP
{
    /// <summary>NPCs: spawning characters, patrols and their effective settings.</summary>
    /// <remarks>
    /// <para>
    /// Retail places an NPC as an instance of an archetype (Archetypes\NPCs\Android\Android_NPC, ...\Human\Civilian\Worker_NPC_Pistol,
    /// ...\Alien\Xenomorph_NPC), mostly in mission composites. The archetype wraps Archetypes\Misc_Subarchetypes\NPC_Logic, whose
    /// Character holds what the NPC is (character_class, alliance_group, attribute_set, display_model, anim_set). The archetype
    /// exposes some of those as pins and fixes others inside itself: an Android_NPC's class and alliance come from VariableEnums
    /// its NPC_Logic reads. Every archetype has spawn_on_reset, the spawn_npc / despawn_npc methods, the finished_spawning relay
    /// and an npc_reference pin standing for its Character.
    /// </para>
    /// <para>
    /// Commands (CMD_GoTo, CMD_Idle, CMD_PlayAnimation) have no character pin: they act on the character of the trigger that
    /// starts them. Retail binds one with a TriggerBindCharacter (characters -> the NPC's npc_reference, bound_trigger -> the
    /// command's apply_start), and chains commands on their own events, which carry the character on (CMD_GoTo.succeeded ->
    /// CMD_Idle.apply_start, CMD_Idle.finished -> the next CMD_GoTo.apply_start). A CMD_GoTo's Waypoint and a CMD_PlayAnimation's
    /// Marker point at PositionMarkers. Counted on vanilla SCI_AndroidLab: 130 TriggerBindCharacter links into NPCs' npc_reference,
    /// 93 bound_trigger -> CMD_PlayAnimation.apply_start, 48 -> CMD_GoTo.apply_start, 79 Waypoint and 189 Marker links to
    /// PositionMarkers, 53 CMD_GoTo.succeeded -> CMD_Idle.apply_start and 18 CMD_Idle.finished -> CMD_GoTo.apply_start. Friendly
    /// NPCs are on alliance_group PLAYER_ALLY (Samuels; CHR_SetAlliance on a friendly android).
    /// </para>
    /// </remarks>
    internal static class McpCharacterTools
    {
        #region Archetypes
        /// <summary>A retail NPC archetype: what spawn_npc calls it, where retail levels have it, and what it needs.</summary>
        private sealed class Archetype
        {
            public string Kind;
            public string Path;
            public string Description;
            public string[] Levels;
            /// <summary>The sound banks its voice, foley and weapon come from (retail levels with it list these in SOUNDLOADZONES).</summary>
            public string[] Banks;
            /// <summary>Its NAVIGATION_CHARACTER_CLASS (0 player, 1 alien, 2 android, 3 human NPC, 4 facehugger).</summary>
            public int NavClass;
        }

        private static readonly Archetype[] Archetypes =
        {
            new Archetype() { Kind = "android", Path = @"Archetypes\NPCs\Android\Android_NPC", Description = "Working Joe android", Levels = new[] { "PRODUCTION/SCI_ANDROIDLAB", "PRODUCTION/TECH_COMMS", "PRODUCTION/ENG_REACTORCORE" }, Banks = new[] { "Android", "Human_Android_Shared" }, NavClass = 2 },
            new Archetype() { Kind = "android_heavy", Path = @"Archetypes\NPCs\Android\Android_Heavy_NPC", Description = "heavy android", Levels = new[] { "PRODUCTION/ENG_REACTORCORE", "PRODUCTION/TECH_MUTHRCORE" }, Banks = new[] { "Android", "Human_Android_Shared" }, NavClass = 2 },
            new Archetype() { Kind = "human", Path = @"Archetypes\NPCs\Human\Civilian\Worker_NPC_Pistol", Description = "civilian survivor with a pistol", Levels = new[] { "PRODUCTION/SCI_ANDROIDLAB", "PRODUCTION/TECH_HUB", "PRODUCTION/HAB_AIRPORT" }, Banks = new[] { "Human", "Human_Android_Shared", "Pistol" }, NavClass = 3 },
            new Archetype() { Kind = "human_shotgun", Path = @"Archetypes\NPCs\Human\Civilian\Worker_NPC_Shotgun", Description = "civilian survivor with a shotgun", Levels = new[] { "PRODUCTION/SCI_ANDROIDLAB", "PRODUCTION/TECH_HUB", "PRODUCTION/HAB_AIRPORT" }, Banks = new[] { "Human", "Human_Android_Shared", "Shotgun" }, NavClass = 3 },
            new Archetype() { Kind = "human_unarmed", Path = @"Archetypes\NPCs\Human\Civilian\Worker_NPC", Description = "unarmed civilian (attribute set INNOCENT)", Levels = new[] { "PRODUCTION/HAB_AIRPORT", "PRODUCTION/TECH_HUB", "PRODUCTION/HAB_SHOPPINGCENTRE" }, Banks = new[] { "Human", "Human_Android_Shared" }, NavClass = 3 },
            new Archetype() { Kind = "security", Path = @"Archetypes\NPCs\Human\Security\Grunt_NPC_Pistol", Description = "security guard with a pistol", Levels = new[] { "PRODUCTION/SCI_ANDROIDLAB", "PRODUCTION/SCI_HUB", "PRODUCTION/TECH_COMMS" }, Banks = new[] { "Human", "Human_Android_Shared", "Pistol" }, NavClass = 3 },
            new Archetype() { Kind = "security_shotgun", Path = @"Archetypes\NPCs\Human\Security\Grunt_NPC_Shotgun", Description = "security guard with a shotgun", Levels = new[] { "PRODUCTION/TECH_COMMS" }, Banks = new[] { "Human", "Human_Android_Shared", "Shotgun" }, NavClass = 3 },
            new Archetype() { Kind = "riot", Path = @"Archetypes\NPCs\Human\Riot\Riot_NPC_Pistol", Description = "rioter with a pistol", Levels = new[] { "PRODUCTION/TECH_COMMS" }, Banks = new[] { "Human", "Human_Android_Shared", "Pistol" }, NavClass = 3 },
            new Archetype() { Kind = "riot_shotgun", Path = @"Archetypes\NPCs\Human\Riot\Riot_NPC_Shotgun", Description = "rioter with a shotgun", Levels = new[] { "PRODUCTION/TECH_COMMS" }, Banks = new[] { "Human", "Human_Android_Shared", "Shotgun" }, NavClass = 3 },
            new Archetype() { Kind = "alien", Path = @"Archetypes\NPCs\Alien\Xenomorph_NPC", Description = "the alien", Levels = new[] { "PRODUCTION/SCI_ANDROIDLAB", "PRODUCTION/ENG_ALIEN_NEST", "PRODUCTION/TECH_HUB" }, Banks = new[] { "Alien" }, NavClass = 1 },
            new Archetype() { Kind = "facehugger", Path = @"Archetypes\NPCs\Alien\Facehugger", Description = "facehugger", Levels = new[] { "PRODUCTION/ENG_ALIEN_NEST", "PRODUCTION/ENG_TOWPLATFORM" }, Banks = new[] { "Alien" }, NavClass = 4 },
        };

        private static string KindList => string.Join(", ", Archetypes.Select(o => o.Kind));

        private static readonly ShortGuid NpcReference = ShortGuidUtils.Generate("npc_reference");
        private static readonly ShortGuid SpawnNpc = ShortGuidUtils.Generate("spawn_npc");
        private static readonly ShortGuid DespawnNpc = ShortGuidUtils.Generate("despawn_npc");
        private static readonly ShortGuid InitialValue = ShortGuidUtils.Generate("initial_value");
        private static readonly ShortGuid IsPlayer = ShortGuidUtils.Generate("is_player");
        private static readonly ShortGuid Characters = ShortGuidUtils.Generate("characters");
        private static readonly ShortGuid BoundTrigger = ShortGuidUtils.Generate("bound_trigger");
        private static readonly ShortGuid NpcLogic = ShortGuidUtils.Generate("NPC_Logic");

        /// <summary>A composite that is an NPC archetype: it has the npc_reference and spawn_npc pins every retail NPC has.</summary>
        internal static bool IsNpcArchetype(Composite composite) =>
            composite != null && composite.variables.Any(o => o.name == NpcReference) && composite.variables.Any(o => o.name == SpawnNpc);

        private static bool IsCharacter(Entity entity) => entity is FunctionEntity function && function.function == FunctionType.Character;

        private static Archetype Known(Composite composite) =>
            composite == null ? null : Archetypes.FirstOrDefault(o => string.Equals(McpScript.NormalisePath(o.Path), McpScript.NormalisePath(composite.name), StringComparison.OrdinalIgnoreCase));

        /// <summary>What an NPC is in a word: the catalogue's kind, else from its archetype's path.</summary>
        private static string KindOf(Composite archetype, Entity character, Commands commands)
        {
            Archetype known = Known(archetype);
            if (known != null) return known.Kind;
            if (archetype == null) return (character?.GetParameter(IsPlayer)?.content as cBool)?.value == true ? "player" : "character";
            string path = archetype.name.ToLowerInvariant();
            if (path.Contains("\\actors\\")) return "actor";
            if (path.Contains("android")) return "android";
            if (path.Contains("alien") || path.Contains("xeno")) return "alien";
            if (path.Contains("human")) return "human";
            return "npc";
        }

        /// <summary>
        /// The archetype a spawn_npc 'kind' names in the open level: a catalogue kind, or any NPC archetype by path, id or name.
        /// Refuses one the level does not have, naming a level to port it from. UI thread.
        /// </summary>
        private static Composite FindArchetype(Commands commands, string kind, out Archetype known)
        {
            string wanted = (kind ?? "").Trim();
            if (wanted.Length == 0) throw McpError.Invalid("'kind' is required: " + KindList + ", or an NPC archetype's path or name.");
            List<Composite> present = commands.Entries.Where(o => o != null && IsNpcArchetype(o) && o.name.IndexOf("Misc_Subarchetypes", StringComparison.OrdinalIgnoreCase) < 0).ToList();
            string presentText = present.Count == 0 ? "none" : string.Join(", ", present.Select(o => o.name).OrderBy(o => o).Take(20));
            known = Archetypes.FirstOrDefault(o => string.Equals(o.Kind, wanted, StringComparison.OrdinalIgnoreCase) || string.Equals(McpNames.Leaf(McpScript.NormalisePath(o.Path)), wanted, StringComparison.OrdinalIgnoreCase)
                || string.Equals(McpScript.NormalisePath(o.Path), McpScript.NormalisePath(wanted), StringComparison.OrdinalIgnoreCase));
            if (known != null)
            {
                Archetype found = known;
                Composite inLevel = commands.Entries.FirstOrDefault(o => o != null && string.Equals(McpScript.NormalisePath(o.name), McpScript.NormalisePath(found.Path), StringComparison.OrdinalIgnoreCase));
                if (inLevel != null) return inLevel;
                throw new McpError(McpErrorCodes.NotFound, found.Path + " (" + found.Kind + ") is not in this level. Port it in first with port_composites {\"level\": \"" + found.Levels[0] + "\", \"composites\": [\"" + found.Path.Replace("\\", "\\\\") + "\"]} " +
                    "(not undoable; " + string.Join(", ", found.Levels) + " have it), then call spawn_npc again. NPC archetypes this level has: " + presentText + ".");
            }
            try
            {
                Composite composite = McpScript.FindComposite(commands, wanted);
                if (IsNpcArchetype(composite)) return composite;
                throw McpError.Invalid(composite.name + " is not an NPC archetype (it has no npc_reference and spawn_npc pins). 'kind' is " + KindList + ", or one of this level's NPC archetypes: " + presentText + ".");
            }
            catch (McpError error) when (error.Code == McpErrorCodes.NotFound)
            {
                List<string> names = Archetypes.Select(o => o.Kind).Concat(present.Select(o => McpScript.CompositeLeaf(o))).ToList();
                throw McpError.NotFound("NPC kind or archetype", wanted, names, "Kinds: " + KindList + ". NPC archetypes this level has: " + presentText + ". search_level finds others in another level (composites 'Archetypes NPCs'), and port_composites brings one in.");
            }
        }

        /// <summary>
        /// The Character an archetype's npc_reference pin stands for: the steps from the archetype's contents down to it, with the
        /// composite holding each step in <paramref name="comps"/> (the archetype first). Falls back to the first Character found.
        /// Empty when it holds none.
        /// </summary>
        internal static List<Entity> MainCharacter(Commands commands, Composite archetype, out List<Composite> comps)
        {
            comps = new List<Composite>() { archetype };
            List<Entity> steps = new List<Entity>();
            Composite at = archetype;
            ShortGuid pin = NpcReference;
            for (int depth = 0; depth < 8; depth++)
            {
                VariableEntity variable = at.variables.FirstOrDefault(o => o.name == pin);
                if (variable == null) break;
                FunctionEntity next = null;
                ShortGuid nextPin = ShortGuid.Invalid;
                foreach (EntityConnector link in variable.childLinks)
                    if (at.GetEntityByID(link.linkedEntityID) is FunctionEntity linked) { next = linked; nextPin = link.linkedParamID; break; }
                if (next == null) break;
                if (next.function.IsFunctionType)
                {
                    if (next.function == FunctionType.Character)
                    {
                        steps.Add(next);
                        return steps;
                    }
                    break;
                }
                Composite inner = commands.GetComposite(next.function);
                if (inner == null || comps.Contains(inner)) break;
                steps.Add(next);
                comps.Add(inner);
                at = inner;
                pin = nextPin;
            }

            //Breadth first: the first Character, through instances that are not templates
            comps = new List<Composite>() { archetype };
            Queue<Tuple<List<Entity>, List<Composite>>> pending = new Queue<Tuple<List<Entity>, List<Composite>>>();
            pending.Enqueue(Tuple.Create(new List<Entity>(), new List<Composite>() { archetype }));
            while (pending.Count != 0)
            {
                Tuple<List<Entity>, List<Composite>> here = pending.Dequeue();
                Composite composite = here.Item2[here.Item2.Count - 1];
                FunctionEntity character = composite.functions.FirstOrDefault(o => o.function == FunctionType.Character);
                if (character != null)
                {
                    comps = here.Item2;
                    return here.Item1.Concat(new[] { (Entity)character }).ToList();
                }
                if (here.Item2.Count > 5) continue;
                foreach (FunctionEntity function in composite.functions)
                {
                    if (function.function.IsFunctionType || (function.GetParameter("is_template")?.content as cBool)?.value == true) continue;
                    Composite inner = commands.GetComposite(function.function);
                    if (inner == null || here.Item2.Contains(inner)) continue;
                    pending.Enqueue(Tuple.Create(here.Item1.Concat(new[] { (Entity)function }).ToList(), here.Item2.Concat(new[] { inner }).ToList()));
                }
            }
            comps = new List<Composite>();
            return new List<Entity>();
        }
        #endregion

        #region NPCs
        /// <summary>One NPC at one placement: the archetype instance (or a Character placed on its own), and the Character it runs on.</summary>
        internal sealed class Npc
        {
            /// <summary>From the start composite (the root, for a placement in the level) to the NPC entity.</summary>
            public List<Entity> Chain;
            /// <summary>Comps[i] holds Chain[i].</summary>
            public List<Composite> Comps;
            /// <summary>The composite placed; null for a Character placed on its own.</summary>
            public Composite Archetype;
            /// <summary>From the archetype's contents down to its Character; InnerComps[i] holds Inner[i]. Empty for a Character on its own.</summary>
            public List<Entity> Inner = new List<Entity>();
            public List<Composite> InnerComps = new List<Composite>();
            public cTransform World;
            public bool Placed;
            public bool Real = true;

            public Entity Entity => Chain[Chain.Count - 1];
            public Composite Holder => Comps[Comps.Count - 1];
            public int Level => Chain.Count - 1;
            public List<Entity> CharacterChain => Chain.Concat(Inner).ToList();
            public List<Composite> CharacterComps => Comps.Concat(InnerComps).ToList();
            public Entity Character => Inner.Count != 0 ? Inner[Inner.Count - 1] : (IsCharacter(Entity) ? Entity : null);
        }

        /// <summary>
        /// An NPC from a chain of entities from <paramref name="start"/>. A chain into an archetype (to its Character, say) is cut
        /// at the outermost archetype instance: that is the NPC, what the script places and spawns. Null when it is not an NPC.
        /// </summary>
        private static Npc NpcOf(Commands commands, Composite start, List<Entity> chain, McpPlacements.Placement placement)
        {
            List<Composite> along = McpScript.CompositesAlong(commands, start, chain);
            for (int i = 0; i < chain.Count; i++)
            {
                Composite placed = along[i + 1];
                if (placed != null && IsNpcArchetype(placed))
                {
                    Npc npc = new Npc() { Chain = chain.Take(i + 1).ToList(), Comps = along.Take(i + 1).ToList(), Archetype = placed };
                    if (i + 1 < chain.Count && IsCharacter(chain[chain.Count - 1]))
                    {
                        npc.Inner = chain.Skip(i + 1).ToList();
                        npc.InnerComps = along.Skip(i + 1).Take(npc.Inner.Count).ToList();
                    }
                    else
                    {
                        npc.Inner = MainCharacter(commands, placed, out List<Composite> comps);
                        npc.InnerComps = npc.Inner.Count == 0 ? new List<Composite>() : comps;
                    }
                    Finish(npc, placement, i == chain.Count - 1);
                    return npc;
                }
            }
            if (chain.Count != 0 && IsCharacter(chain[chain.Count - 1]))
            {
                Npc npc = new Npc() { Chain = chain, Comps = along.Take(chain.Count).ToList() };
                Finish(npc, placement, true);
                return npc;
            }
            return null;
        }

        private static void Finish(Npc npc, McpPlacements.Placement placement, bool placementIsNpc)
        {
            if (placement == null) return;
            npc.Placed = true;
            npc.Real = placement.Real;
            //A placement of something deeper is the NPC's when the chain was cut: its position is the instance's
            npc.World = placementIsNpc ? placement.World : null;
        }

        /// <summary>
        /// The NPC(s) a call names: 'npc' (a path from the root, one placement), or composite + entity (every placement of it in the
        /// level; the entity alone when the composite is not placed). UI thread.
        /// </summary>
        private static List<Npc> ReadNpcs(McpCall call, Commands commands, string npcArgument = "npc", int limit = 200)
        {
            Composite root = commands.EntryPoints[0];
            McpPlacements walker = McpRegion.Walker(commands);
            if (call.Has(npcArgument))
            {
                if (call.Has("entity")) throw McpError.Invalid("Give '" + npcArgument + "' (a path from the root) or composite + entity, not both.");
                List<Entity> chain = McpScript.ChainFrom(commands, root, McpScript.PathSteps(call.Token(npcArgument), npcArgument));
                McpPlacements.Placement placement = walker.Evaluate(root, chain);
                Npc npc = NpcOf(commands, root, chain, placement);
                if (npc == null)
                {
                    Entity last = chain[chain.Count - 1];
                    Composite holder = McpScript.CompositesAlong(commands, root, chain)[chain.Count - 1];
                    throw McpError.Invalid(McpScript.EntityName(commands, holder, last) + " is a " + McpScript.TypeName(commands, holder, last) + ", not an NPC: give the path to an NPC archetype's instance (an Android_NPC, say) or a Character. describe_npc lists the level's NPCs with their paths.");
                }
                if (npc.World == null && npc.Chain.Count != chain.Count)
                    npc.World = walker.Evaluate(root, npc.Chain).World;
                return new List<Npc>() { npc };
            }
            if (!call.Has("composite") || !call.Has("entity"))
                throw McpError.Invalid("Say which NPC: '" + npcArgument + "' (a path from the root to its archetype instance or Character, as describe_npc lists it), or composite + entity.");
            Composite composite = McpScript.FindComposite(commands, call.Str("composite"));
            Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity"));
            List<McpPlacements.Placement> found = new List<McpPlacements.Placement>();
            int total = walker.PlacementsOf(root, composite, entity, found, limit, call.Cancel, realOnly: false);
            List<Npc> npcs = new List<Npc>();
            foreach (McpPlacements.Placement placement in found)
            {
                Npc npc = NpcOf(commands, root, placement.Chain, placement);
                if (npc != null && npc.Entity == entity) npcs.Add(npc);
            }
            if (npcs.Count == 0)
            {
                Npc alone = NpcOf(commands, composite, new List<Entity>() { entity }, null);
                if (alone == null)
                    throw McpError.Invalid(McpScript.EntityName(commands, composite, entity) + " is a " + McpScript.TypeName(commands, composite, entity) + ", not an NPC: give an NPC archetype's instance (an Android_NPC, say) or a Character. describe_npc lists the level's NPCs.");
                npcs.Add(alone);
                call.Note(composite.name + " is not placed under the level's root, so values passed in from above and the world position are unknown.");
            }
            else if (total > found.Count)
                call.Note(total + " placements; the first " + found.Count + " are used.");
            return npcs;
        }

        /// <summary>Every NPC of the level in a region (the whole level when null), walking only composites that hold one. UI thread.</summary>
        private static List<Npc> FindNpcs(McpCall call, Commands commands, McpRegion region, out bool truncated)
        {
            Composite root = commands.EntryPoints[0];
            Dictionary<Composite, bool> holds = new Dictionary<Composite, bool>();
            bool Holds(Composite composite)
            {
                if (composite == null) return false;
                if (holds.TryGetValue(composite, out bool known)) return known;
                holds[composite] = false;
                bool result = IsNpcArchetype(composite) || composite.functions.Any(o => o.function == FunctionType.Character);
                if (!result)
                    foreach (FunctionEntity function in composite.functions)
                        if (!function.function.IsFunctionType && Holds(commands.GetComposite(function.function))) { result = true; break; }
                holds[composite] = result;
                return result;
            }

            List<List<Entity>> roots = region?.Roots ?? new List<List<Entity>>();
            List<Npc> found = new List<Npc>();
            McpPlacements walker = McpRegion.Walker(commands);
            walker.Walk(root, step =>
            {
                if (!(step.Entity is FunctionEntity function)) return true;
                Composite placed = function.function.IsFunctionType ? null : commands.GetComposite(function.function);
                if (placed == null ? function.function != FunctionType.Character : !IsNpcArchetype(placed)) return true;
                List<Entity> chain = new List<Entity>(step.Chain);
                if (roots.Count != 0 && !roots.Any(o => o.Count <= chain.Count && !o.Where((e, i) => chain[i] != e).Any())) return true;
                if (region != null && (region.HasClip || region.Centre != null) && (step.World == null || !region.InClip(step.World.position))) return true;
                Npc npc = NpcOf(commands, root, chain, McpPlacements.Placement.Of(step));
                if (npc != null) found.Add(npc);
                return true;
            }, child => !IsNpcArchetype(child) && Holds(child), call.Cancel);
            truncated = walker.Truncated;
            return found;
        }

        /// <summary>The path of an NPC as results give it (from the root when placed).</summary>
        private static JObject PathOf(Commands commands, Npc npc)
        {
            if (npc.Comps[0] == commands.EntryPoints[0])
                return McpRegion.ChainJson(commands, npc.Chain);
            return new JObject()
            {
                ["composite"] = npc.Holder.name,
                ["ids"] = new JArray(McpScript.Id(npc.Entity.shortGUID)),
            };
        }
        #endregion

        #region Settings
        /// <summary>The Character settings describe_npc resolves and set_npc changes.</summary>
        private static readonly string[] ProfileSettings = { "character_class", "alliance_group", "attribute_set", "display_model", "anim_set", "anim_tree_set", "reference_skeleton", "spawn_on_reset" };

        /// <summary>Other names callers use for a setting (NPC_Logic's own pin is spelt aliance_group).</summary>
        private static string SettingName(string name)
        {
            string wanted = (name ?? "").Trim();
            switch (wanted.ToLowerInvariant())
            {
                case "alliance":
                case "aliance_group":
                case "alliance_group":
                    return "alliance_group";
                case "class":
                    return "character_class";
                case "attributes":
                case "attribute":
                    return "attribute_set";
                case "model":
                    return "display_model";
            }
            return wanted;
        }

        /// <summary>One setting of an NPC as the game resolves it for a placement, and where the value is held.</summary>
        internal sealed class Setting
        {
            public string Name;
            public JToken Value;
            /// <summary>alias (an alias further out overrides it), own (set where it is held), default, runtime (another entity feeds it as the game runs).</summary>
            public string Source;
            public List<string> Route = new List<string>();
            /// <summary>own / default: the chain (from the start composite) to the entity holding it, and its parameter there.</summary>
            public List<Entity> Holder;
            public string Parameter;
            /// <summary>alias: the alias overriding it, in AliasOwner.</summary>
            public AliasEntity Alias;
            public Composite AliasOwner;
            /// <summary>runtime: what feeds it.</summary>
            public string RuntimeBy;
        }

        /// <summary>The Variable* functions (VariableEnum, VariableString...) hold a value in initial_value that links from it read.</summary>
        private static bool IsValueVariable(Entity entity) =>
            entity is FunctionEntity function && function.function.IsFunctionType && function.function.AsFunctionType.ToString().StartsWith("Variable", StringComparison.Ordinal)
            && function.function != FunctionType.VariableThePlayer;

        /// <summary>The aliases reaching chain[k] (comps[j] holding one whose path is chain[j..k]), outermost first.</summary>
        private static List<(Composite owner, AliasEntity alias)> AliasesOf(List<Composite> comps, List<Entity> chain, int k)
        {
            List<(Composite, AliasEntity)> found = new List<(Composite, AliasEntity)>();
            for (int j = 0; j <= k; j++)
            {
                int length = k - j + 1;
                foreach (AliasEntity alias in comps[j].aliases)
                {
                    ShortGuid[] path = alias.alias?.path;
                    if (path == null) continue;
                    int stored = path.Length > 0 && path[path.Length - 1] == ShortGuid.Invalid ? path.Length - 1 : path.Length;
                    if (stored != length) continue;
                    bool same = true;
                    for (int i = 0; i < length && same; i++)
                        same = path[i] == chain[j + i].shortGUID;
                    if (same) found.Add((comps[j], alias));
                }
            }
            return found;
        }

        /// <summary>
        /// A parameter of chain[k] as instancing gives it to this placement: a link out of the parameter feeds it (a composite pin,
        /// followed up into the instance that places the composite; a Variable function's initial_value; or another entity, read
        /// as the game runs), else the outermost alias's value, its own value, or the default. Also where the value is held, so it
        /// can be changed there.
        /// </summary>
        internal static Setting Resolve(Commands commands, List<Composite> comps, List<Entity> chain, int k, ShortGuid parameter, Setting into = null, int depth = 0)
        {
            Setting setting = into ?? new Setting() { Name = McpScript.ParamName(parameter) };
            Composite composite = comps[k];
            Entity entity = chain[k];
            string entityName = McpScript.EntityName(commands, composite, entity);
            if (depth < 24)
            {
                //A link out of the parameter (the last one wins over any value), or a composite pin linking into it
                EntityConnector? feed = null;
                foreach (EntityConnector link in entity.childLinks)
                    if (link.thisParamID == parameter && composite.GetEntityByID(link.linkedEntityID) != null)
                        feed = link;
                VariableEntity pin = null;
                if (feed != null)
                    pin = composite.GetEntityByID(feed.Value.linkedEntityID) as VariableEntity;
                else
                    pin = composite.variables.FirstOrDefault(o => o.childLinks.Any(l => l.linkedEntityID == entity.shortGUID && l.linkedParamID == parameter));
                if (pin != null)
                {
                    string pinName = ShortGuidUtils.FindString(pin.name);
                    setting.Route.Add(entityName + "." + McpScript.ParamName(parameter) + " is " + McpScript.CompositeLeaf(composite) + "'s pin '" + pinName + "'");
                    if (k > 0)
                        return Resolve(commands, comps, chain, k - 1, pin.name, setting, depth + 1);
                    ParameterData fallback = pin.GetParameter(pin.name)?.content;
                    setting.Value = fallback == null ? null : McpValues.ToJson(fallback, commands);
                    setting.Source = "default";
                    setting.Holder = new List<Entity>() { pin };
                    setting.Parameter = pinName;
                    setting.Route.Add("the pin's own default (nothing places " + McpScript.CompositeLeaf(composite) + " here)");
                    return setting;
                }
                if (feed != null)
                {
                    Entity other = composite.GetEntityByID(feed.Value.linkedEntityID);
                    if (IsValueVariable(other))
                    {
                        setting.Route.Add(entityName + "." + McpScript.ParamName(parameter) + " reads " + McpScript.TypeName(commands, composite, other) + " '" + McpScript.EntityName(commands, composite, other) + "' in " + McpScript.CompositeLeaf(composite));
                        List<Entity> variableChain = chain.Take(k).Concat(new[] { other }).ToList();
                        return Resolve(commands, comps, variableChain, k, InitialValue, setting, depth + 1);
                    }
                    string by = McpScript.EntityName(commands, composite, other) + "." + McpScript.ParamName(feed.Value.linkedParamID);
                    ParameterData current = other.GetParameter(feed.Value.linkedParamID)?.content;
                    setting.Value = current == null ? null : McpValues.ToJson(current, commands);
                    setting.Source = "runtime";
                    setting.RuntimeBy = by + " in " + composite.name;
                    setting.Route.Add("read as the game runs from " + by + " in " + McpScript.CompositeLeaf(composite));
                    return setting;
                }
            }

            //Values: the outermost alias's, else its own, else the default
            foreach ((Composite owner, AliasEntity alias) in AliasesOf(comps, chain, k))
            {
                ParameterData overriding = alias.GetParameter(parameter)?.content;
                if (overriding == null) continue;
                setting.Value = McpValues.ToJson(overriding, commands);
                setting.Source = "alias";
                setting.Alias = alias;
                setting.AliasOwner = owner;
                setting.Parameter = McpScript.ParamName(parameter);
                setting.Route.Add("overridden by an alias in " + owner.name + " (" + McpScript.Id(alias.shortGUID) + ")");
                return setting;
            }
            setting.Holder = chain.Take(k + 1).ToList();
            setting.Parameter = McpScript.ParamName(parameter);
            ParameterData own = entity.GetParameter(parameter)?.content;
            if (own != null)
            {
                setting.Value = McpValues.ToJson(own, commands);
                setting.Source = "own";
                setting.Route.Add("set on " + entityName + " in " + McpScript.CompositeLeaf(composite));
                return setting;
            }
            ParameterData standard = null;
            try { standard = commands.Utils.CreateDefaultParameterData(entity, composite, parameter); } catch (Exception) { }
            setting.Value = standard == null ? null : McpValues.ToJson(standard, commands);
            setting.Source = "default";
            Composite instanced = McpScript.InstancedComposite(commands, entity);
            setting.Route.Add(standard == null ? "not set anywhere" : instanced != null ? "not set on " + entityName + ": " + McpScript.CompositeLeaf(instanced) + "'s default" : "not set: the " + McpScript.TypeName(commands, composite, entity) + " default");
            return setting;
        }

        /// <summary>A setting of an NPC's Character, resolved for its placement. Null when the NPC holds no Character.</summary>
        private static Setting SettingOf(Commands commands, Npc npc, string name)
        {
            if (npc.Character == null) return null;
            List<Entity> chain = npc.CharacterChain;
            return Resolve(commands, npc.CharacterComps, chain, chain.Count - 1, ShortGuidUtils.Generate(name));
        }

        /// <summary>Where one setting is written so it holds for an NPC whose instance is the last of its chain: planned before an edit, made in it.</summary>
        private sealed class Write
        {
            public string Setting;
            public JToken Value;
            public JToken Before;
            /// <summary>instance (the NPC instance's own pin), outer (a value passed in from further out), alias (an override reaching into the archetype), existing_alias.</summary>
            public string Kind;
            public Composite Composite;
            public Entity Entity;
            public ShortGuid[] AliasPath;
            public string Parameter;
            public string Problem;
            public List<string> Route;
            /// <summary>The entities an alias would go through (from Composite), each with the composite holding it, for naming.</summary>
            public List<(Composite composite, Entity entity)> Steps;
            /// <summary>What to call the NPC instance when it is not made yet (a dry run).</summary>
            public string NewName;

            public string Describe(Commands commands)
            {
                if (Problem != null) return Problem;
                switch (Kind)
                {
                    case "instance":
                    case "outer":
                        return (NewName ?? McpScript.EntityName(commands, Composite, Entity)) + "." + Parameter + " in " + Composite.name + (Kind == "outer" ? " (the value is passed in from this placement further out)" : " (the instance's own pin)");
                    case "existing_alias":
                        return "the alias " + McpScript.Id(Entity.shortGUID) + " in " + Composite.name + " that already overrides it (." + Parameter + ")";
                    default:
                        return "an alias override in " + Composite.name + " on " + string.Join(" > ", Steps.Select((o, i) => i == 0 && NewName != null ? NewName : McpScript.EntityName(commands, o.composite, o.entity))) + "." + Parameter;
                }
            }
        }

        /// <summary>An NPC as a new instance of an archetype in a composite would be (or is, once made): for planning and making its writes.</summary>
        private static Npc StandIn(Commands commands, Composite composite, Composite archetype, FunctionEntity instance)
        {
            Npc npc = new Npc() { Chain = new List<Entity>() { instance }, Comps = new List<Composite>() { composite }, Archetype = archetype };
            npc.Inner = MainCharacter(commands, archetype, out List<Composite> innerComps);
            npc.InnerComps = npc.Inner.Count == 0 ? new List<Composite>() : innerComps;
            return npc;
        }

        /// <summary>
        /// Where to write a setting so it holds for this NPC: where its value comes from, but no deeper than the NPC's own composite -
        /// a value held inside the archetype is overridden by an alias in the NPC's composite, so other NPCs of the archetype keep theirs.
        /// </summary>
        private static Write PlanWrite(Commands commands, Npc npc, string name, JToken value)
        {
            Write write = new Write() { Setting = name, Value = value };
            Setting resolved = SettingOf(commands, npc, name);
            if (resolved == null)
            {
                write.Problem = "it holds no Character to set " + name + " on";
                return write;
            }
            write.Before = resolved.Value;
            write.Route = resolved.Route;
            List<Composite> comps = npc.CharacterComps;
            int e = npc.Level;
            switch (resolved.Source)
            {
                case "runtime":
                    write.Problem = "it is set as the game runs, read from " + resolved.RuntimeBy + ": change that instead";
                    return write;
                case "alias":
                    write.Kind = "existing_alias";
                    write.Composite = resolved.AliasOwner;
                    write.Entity = resolved.Alias;
                    write.Parameter = resolved.Parameter;
                    return write;
            }
            List<Entity> holder = resolved.Holder;
            int m = holder.Count - 1;
            if (holder[m] is VariableEntity)
            {
                write.Problem = "it comes from " + comps[m].name + "'s pin '" + resolved.Parameter + "', which nothing places here";
                return write;
            }
            write.Parameter = resolved.Parameter;
            if (m <= e)
            {
                write.Kind = m == e ? "instance" : "outer";
                write.Composite = comps[m];
                write.Entity = holder[m];
                return write;
            }
            write.Kind = "alias";
            write.Composite = comps[e];
            write.AliasPath = holder.Skip(e).Select(o => o.shortGUID).Concat(new[] { ShortGuid.Invalid }).ToArray();
            write.Steps = holder.Select((o, i) => (comps[i], o)).Skip(e).ToList();
            return write;
        }

        /// <summary>An alias of a composite with exactly this path (ending in Invalid), or null.</summary>
        private static AliasEntity FindAlias(Composite composite, ShortGuid[] path)
        {
            int length = path.Length > 0 && path[path.Length - 1] == ShortGuid.Invalid ? path.Length - 1 : path.Length;
            foreach (AliasEntity alias in composite.aliases)
            {
                ShortGuid[] stored = alias.alias?.path;
                if (stored == null) continue;
                int count = stored.Length > 0 && stored[stored.Length - 1] == ShortGuid.Invalid ? stored.Length - 1 : stored.Length;
                if (count != length) continue;
                bool same = true;
                for (int i = 0; i < length && same; i++) same = stored[i] == path[i];
                if (same) return alias;
            }
            return null;
        }

        /// <summary>Make a planned write within an edit; returns what was written. An alias is reused or made.</summary>
        private static JToken ApplyWrite(McpScriptEdit edit, Write write)
        {
            Commands commands = edit.Commands;
            Entity target = write.Entity;
            if (target == null)
            {
                target = FindAlias(write.Composite, write.AliasPath);
                if (target == null) target = edit.AddAlias(write.Composite, write.AliasPath, null);
            }
            //An alias takes its override in the type of what it points at: seed it with that value, then write over it
            ShortGuid id = ShortGuidUtils.Generate(write.Parameter);
            if (target is AliasEntity alias && alias.GetParameter(id) == null)
            {
                (Composite pointedComposite, Entity pointed) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveAlias(alias, write.Composite));
                ParameterData current = pointed?.GetParameter(id)?.content;
                if (current != null)
                {
                    (ParameterVariant? variant, DataType? _, ShortGuid __) = commands.Utils.GetParameterMetadata(pointed, id, pointedComposite);
                    edit.Touch(write.Composite, alias);
                    alias.AddParameter(id, (ParameterData)current.Clone(), variant ?? ParameterVariant.PARAMETER);
                }
            }
            ParameterData written = edit.SetParameter(write.Composite, target, write.Parameter, write.Value, allowCustom: true);
            return McpValues.ToJson(written, commands);
        }

        /// <summary>Check a setting's value before writing it: a class config, an animation set, a display model. Returns a warning, or null.</summary>
        private static string CheckSetting(string name, JToken value)
        {
            string text = value != null && value.Type == JTokenType.String ? ((string)value).Trim() : null;
            if (text == null || text.Length == 0) return null;
            switch (name)
            {
                case "attribute_set":
                    {
                        Dictionary<string, string> trees = ClassTrees();
                        if (trees != null && !trees.Keys.Any(o => string.Equals(o, text, StringComparison.OrdinalIgnoreCase)))
                            return "attribute_set '" + text + "' is no character class config" + McpNames.DidYouMean(trees.Keys, text) + " (get_config_record kind 'attributes' lists them): the NPC would have no behaviour.";
                        return null;
                    }
                case "anim_set":
                    {
                        Anim.AnimationSet set = McpGlobalAssetChecks.FindSet(text);
                        Anim animations = Singleton.Animations;
                        if (set == null && animations != null && animations.Loaded)
                            return "anim_set '" + text + "' is not an animation set" + McpGlobalAssetChecks.DidYouMean(animations.Sets.Where(o => o.Kind != Anim.AnimationKind.Environment).Select(o => o.Name), text, " (list_animation_sets kind character lists them).");
                        if (set != null && set.Kind == Anim.AnimationKind.Environment)
                            return "anim_set '" + set.Name + "' is a prop's set: a Character needs a character set (list_animation_sets kind character).";
                        return null;
                    }
                case "display_model":
                    return UnknownEnumString(EnumStringType.DISPLAY_MODEL, text);
            }
            return null;
        }

        /// <summary>Why a value is not one the inspector's picker offers for an enum-string type, or null (also when there is no list).</summary>
        private static string UnknownEnumString(EnumStringType type, string value)
        {
            try
            {
                EnumStringListViewItems.PopulateGlobalEntries();
                EnumStringListViewItems.PopulateLevelSpecificEntries();
                Tuple<System.Windows.Forms.ListViewItem[], bool> items = EnumStringListViewItems.GetItems(type);
                if (items == null || items.Item1.Length == 0 || items.Item1.Any(o => string.Equals(o.Text, value, StringComparison.OrdinalIgnoreCase)))
                    return null;
                return "'" + value + "' is not a " + type + " value OpenCAGE knows" + McpNames.DidYouMean(items.Item1.Select(o => o.Text), value) + " (list_enum_string_values " + type + " lists them).";
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Character class config -> its behaviour tree (BehaviorTreeDB requirements), or null when the configs cannot be read.</summary>
        private static Dictionary<string, string> ClassTrees()
        {
            try
            {
                BehaviorTreeDB.Requirements requirements = BehaviourTreeLevels.Requirements;
                return requirements?.ClassTrees == null || requirements.ClassTrees.Count == 0 ? null : requirements.ClassTrees;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string TreeOf(Dictionary<string, string> trees, JToken attributeSet)
        {
            if (trees == null || attributeSet == null || attributeSet.Type != JTokenType.String) return null;
            string key = trees.Keys.FirstOrDefault(o => string.Equals(o, (string)attributeSet, StringComparison.OrdinalIgnoreCase));
            return key == null ? null : trees[key];
        }

        /// <summary>The settings a call gives, by Character parameter, with the names checked against the Character. UI thread.</summary>
        private static List<(string name, JToken value)> ReadSettings(Commands commands, JObject given, Npc npc, Composite archetype)
        {
            List<(string, JToken)> settings = new List<(string, JToken)>();
            if (given == null) return settings;
            Entity character = npc?.Character;
            Composite characterComposite = npc != null && npc.Inner.Count != 0 ? npc.InnerComps[npc.InnerComps.Count - 1] : npc?.Holder;
            if (character == null && archetype != null)
            {
                List<Entity> steps = MainCharacter(commands, archetype, out List<Composite> comps);
                if (steps.Count != 0) { character = steps[steps.Count - 1]; characterComposite = comps[comps.Count - 1]; }
            }
            List<string> known = character == null ? ProfileSettings.ToList() : commands.Utils.GetAllParameters(character, characterComposite).Select(o => McpScript.ParamName(o.Item1)).Distinct().ToList();
            foreach (JProperty property in given.Properties())
            {
                string name = SettingName(property.Name);
                if (!known.Any(o => string.Equals(o, name, StringComparison.OrdinalIgnoreCase)))
                    throw McpError.NotFound("Character setting", property.Name, known.Concat(new[] { "alliance_group" }), "Common ones: " + string.Join(", ", ProfileSettings) + ".");
                name = known.First(o => string.Equals(o, name, StringComparison.OrdinalIgnoreCase));
                if (settings.Any(o => o.Item1 == name)) throw McpError.Invalid("'" + name + "' is given twice in settings.");
                settings.Add((name, property.Value));
            }
            return settings;
        }
        #endregion

        #region Places
        /// <summary>A box round points, snapped outward to an 8 m grid so nearby calls share one cached collision soup.</summary>
        private static McpRegion SnapBox(IEnumerable<Vector3> points)
        {
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            foreach (Vector3 p in points) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            min -= new Vector3(2, 8, 2);
            max += new Vector3(2, 3, 2);
            Vector3 Down(Vector3 v) => new Vector3((float)Math.Floor(v.X / 8) * 8, (float)Math.Floor(v.Y / 8) * 8, (float)Math.Floor(v.Z / 8) * 8);
            Vector3 Up(Vector3 v) => new Vector3((float)Math.Ceiling(v.X / 8) * 8, (float)Math.Ceiling(v.Y / 8) * 8, (float)Math.Ceiling(v.Z / 8) * 8);
            return McpRegion.Box(Down(min), Up(max));
        }

        /// <summary>
        /// A point stood where a character can stand: on the walkable collision under it (live, unsaved edits included; from 0.5 m
        /// above it to 6 m below), and - when the level has a navmesh and that surface is not on it (a table top, say) - on the
        /// navmesh the class walks within 1.5 m across of the point given.
        /// </summary>
        private sealed class Floor
        {
            public Vector3 Given;
            public Vector3 Point;
            public bool Found;
            /// <summary>collision (the surface under it) or navmesh (the walkable navmesh beside it, as last built).</summary>
            public string On;
            public McpCollision.Record Record;
            /// <summary>When it moved onto the navmesh: the surface it was over first, which characters do not walk on.</summary>
            public McpCollision.Record Skipped;

            public JObject Describe(Commands commands)
            {
                if (!Found) return new JObject() { ["stood_on"] = "nothing: no walkable collision from 0.5 m above to 6 m below, so it was left where given" };
                JObject described = new JObject() { ["stood_on"] = On, ["moved_m"] = Math.Round(Vector3.Distance(Given, Point), 3) };
                if (Record != null) described["surface"] = McpCollision.Describe(commands, Record, brief: true);
                if (Skipped != null) described["not_on"] = McpCollision.Describe(commands, Skipped, brief: true) + " (the surface under the point given is off the navmesh)";
                return described;
            }
        }

        private static List<Floor> Floors(McpCall call, List<Vector3> points, NavigationMeshQuery query, int navClass)
        {
            List<Floor> floors = points.Select(o => new Floor() { Given = o, Point = o }).ToList();
            if (points.Count == 0) return floors;
            McpCollision.Soup soup = McpCollision.SoupFor(call, SnapBox(points), "walkable");
            foreach (Floor floor in floors)
            {
                if (soup.FloorUnder(floor.Given, out McpCollision.Hit hit, above: 0.5f, maxDrop: 6f))
                {
                    floor.Found = true;
                    floor.On = "collision";
                    floor.Point = hit.Point;
                    floor.Record = hit.Record;
                }
                if (query == null) continue;
                NavigationMeshQuery.Options options = new NavigationMeshQuery.Options() { Classes = 1 << navClass, IncludeDisabled = true, Extents = new Vector3(0.3f, 0.6f, 0.3f) };
                NavigationMeshQuery.Nearest onIt;
                lock (_navLock) onIt = query.FindNearest(floor.Point, options);
                if (floor.Found && onIt.Found && onIt.Distance <= 0.3f) continue;
                //Off the navmesh (furniture, a ledge): the navmesh beside it, as an NPC would stand there
                options.Extents = new Vector3(1.5f, 3f, 1.5f);
                NavigationMeshQuery.Nearest beside;
                lock (_navLock) beside = query.FindNearest(floor.Given, options);
                if (!beside.Found) continue;
                Vector2 across = new Vector2(beside.Point.X - floor.Given.X, beside.Point.Z - floor.Given.Z);
                if (across.Length() > 1.5f) continue;
                floor.Skipped = floor.Found ? floor.Record : null;
                floor.Found = true;
                floor.On = "navmesh";
                floor.Point = beside.Point;
                floor.Record = soup.FloorUnder(beside.Point, out McpCollision.Hit under, above: 0.3f, maxDrop: 1f) ? under.Record : null;
            }
            return floors;
        }

        /// <summary>The zone claiming a placement (a chain from the root), by name, or null. UI thread.</summary>
        private static string ZoneClaiming(Commands commands, List<Entity> chain)
        {
            if (chain == null || chain.Count == 0) return null;
            List<uint> ids = chain.Select(o => o.shortGUID.AsUInt32).ToList();
            foreach (SyncedZone zone in McpRegion.Zones(commands))
                foreach (List<uint> root in zone.roots)
                    if (root != null && root.Count != 0 && root.Count <= ids.Count && root.SequenceEqual(ids.Take(root.Count)))
                        return McpRegion.ZoneName(commands, zone);
            return null;
        }

        /// <summary>A world position from a token: [x, y, z], or a path from the root to an entity (its world position). UI thread.</summary>
        private static cTransform PlaceOf(Commands commands, JToken token, string what)
        {
            if (token is JArray array && array.Count == 3 && array.All(o => o.Type == JTokenType.Integer || o.Type == JTokenType.Float))
                return new cTransform(McpValues.ReadVector(token, what, null), Vector3.Zero);
            List<string> steps = McpScript.PathSteps(token, what);
            Composite root = commands.EntryPoints[0];
            List<Entity> chain = McpScript.ChainFrom(commands, root, steps);
            cTransform world = McpRegion.Walker(commands).Evaluate(root, chain).World;
            if (world == null)
                throw McpError.Invalid("'" + what + "': " + McpRegion.DescribeChain(commands, chain) + " has no position. Give a world position [x, y, z] or an entity that has one.");
            return world;
        }

        private static readonly object _navLock = new object();
        private static NavigationMeshQuery _nav;
        private static NavigationMesh _navFor;

        /// <summary>Forget the cached navmesh query (the level is closing: it must not keep the old navmesh alive).</summary>
        internal static void DropNav()
        {
            lock (_navLock)
            {
                _nav = null;
                _navFor = null;
            }
        }

        /// <summary>A query over the navmesh the level was last built with, or null with why. UI thread (it reads the level).</summary>
        private static NavigationMeshQuery NavQuery(Level level, out string why)
        {
            //Closing the level drops the cache (McpCollision.DropAll)
            McpCollision.Hook();
            why = null;
            NavigationMesh mesh = level.StateResources != null && level.StateResources.Count != 0 ? level.StateResources[0]?.NavMesh : null;
            if (mesh?.Polygons == null || mesh.Polygons.Length == 0)
            {
                why = "The level has no navmesh yet (save_level with build=true bakes one), so where NPCs can walk was not checked.";
                return null;
            }
            lock (_navLock)
            {
                if (_nav != null && _navFor == mesh) return _nav;
                try
                {
                    _nav = new NavigationMeshQuery(mesh);
                    _navFor = mesh;
                    return _nav;
                }
                catch (InvalidOperationException e)
                {
                    why = "The navmesh could not be read for queries (" + e.Message + ").";
                    return null;
                }
            }
        }

        /// <summary>The NAVIGATION_CHARACTER_CLASS bit an NPC walks as, from its archetype or class.</summary>
        private static int NavClassOf(Composite archetype, JToken characterClass)
        {
            Archetype known = Known(archetype);
            if (known != null) return known.NavClass;
            string text = characterClass?.Type == JTokenType.String ? ((string)characterClass).ToUpperInvariant() : "";
            if (text.Contains("ALIEN")) return 1;
            if (text.Contains("ANDROID")) return 2;
            if (text.Contains("FACEHUGGER")) return 4;
            if (text == "PLAYER") return 0;
            return 3;
        }

        private static readonly string[] NavClassNames = { "player", "alien", "android", "human_npc", "facehugger" };

        /// <summary>Where a point stands on the navmesh for a class: a line for the result, and whether it is on it (within 0.5 m).</summary>
        private static JObject NavAt(NavigationMeshQuery query, Vector3 point, int navClass, out bool on)
        {
            NavigationMeshQuery.Nearest nearest;
            lock (_navLock) nearest = query.FindNearest(point, new NavigationMeshQuery.Options() { Classes = 1 << navClass, IncludeDisabled = true });
            on = nearest.Found && nearest.Distance <= 0.5f;
            if (!nearest.Found)
                return new JObject() { ["on_navmesh"] = false, ["note"] = "no navmesh for class " + NavClassNames[navClass] + " within 2 m across and 4 m up or down" };
            JObject result = new JObject() { ["on_navmesh"] = on, ["nearest"] = McpValues.Vector(nearest.Point), ["off_by_m"] = Math.Round(nearest.Distance, 2) };
            if (!nearest.Enabled) result["note"] = "the nearest navmesh starts disabled (a door or barrier script opens)";
            return result;
        }
        #endregion

        #region Tools
        private const string NpcHelp = "An NPC: its path from the level's root composite (ids or names, through instances) to its archetype instance or Character, as describe_npc lists it.";
        private const string SettingsHelp = "Character settings by name: alliance_group (ALLIANCE_GROUP: PLAYER_ALLY sides with the player, as retail's Samuels and friendly androids; ANDROID, CIVILIAN, SECURITY and ALIEN are the groups retail's hostile NPCs are in), " +
            "character_class (CHARACTER_CLASS: ANDROID, CIVILIAN, SECURITY, INNOCENT, ALIEN...), attribute_set (a character class config, which picks its behaviour tree: get_config_record kind 'attributes'), display_model (DISPLAY_MODEL), anim_set (a character animation set), anim_tree_set, reference_skeleton, spawn_on_reset, or any other Character parameter.";
        private static readonly string[] MoveTypes = { "SLOW_WALK", "WALK", "FAST_WALK", "RUN", "TELEPORT" };
        private static readonly string[] StartModes = { "on_spawn", "trigger", "none" };

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "spawn_npc",
                Title = "Spawn NPC",
                Description = "Place an NPC the way retail does, as one undo step: an instance of a retail archetype - kind " + KindList + ", or any NPC archetype's path or name (e.g. 'Samuels') - " +
                    "at a world position stood on the floor under it (live collision, unsaved edits included), facing a point or a yaw, spawned when the level starts or when a trigger fires, " +
                    "with the settings given written where the archetype reads them (its own pin, else an alias override inside it for this NPC only). The archetype must be in the level: the error names a level to port_composites it from. " +
                    "Reports, without refusing, what the NPC needs: navmesh under it (as last built), its sound banks, its class config's behaviour tree, the zone it stands in. dry_run returns the plan. " +
                    "describe_npc reads it back; create_npc_route and play_character_animation give it something to do.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("kind", "What to spawn: " + KindList + ", or an NPC archetype's path or name.", required: true),
                    McpSchema.Position("position", "Where it stands, in 'space' (default world)"),
                    McpSchema.Any("at", "Instead of position: an entity to stand at, as a path from the root composite (ids or names; its position and facing are used), or 'viewport' for the point at the centre of the 3D view (needs the viewport)."),
                    McpSchema.String("space", "What 'position' is relative to: " + McpSchema.SpaceText + ". Default 'world'.", options: new[] { "world", "composite" }),
                    McpSchema.Integer("placement", "Which placement of 'composite' world positions go through when it is placed more than once (0-based, get_placements order)."),
                    McpSchema.Position("face", "A world point to face (it turns about Y only)"),
                    McpSchema.Number("yaw", "Or the way it faces: degrees about +Y in world space (0 faces +Z, 90 faces +X)."),
                    McpSchema.String("composite", "The composite to put it in (path or id). " + McpSchema.CompositeDefaultRoot + " Retail keeps NPCs in mission composites; what drives an NPC (its trigger, its routes) has to be in the same composite."),
                    McpSchema.String("name", "The instance's name (default the archetype's name, numbered)."),
                    McpSchema.Boolean("spawn_on_reset", "Spawn it when the level starts (default true, or false when 'trigger' is given)."),
                    McpSchema.String("trigger", "An entity of 'composite' (id or name) whose event spawns it (linked to its spawn_npc), e.g. a TriggerBox."),
                    McpSchema.String("trigger_pin", "The trigger's event (default on_entered when it has one, else triggered)."),
                    McpSchema.Map("settings", SettingsHelp),
                    McpSchema.Boolean("snap_to_floor", "Stand it on the walkable collision under the position, up to 1 m above and 6 m below (default true)."),
                    McpSchema.Boolean("dry_run", "Only say what would be made and where the settings would be written.")),
                Run = SpawnNpcTool,
            };

            yield return new McpTool()
            {
                Name = "describe_npc",
                Title = "Describe NPCs",
                Description = "The open level's NPCs as the game sets them up, per placement: archetype, world position, whether the level spawns it (spawn_on_reset, and what links to its spawn_npc), " +
                    "its effective character_class, alliance_group, attribute_set (with the behaviour tree its class config runs), display_model, anim_set, anim_tree_set and reference_skeleton - each resolved through the instance chain " +
                    "(instance pins, composite pins, VariableEnums inside the archetype, alias overrides), with where it comes from in detail 'full' - and the commands bound to it (TriggerBindCharacter). " +
                    "Give 'npc' or composite + entity for one NPC in full; otherwise every NPC (and Character outside an archetype) in 'region', briefly. Unsaved edits included. set_npc changes the settings where they come from.",
                InputSchema = McpSchema.Object(
                    McpSchema.Any("npc", NpcHelp),
                    McpSchema.String("composite", "With 'entity': the composite holding an NPC instance (every placement of it is described)."),
                    McpSchema.String("entity", "With 'composite': the NPC instance or Character (id or name)."),
                    McpSchema.Nested("region", "Only NPCs here. " + McpRegion.Help, McpRegion.Schema),
                    McpSchema.String("kind", "Only NPCs of this kind (" + KindList + ", actor, character, player) or whose archetype path contains this."),
                    McpSchema.String("filter", "Only NPCs whose name, archetype or composite contains this."),
                    McpSchema.String("detail", "'brief' (values only; the default for a list) or 'full' (where each value comes from, and the commands bound; the default for one NPC).", options: new[] { "brief", "full" }),
                    McpSchema.Limit(50, "NPCs"),
                    McpSchema.Offset("NPCs")),
                ReadOnly = true,
                Idempotent = true,
                Run = DescribeNpcTool,
            };

            yield return new McpTool()
            {
                Name = "set_npc",
                Title = "Set NPC settings",
                Description = "Change what an NPC is - its alliance (PLAYER_ALLY is friendly, as retail's Samuels and friendly androids are; ANDROID, CIVILIAN, SECURITY and ALIEN are hostile groups), class, attribute set, look, animation set, spawn_on_reset - " +
                    "written where the game reads it for that NPC: the archetype instance's own pin when the value comes from there, else an alias override in the NPC's composite on the value inside the archetype " +
                    "(an Android_NPC's alliance is a VariableEnum inside it; a Worker's is its NPC_Logic's pin), so other NPCs of the same archetype keep theirs. A value an alias further out already overrides is changed on that alias. " +
                    "One undo step; dry_run says where each would go. A value script sets as the game runs (CHR_SetAlliance, say) is reported, not overridden. describe_npc shows the current values and where they come from.",
                InputSchema = McpSchema.Object(
                    McpSchema.Any("npc", NpcHelp),
                    McpSchema.String("composite", "With 'entity': the composite holding the NPC instance."),
                    McpSchema.String("entity", "With 'composite': the NPC instance or Character (id or name)."),
                    McpSchema.Map("settings", SettingsHelp, required: true),
                    McpSchema.Boolean("dry_run", "Only say where each setting would be written, and its value now.")),
                Run = SetNpcTool,
            };

            yield return new McpTool()
            {
                Name = "create_npc_route",
                Title = "Create NPC route",
                Description = "Make an NPC walk a route, wired as retail does it, as one undo step in the NPC's composite: a PositionMarker and a CMD_GoTo per point (Waypoint -> the marker), " +
                    "chained through CMD_Idle waits (GoTo.succeeded -> Idle.apply_start, Idle.finished -> the next GoTo.apply_start; a failed move waits and goes on), the last looping back to the first for a patrol, " +
                    "and a TriggerBindCharacter that binds the NPC (characters -> its npc_reference) and starts the first move (bound_trigger -> apply_start) when it finishes spawning, when a trigger fires, or when script fires the bind's trigger. " +
                    "Points are world positions (stood on the floor) or entities. Each point and each leg is checked against the navmesh as last built; what fails is reported, not refused. " +
                    "In game, runtime_utils action 'activity' shows apply_start, succeeded and failed firing.",
                InputSchema = McpSchema.Object(
                    McpSchema.Any("npc", NpcHelp),
                    McpSchema.String("composite", "With 'entity': the composite holding the NPC instance (the route is built there)."),
                    McpSchema.String("entity", "With 'composite': the NPC instance or Character (id or name)."),
                    McpSchema.Integer("placement", "With composite + entity placed more than once: which placement the world points are converted through (default 0)."),
                    McpSchema.Any("points", "The places to walk to, in order: world positions [x, y, z] (metres, Y up) or entity paths from the root composite, e.g. [[1, 0, 2], [8, 0, 2], [\"Door_1\"]].", required: true),
                    McpSchema.String("mode", "'loop' (default: a patrol, back to the first point and round again) or 'once'.", options: new[] { "loop", "once" }),
                    McpSchema.String("move", "How it moves (CMD_GoTo move_type): slow_walk, walk (default), fast_walk, run or teleport.", options: MoveTypes.Select(o => o.ToLowerInvariant())),
                    McpSchema.Number("wait", "Seconds it waits at each point (CMD_Idle duration; default 2, 0 goes straight on)."),
                    McpSchema.String("start", "What starts it: 'on_spawn' (default without a trigger: the NPC's finished_spawning), 'trigger' (the trigger's event) or 'none' (fire the bind's trigger from script).", options: StartModes),
                    McpSchema.String("trigger", "An entity of the NPC's composite (id or name) whose event starts the route."),
                    McpSchema.String("trigger_pin", "The trigger's event (default on_entered when it has one, else triggered)."),
                    McpSchema.Boolean("override_ai", "Keep it on the route whatever it sees (the commands' override_all_ai; default false: its AI may break off to react, which ends the route)."),
                    McpSchema.Boolean("snap_to_floor", "Stand each point on the walkable collision under it (default true)."),
                    McpSchema.String("name", "Name prefix for what is made (default Route_<npc>)."),
                    McpSchema.String("page", "The flowgraph page the new nodes go on (default the name prefix)."),
                    McpSchema.Boolean("dry_run", "Only check the points and say what would be made.")),
                Run = CreateNpcRoute,
            };

            yield return new McpTool()
            {
                Name = "play_character_animation",
                Title = "Play character animation",
                Description = "Make a character play a clip when something happens, wired as retail does it, as one undo step: a TriggerBindCharacter binding the character (an NPC's npc_reference, or the player through VariableThePlayer) " +
                    "whose bound_trigger starts a CMD_PlayAnimation (or, with secondary, a CHR_PlaySecondaryAnimation layered over what it is doing) with AnimationSet and Animation set; with 'at', a PositionMarker it moves to and plays at (Marker); " +
                    "and the start link (a trigger's event, or the NPC finishing spawning). The clip is checked against ANIMATION.PAK: it has to be in the set (default the character's anim_set), and the set has to play on the character's rig. " +
                    "Returns the entities made and the pins to chain from: finished, Interrupted, command_started.",
                InputSchema = McpSchema.Object(
                    McpSchema.Any("character", "Who plays it: an NPC's path from the root composite (to its archetype instance or Character, as describe_npc lists it), or 'player'.", required: true),
                    McpSchema.String("composite", "Where to build it (path or id). Default: the NPC's composite (a TriggerBindCharacter only links to an NPC in its own composite); for the player, 'root'."),
                    McpSchema.String("animation", "The clip, as list_animations names it.", required: true),
                    McpSchema.String("animation_set", "The set holding it (default: the character's anim_set; for the player, the one character set holding the clip)."),
                    McpSchema.Boolean("secondary", "Play it as a secondary animation (CHR_PlaySecondaryAnimation) over what the character is doing, rather than as a command that takes it over."),
                    McpSchema.Any("at", "Where it plays: a world position [x, y, z] (a PositionMarker is made there, facing 'yaw') or an entity path from the root composite (a marker at its position and facing). Leave out to play where the character stands."),
                    McpSchema.Number("yaw", "With a position in 'at': the way it faces, degrees about +Y in world space (0 faces +Z)."),
                    McpSchema.String("start", "What starts it: 'trigger' (the trigger's event; the default when one is given), 'on_spawn' (the NPC's finished_spawning; the default for an NPC) or 'none' (fire the bind's trigger from script; the default for the player).", options: StartModes),
                    McpSchema.String("trigger", "An entity of 'composite' (id or name) whose event starts it."),
                    McpSchema.String("trigger_pin", "The trigger's event (default on_entered when it has one, else triggered)."),
                    McpSchema.Boolean("loop", "Play it over and over (PlayCount -1) until something stops it."),
                    McpSchema.Integer("play_count", "How many times (default 1)."),
                    McpSchema.Number("play_speed", "Speed multiplier (default 1)."),
                    McpSchema.Number("blend_in", "Seconds to blend in."),
                    McpSchema.Number("blend_out", "Seconds to blend out."),
                    McpSchema.Integer("start_frame", "First frame to play."),
                    McpSchema.Integer("end_frame", "Last frame to play."),
                    McpSchema.Boolean("override_ai", "The command overrides the NPC's AI while it plays (override_all_ai; default true, as most retail ones do)."),
                    McpSchema.Boolean("allow_interruption", "Let the AI or damage interrupt it (AllowInterruption)."),
                    McpSchema.String("name", "The command's name (default the clip's)."),
                    McpSchema.String("page", "The flowgraph page the new nodes go on."),
                    McpSchema.Boolean("dry_run", "Only check the clip and say what would be made.")),
                Run = PlayCharacterAnimation,
            };
        }

        private static bool IsFirstPerson(Anim.AnimationSet set) => set.Name.StartsWith("1ST_PERSON", StringComparison.OrdinalIgnoreCase);

        /// <summary>A name prefix nothing in the composite is called yet with any of these endings: the one wanted, else it with _2, _3...</summary>
        private static string FreePrefix(Commands commands, Composite composite, string wanted, params string[] endings)
        {
            HashSet<string> taken = new HashSet<string>(composite.GetEntities().Select(o => McpScript.EntityName(commands, composite, o)), StringComparer.OrdinalIgnoreCase);
            string prefix = wanted;
            for (int i = 2; endings.Any(o => taken.Contains(prefix + o)); i++) prefix = wanted + "_" + i;
            return prefix;
        }

        /// <summary>The event of a trigger entity to start things from: on_entered for a volume, else triggered.</summary>
        private static string EventPinOf(Commands commands, Composite composite, Entity entity)
        {
            HashSet<string> pins = new HashSet<string>(commands.Utils.GetAllParameters(entity, composite).Select(o => McpScript.ParamName(o.Item1)), StringComparer.OrdinalIgnoreCase);
            foreach (string pin in new[] { "on_entered", "triggered", "on_think", "on_success" })
                if (pins.Contains(pin)) return pin;
            return "triggered";
        }

        /// <summary>A world facing: yaw from 'face' (a point) or 'yaw', else <paramref name="fallback"/>.</summary>
        private static Vector3 FacingOf(McpCall call, Vector3 from, Vector3 fallback)
        {
            if (call.Has("face"))
            {
                Vector3 toward = McpValues.ReadVector(call.Token("face"), "face", null) - from;
                toward.Y = 0;
                if (toward.LengthSquared() < 1e-6f) return fallback;
                return InstanceTransform.LookRotation(toward);
            }
            if (call.Has("yaw")) return new Vector3(0, (float)call.Num("yaw"), 0);
            return fallback;
        }

        /// <summary>The world frame of the composite holding an NPC at its placement (identity in the root); null when it is not placed. UI thread.</summary>
        private static cTransform HolderFrame(Commands commands, Npc npc)
        {
            Composite root = commands.EntryPoints[0];
            if (npc.Holder == root) return new cTransform(Vector3.Zero, Vector3.Zero);
            if (!npc.Placed || npc.Comps[0] != root) return null;
            return McpRegion.Walker(commands).Evaluate(root, npc.Chain.Take(npc.Level).ToList()).World ?? new cTransform(Vector3.Zero, Vector3.Zero);
        }

        private static JObject Transform(cTransform transform) => new JObject() { ["position"] = McpValues.Vector(transform.position), ["rotation"] = McpValues.Vector(transform.rotation) };

        private static string Event(Commands commands, Composite composite, Entity entity, string pin) => McpScript.EntityName(commands, composite, entity) + "." + pin;

        #region spawn_npc
        private static object SpawnNpcTool(McpCall call)
        {
            string kind = call.Str("kind", required: true);
            bool dryRun = call.Bool("dry_run"), snap = call.Bool("snap_to_floor", true);
            string space = (call.Str("space") ?? "world").Trim().ToLowerInvariant();
            if (space != "world" && space != "composite") throw McpError.Invalid("'space' is 'world' (default) or 'composite' (relative to the composite's own origin).");
            if ((call.Has("position") ? 1 : 0) + (call.Has("at") ? 1 : 0) != 1) throw McpError.Invalid("Say where it stands: 'position' [x, y, z], or 'at' (an entity's path from the root, or 'viewport').");
            if (call.Has("face") && call.Has("yaw")) throw McpError.Invalid("Give 'face' (a world point to face) or 'yaw', not both.");
            if (call.Has("trigger_pin") && !call.Has("trigger")) throw McpError.Invalid("'trigger_pin' goes with 'trigger'.");
            if (call.Has("at") && space == "composite") throw McpError.Invalid("'space' is for 'position'; 'at' is always a place in the level.");
            bool spawnOnReset = call.Bool("spawn_on_reset", !call.Has("trigger"));
            JObject settingsGiven = call.Object("settings");
            JToken atToken = call.Token("at");
            bool viewport = atToken != null && atToken.Type == JTokenType.String && string.Equals(((string)atToken).Trim(), "viewport", StringComparison.OrdinalIgnoreCase);

            //The point at the centre of the 3D view, asked of the viewer (not on the UI thread)
            Vector3? picked = null;
            if (viewport)
            {
                JObject pick = McpViewportTools.PickPoint(call, 0.5, 0.5);
                if (pick["hit"]?.Value<bool>() != true)
                    throw new McpError(McpErrorCodes.NotFound, "Nothing is under the centre of the 3D view: point it at the floor where the NPC should stand, or give 'position'.");
                if (!string.Equals((string)pick["space"], "world", StringComparison.OrdinalIgnoreCase))
                    throw new McpError(McpErrorCodes.Refused, "The 3D view shows " + (string)pick["space"] + ", not the level: open the level's root composite in it, or give 'position'.");
                picked = McpValues.ReadVector(pick["position"], "the viewport's point", null);
            }

            Composite composite = null, archetype = null;
            Archetype known = null;
            cTransform frame = null;
            Vector3 world = Vector3.Zero, local = Vector3.Zero;
            Vector3 facing = Vector3.Zero;
            List<(string name, JToken value)> settings = null;
            Entity trigger = null;
            string triggerPin = null;
            List<string> warnings = new List<string>();
            NavigationMeshQuery query = null;
            string navWhy = null;
            int navClass = 3;
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: !dryRun);
                Commands commands = content.Level.Commands;
                composite = call.Has("composite") ? McpScript.FindComposite(commands, call.Str("composite")) : commands.EntryPoints[0];
                archetype = FindArchetype(commands, kind, out known);
                if (commands.Utils.WouldCreateCompositeInstanceCycle(composite, archetype))
                    throw McpError.Invalid("An instance of " + archetype.name + " cannot go in " + composite.name + ": it would contain itself.");
                try
                {
                    frame = McpSpatialTools.FrameOf(commands, composite, call.Has("placement") ? call.Int("placement") : (int?)null, "'placement'", out JObject _);
                }
                catch (McpError) when (space == "composite")
                {
                    frame = null;
                }
                Vector3 fallback = Vector3.Zero;
                if (call.Has("position"))
                {
                    Vector3 given = McpValues.ReadVector(call.Token("position"), "position", null);
                    world = space == "world" ? given : (frame == null ? given : InstanceTransform.PointToWorld(frame, given));
                    local = given;
                }
                else if (picked != null)
                    world = picked.Value;
                else
                {
                    cTransform at = PlaceOf(commands, atToken, "at");
                    world = at.position;
                    fallback = new Vector3(0, at.rotation.Y, 0);
                }
                facing = FacingOf(call, world, fallback);
                settings = ReadSettings(commands, settingsGiven, null, archetype);
                foreach ((string name, JToken value) in settings)
                {
                    string problem = CheckSetting(name, value);
                    if (problem != null) warnings.Add(problem);
                }
                if (call.Has("trigger"))
                {
                    trigger = McpScript.FindEntity(commands, composite, call.Str("trigger"));
                    triggerPin = call.Str("trigger_pin") ?? EventPinOf(commands, composite, trigger);
                }
                //The class it walks the navmesh as: the one given, else its archetype's
                JToken givenClass = settings.Where(o => o.name == "character_class").Select(o => o.value).FirstOrDefault();
                navClass = NavClassOf(archetype, givenClass ?? SettingOf(commands, StandIn(commands, composite, archetype, new FunctionEntity(archetype)), "character_class")?.Value);
                if (frame != null) query = NavQuery(content.Level, out navWhy);
            });

            //Stood where a character stands, as the live collision (and the navmesh as last built) has it
            Floor floor = null;
            if (snap && frame != null)
            {
                floor = Floors(call, new List<Vector3>() { world }, query, navClass)[0];
                if (floor.Found) world = floor.Point;
                else warnings.Add("No walkable collision from 0.5 m above to 6 m below " + McpRegion.Format(world) + ": it stands where given, and may fall or float.");
            }
            cTransform placed = frame == null
                ? new cTransform(local, facing)
                : InstanceTransform.ToLocal(frame, new cTransform(world, facing));
            if (frame == null) warnings.Add(composite.name + " is not placed in the level, so the position is only relative to it; the floor and navmesh were not checked.");

            if (dryRun)
            {
                return McpEditor.UI(() =>
                {
                    Commands commands = McpEditor.RequireCommands(forEditing: false);
                    //Planned on a stand-in instance: every write lands on the instance or an alias through it
                    FunctionEntity standIn = new FunctionEntity(archetype);
                    Npc plan = StandIn(commands, composite, archetype, standIn);
                    JObject planned = new JObject();
                    foreach ((string name, JToken value) in settings)
                    {
                        Write write = PlanWrite(commands, plan, name, value);
                        write.NewName = "<the new instance>";
                        planned[name] = new JObject() { ["value"] = value, ["now"] = write.Before, ["would_write"] = write.Describe(commands) };
                    }
                    JObject result = new JObject()
                    {
                        ["dry_run"] = true,
                        ["archetype"] = archetype.name,
                        ["kind"] = KindOf(archetype, null, commands),
                        ["composite"] = composite.name,
                        ["space"] = "world",
                        ["position"] = frame == null ? null : McpValues.Vector(world),
                        ["rotation"] = McpValues.Vector(facing),
                        ["local"] = Transform(placed),
                        ["spawn"] = trigger != null ? "when " + Event(commands, composite, trigger, triggerPin) + " fires" : spawnOnReset ? "when the level starts" : "not spawned until something links to its spawn_npc",
                    };
                    if (floor != null) result["floor"] = floor.Describe(commands);
                    if (planned.Count != 0) result["settings"] = planned;
                    if (warnings.Count != 0) result["warnings"] = new JArray(warnings);
                    result["next"] = "Call again without dry_run to make it, as one undo step.";
                    return result;
                });
            }

            FunctionEntity made = null;
            JObject written = new JObject();
            McpScriptEdit.Run("Spawn " + (known?.Kind ?? McpScript.CompositeLeaf(archetype)) + (call.Has("name") ? " " + call.Str("name") : ""), composite, edit =>
            {
                Commands commands = edit.Commands;
                made = edit.AddInstance(composite, archetype, call.Str("name"));
                edit.SetParameter(composite, made, "position", Transform(placed));
                if (!settings.Any(o => o.name == "spawn_on_reset"))
                    edit.SetParameter(composite, made, "spawn_on_reset", new JValue(spawnOnReset));
                if (trigger != null)
                    edit.AddLink(composite, trigger, triggerPin, made, "spawn_npc");
                Npc npc = StandIn(commands, composite, archetype, made);
                foreach ((string name, JToken value) in settings)
                {
                    Write write = PlanWrite(commands, npc, name, value);
                    if (write.Problem != null)
                    {
                        warnings.Add(name + " was not written: " + write.Problem + ".");
                        continue;
                    }
                    JToken value2 = ApplyWrite(edit, write);
                    written[name] = new JObject() { ["value"] = value2, ["written_on"] = write.Describe(commands) };
                }
                edit.Select.Add(made);
            });

            JObject described = McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                Commands commands = content.Level.Commands;
                Composite root = commands.EntryPoints[0];
                List<Entity> chain = null;
                if (composite == root) chain = new List<Entity>() { made };
                else if (frame != null)
                {
                    List<McpPlacements.Placement> found = new List<McpPlacements.Placement>();
                    McpRegion.Walker(commands).PlacementsOf(root, composite, made, found, 1 + (call.Has("placement") ? call.Int("placement") : 0), call.Cancel, realOnly: true);
                    if (found.Count != 0) chain = found[Math.Min(found.Count - 1, call.Has("placement") ? call.Int("placement") : 0)].Chain;
                }
                Npc npc = chain != null ? NpcOf(commands, root, chain, null) : NpcOf(commands, composite, new List<Entity>() { made }, null);
                JObject result = new JObject()
                {
                    ["npc"] = new JObject()
                    {
                        ["id"] = McpScript.Id(made.shortGUID),
                        ["name"] = McpScript.EntityName(commands, composite, made),
                        ["kind"] = KindOf(archetype, null, commands),
                        ["archetype"] = archetype.name,
                        ["composite"] = composite.name,
                        ["path"] = chain != null ? McpRegion.ChainJson(commands, chain) : null,
                    },
                    ["space"] = "world",
                };
                if (frame != null)
                {
                    result["position"] = McpValues.Vector(world);
                    result["rotation"] = McpValues.Vector(facing);
                }
                if (composite != root) result["local"] = Transform(placed);
                if (floor != null)
                {
                    JObject stood = floor.Describe(commands);
                    if (floor.Record != null) stood["zone"] = ZoneClaiming(commands, floor.Record.Chain) ?? "no zone claims that floor (the global zone)";
                    result["floor"] = stood;
                }
                result["spawn"] = trigger != null ? "when " + Event(commands, composite, trigger, triggerPin) + " fires" : spawnOnReset ? "when the level starts (spawn_on_reset)" : "not spawned yet: link an event to " + McpScript.EntityName(commands, composite, made) + ".spawn_npc";
                if (written.Count != 0) result["settings"] = written;

                //What it needs to run, reported rather than refused
                JObject needs = new JObject();
                Setting attributes = npc == null ? null : SettingOf(commands, npc, "attribute_set");
                if (attributes != null)
                {
                    Dictionary<string, string> trees = ClassTrees();
                    string tree = TreeOf(trees, attributes.Value);
                    needs["behaviour_tree"] = tree != null ? attributes.Value + " runs " + tree : trees == null ? "the class configs could not be read" : "attribute_set " + attributes.Value + " names no class config: it would have no behaviour";
                    if (tree == null && trees != null) warnings.Add("attribute_set " + attributes.Value + " names no character class config (get_config_record kind 'attributes' lists them).");
                }
                if (known != null)
                {
                    List<string> loaded = McpSoundTools.PermanentBanks().Concat(McpSoundTools.LoadZoneBanks(content.Level, out bool listFound)).Concat(McpSoundTools.BankLoaders(commands).Select(o => o.Bank)).Where(o => o != null).ToList();
                    List<string> missing = known.Banks.Where(b => !loaded.Any(o => string.Equals(o, b, StringComparison.OrdinalIgnoreCase))).ToList();
                    needs["sound_banks"] = new JObject() { ["wants"] = new JArray(known.Banks), ["missing"] = new JArray(missing) };
                    if (missing.Count != 0)
                        warnings.Add("Retail levels with this kind (" + known.Kind + ") list the sound bank" + (missing.Count == 1 ? " " : "s ") + string.Join(", ", missing) + " in their SOUNDLOADZONES; this level does not, has no SoundLoadBank for " + (missing.Count == 1 ? "it" : "them") + ", and " + (missing.Count == 1 ? "it is" : "they are") + " not permanent, so it may be silent (whether that list loads banks is unverified).");
                }
                result["needs"] = needs;
                return result;
            });
            if (query != null)
            {
                JObject nav = NavAt(query, world, navClass, out bool on);
                nav["class"] = NavClassNames[navClass];
                nav["note"] = "as of the last Save & Build";
                ((JObject)described["needs"])["navmesh"] = nav;
                if (!on) warnings.Add("It does not stand on navmesh for class " + NavClassNames[navClass] + " (as last built), so it may not move: Save & Build after adding floors, or give a position on the navmesh (needs.navmesh.nearest).");
            }
            else if (navWhy != null)
                ((JObject)described["needs"])["navmesh"] = navWhy;
            if (warnings.Count != 0) described["warnings"] = new JArray(warnings);
            described["pins"] = new JObject()
            {
                ["spawn_npc"] = "method: spawns it",
                ["despawn_npc"] = "method: removes it",
                ["finished_spawning"] = "fires once it has spawned: start its commands from here",
                ["killed"] = "fires when it dies",
                ["npc_reference"] = "stands for it: TriggerBindCharacter.characters links here to bind commands to it",
            };
            described["next"] = "create_npc_route gives it a patrol, play_character_animation a clip to play; set_npc changes its alliance or class; describe_npc reads it back. It reaches the game at save_level with build=true.";
            return described;
        }
        #endregion

        #region describe_npc
        private static object DescribeNpcTool(McpCall call)
        {
            bool one = call.Has("npc") || call.Has("entity");
            if (call.Has("entity") && !call.Has("composite")) throw McpError.Invalid("'entity' goes with 'composite'.");
            if (one && call.Has("region")) throw McpError.Invalid("Give one NPC (npc, or composite + entity) or a 'region' to list, not both.");
            string detail = (call.Str("detail") ?? (one ? "full" : "brief")).Trim().ToLowerInvariant();
            if (detail != "brief" && detail != "full") throw McpError.Invalid("'detail' is 'brief' or 'full'.");
            string kind = call.Str("kind")?.Trim();
            string filter = call.Str("filter")?.Trim();

            using (McpEditorTools.Heartbeat(call, "Finding NPCs"))
                return McpEditor.UI(() =>
                {
                    LevelContent content = McpEditor.RequireLevel(forEditing: false);
                    Commands commands = content.Level.Commands;
                    Composite shown = Singleton.Editor?.CompositeDisplay?.Composite;
                    if (shown != null) McpBrowseTools.CompileIfShown(shown);
                    JObject result = new JObject();
                    List<Npc> npcs;
                    bool truncated = false;
                    if (one)
                        npcs = ReadNpcs(call, commands);
                    else
                    {
                        McpRegion region = call.Has("region") ? McpRegion.Resolve(call, commands, content.Level, call.Token("region")) : null;
                        if (region != null)
                        {
                            result["region"] = region.Label;
                            foreach (string note in region.Notes) call.Note(note);
                            if (region.Kind == "level") region = null;
                        }
                        npcs = FindNpcs(call, commands, region, out truncated);
                    }
                    if (kind != null)
                        npcs = npcs.Where(o => string.Equals(KindOf(o.Archetype, o.Character, commands), kind, StringComparison.OrdinalIgnoreCase) || (o.Archetype != null && o.Archetype.name.IndexOf(kind, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                    if (filter != null)
                        npcs = npcs.Where(o => McpScript.EntityName(commands, o.Holder, o.Entity).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || (o.Archetype?.name ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || o.Holder.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                    List<Npc> ordered = npcs.OrderBy(o => o.Holder.name, StringComparer.OrdinalIgnoreCase).ThenBy(o => McpScript.EntityName(commands, o.Holder, o.Entity), StringComparer.OrdinalIgnoreCase).ThenBy(o => string.Join("/", o.Chain.Select(e => e.shortGUID.AsUInt32))).ToList();
                    Dictionary<string, string> trees = ClassTrees();
                    result["space"] = "world";
                    McpPaging.Page(call, ordered, result, "npcs", o => DescribeNpc(commands, o, detail == "full", trees), 50);
                    if (truncated) call.Note("The walk of the level stopped early (it is very large): some NPCs may be missing. Give a 'region'.");
                    if (ordered.Count == 0)
                        call.Note(kind != null || filter != null ? "No NPC matches. Leave out kind/filter to list them all." : "No NPCs here: spawn_npc places one (retail archetypes: " + KindList + ").");
                    else if (!one && detail == "brief")
                        call.Note("detail 'full' (or one NPC by 'npc') says where each value comes from and what commands are bound to it; set_npc changes them.");
                    return result;
                });
        }

        /// <summary>One NPC for describe_npc. UI thread.</summary>
        private static JObject DescribeNpc(Commands commands, Npc npc, bool full, Dictionary<string, string> trees)
        {
            Entity character = npc.Character;
            Composite holder = npc.Holder;
            JObject row = new JObject()
            {
                ["name"] = McpScript.EntityName(commands, holder, npc.Entity),
                ["kind"] = KindOf(npc.Archetype, character, commands),
                ["archetype"] = npc.Archetype?.name ?? "Character",
                ["composite"] = holder.name,
                ["path"] = PathOf(commands, npc),
            };
            if (npc.World != null) row["position"] = McpValues.Vector(npc.World.position);
            if (npc.World != null && full && npc.World.rotation != Vector3.Zero) row["rotation"] = McpValues.Vector(npc.World.rotation);
            if (!npc.Real) row["not_placed"] = "it is deleted, or under a template or deleted instance: the game does not place it";
            if (character == null)
            {
                row["problem"] = "it holds no Character the npc_reference pin leads to, so it has no settings";
                return row;
            }

            JObject settings = new JObject();
            JToken attributeSet = null;
            Setting spawnOnReset = null;
            foreach (string name in ProfileSettings)
            {
                Setting setting = SettingOf(commands, npc, name);
                if (name == "attribute_set") attributeSet = setting.Value;
                if (name == "spawn_on_reset") { spawnOnReset = setting; continue; }
                if (!full) { settings[name] = setting.Value; continue; }
                JObject detailed = new JObject() { ["value"] = setting.Value, ["from"] = setting.Source, ["via"] = new JArray(setting.Route) };
                if (setting.Source == "runtime") detailed["note"] = "set as the game runs by " + setting.RuntimeBy;
                settings[name] = detailed;
            }
            row["settings"] = settings;
            string tree = TreeOf(trees, attributeSet);
            if (tree != null) row["behaviour_tree"] = tree;
            else if (trees != null && attributeSet != null && attributeSet.Type == JTokenType.String) row["behaviour_tree"] = "none: '" + (string)attributeSet + "' is no class config";

            //Spawning: on reset, and what calls spawn_npc / despawn_npc
            JObject spawn = new JObject() { ["on_reset"] = spawnOnReset?.Value };
            if (full && spawnOnReset != null) spawn["via"] = new JArray(spawnOnReset.Route);
            if (npc.Archetype != null)
            {
                JArray by = new JArray(), despawnBy = new JArray();
                foreach (Entity other in holder.GetEntities())
                    foreach (EntityConnector link in other.childLinks)
                    {
                        if (link.linkedEntityID != npc.Entity.shortGUID) continue;
                        if (link.linkedParamID == SpawnNpc) by.Add(Event(commands, holder, other, McpScript.ParamName(link.thisParamID)) + " (" + McpScript.TypeName(commands, holder, other) + ")");
                        else if (link.linkedParamID == DespawnNpc) despawnBy.Add(Event(commands, holder, other, McpScript.ParamName(link.thisParamID)));
                    }
                if (by.Count != 0) spawn["spawned_by"] = by;
                if (despawnBy.Count != 0 && full) spawn["despawned_by"] = despawnBy;
                if (by.Count == 0 && spawnOnReset?.Value?.Type == JTokenType.Boolean && !(bool)spawnOnReset.Value)
                    spawn["note"] = "Nothing in " + McpScript.CompositeLeaf(holder) + " links to its spawn_npc and it does not spawn on reset: it only appears if script elsewhere spawns it (an alias, or a parent composite's pin).";
            }
            row["spawn"] = spawn;

            //Commands bound to it: TriggerBindCharacters whose characters pin points at it, and what each starts
            ShortGuid pointed = npc.Archetype != null ? NpcReference : ShortGuids.reference;
            JArray binds = new JArray();
            int bound = 0;
            foreach (FunctionEntity bind in holder.functions)
            {
                if (bind.function != FunctionType.TriggerBindCharacter) continue;
                if (!bind.childLinks.Any(o => o.thisParamID == Characters && o.linkedEntityID == npc.Entity.shortGUID && o.linkedParamID == pointed)) continue;
                bound++;
                if (!full || binds.Count >= 20) continue;
                JArray starts = new JArray(bind.childLinks.Where(o => o.thisParamID == BoundTrigger && holder.GetEntityByID(o.linkedEntityID) != null)
                    .Select(o => Event(commands, holder, holder.GetEntityByID(o.linkedEntityID), McpScript.ParamName(o.linkedParamID)) + " (" + McpScript.TypeName(commands, holder, holder.GetEntityByID(o.linkedEntityID)) + ")"));
                binds.Add(new JObject() { ["bind"] = McpScript.EntityName(commands, holder, bind) + " (" + McpScript.Id(bind.shortGUID) + ")", ["starts"] = starts });
            }
            if (full)
            {
                if (binds.Count != 0) row["bound_commands"] = binds;
                if (bound > binds.Count) row["bound_commands_total"] = bound;
                ShortGuid finished = ShortGuidUtils.Generate("finished_spawning");
                JArray onSpawn = new JArray(npc.Entity.childLinks.Where(o => o.thisParamID == finished && holder.GetEntityByID(o.linkedEntityID) != null).Take(20)
                    .Select(o => Event(commands, holder, holder.GetEntityByID(o.linkedEntityID), McpScript.ParamName(o.linkedParamID))));
                if (onSpawn.Count != 0) row["on_finished_spawning"] = onSpawn;
                row["change"] = "set_npc writes a setting where it comes from for this NPC; play_character_animation and create_npc_route give it things to do.";
            }
            else if (bound != 0)
                row["bound_commands"] = bound;
            return row;
        }
        #endregion

        #region set_npc
        private static object SetNpcTool(McpCall call)
        {
            JObject given = call.Object("settings", required: true);
            if (given.Count == 0) throw McpError.Invalid("'settings' is empty: give the Character settings to change, e.g. {\"alliance_group\": \"PLAYER_ALLY\"}.");
            bool dryRun = call.Bool("dry_run");
            if (call.Has("entity") && !call.Has("composite")) throw McpError.Invalid("'entity' goes with 'composite'.");

            Npc npc = null;
            List<(string name, JToken value)> settings = null;
            List<string> warnings = new List<string>();
            int placements = 0;
            JObject planned = McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: !dryRun);
                List<Npc> npcs = ReadNpcs(call, commands);
                npc = npcs[0];
                placements = npcs.Count;
                settings = ReadSettings(commands, given, npc, null);
                JObject plan = new JObject();
                foreach ((string name, JToken value) in settings)
                {
                    string problem = CheckSetting(name, value);
                    if (problem != null) warnings.Add(problem);
                    Write write = PlanWrite(commands, npc, name, value);
                    JObject row = new JObject() { ["now"] = write.Before, ["value"] = value };
                    if (write.Problem != null) row["cannot"] = write.Problem;
                    else row["writes"] = write.Describe(commands);
                    row["via"] = new JArray(write.Route ?? new List<string>());
                    plan[name] = row;
                }
                return plan;
            });
            string npcName = McpEditor.UI(() => McpScript.EntityName(McpEditor.RequireCommands(forEditing: false), npc.Holder, npc.Entity));
            if (placements > 1 || (npc.Placed && npc.Comps.Count > 1))
                call.Note("A change in " + npc.Holder.name + " applies wherever that composite is placed" + (placements > 1 ? " (the entity is placed " + placements + " times)" : "") + ".");
            if (dryRun)
            {
                JObject result = new JObject() { ["dry_run"] = true, ["npc"] = npcName, ["composite"] = npc.Holder.name, ["settings"] = planned };
                if (warnings.Count != 0) result["warnings"] = new JArray(warnings);
                result["next"] = "Call again without dry_run to write them, as one undo step.";
                return result;
            }

            JObject written = new JObject();
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run("Set " + npcName + " " + string.Join(", ", settings.Select(o => o.name)), npc.Holder, edit =>
            {
                Commands commands = edit.Commands;
                foreach ((string name, JToken value) in settings)
                {
                    Write write = PlanWrite(commands, npc, name, value);
                    if (write.Problem != null)
                    {
                        written[name] = new JObject() { ["not_written"] = write.Problem, ["value"] = write.Before };
                        continue;
                    }
                    JToken after = ApplyWrite(edit, write);
                    written[name] = new JObject() { ["before"] = write.Before, ["after"] = after, ["written_on"] = write.Describe(commands) };
                }
                edit.Select.Add(npc.Entity);
            }, show: false);

            JObject done = new JObject() { ["npc"] = npcName, ["composite"] = npc.Holder.name, ["settings"] = written };
            if (!outcome.Changed) done["unchanged"] = "Every setting already had that value (or could not be written: see each).";
            if (warnings.Count != 0) done["warnings"] = new JArray(warnings);
            done["next"] = "describe_npc shows the values now in force. It reaches the game at save_level (with build=true if the class config changed).";
            return done;
        }
        #endregion

        #region create_npc_route
        private static string ReadStart(McpCall call, string fallback)
        {
            string start = (call.Str("start") ?? (call.Has("trigger") ? "trigger" : fallback)).Trim().ToLowerInvariant();
            if (!StartModes.Contains(start)) throw McpError.Invalid("'start' is " + string.Join(", ", StartModes) + ".");
            if (start == "trigger" && !call.Has("trigger")) throw McpError.Invalid("start 'trigger' needs 'trigger': the entity whose event starts it.");
            if (start != "trigger" && call.Has("trigger")) throw McpError.Invalid("'trigger' goes with start 'trigger' (the default when a trigger is given).");
            if (call.Has("trigger_pin") && !call.Has("trigger")) throw McpError.Invalid("'trigger_pin' goes with 'trigger'.");
            return start;
        }

        private static object CreateNpcRoute(McpCall call)
        {
            JToken pointsToken = call.Token("points") ?? throw McpError.Invalid("'points' is required: the places to walk to, in order.");
            JArray pointTokens = pointsToken as JArray ?? new JArray(pointsToken);
            //One point given bare, [x, y, z]
            if (pointTokens.Count == 3 && pointTokens.All(o => o.Type == JTokenType.Integer || o.Type == JTokenType.Float))
            {
                JArray single = new JArray();
                single.Add(pointTokens.DeepClone());
                pointTokens = single;
            }
            if (pointTokens.Count == 0) throw McpError.Invalid("'points' is empty: give the places to walk to, in order.");
            if (pointTokens.Count > 64) throw McpError.Invalid("'points' takes at most 64 points.");
            string mode = (call.Str("mode") ?? "loop").Trim().ToLowerInvariant();
            if (mode != "loop" && mode != "once") throw McpError.Invalid("'mode' is 'loop' (a patrol: back to the first point and round again) or 'once'.");
            if (mode == "loop" && pointTokens.Count < 2) throw McpError.Invalid("A loop needs at least two points (mode 'once' walks to one).");
            string move = (call.Str("move") ?? "walk").Trim().ToUpperInvariant();
            if (!MoveTypes.Contains(move)) throw McpError.Invalid("'move' is " + string.Join(", ", MoveTypes.Select(o => o.ToLowerInvariant())) + ".");
            double wait = call.Num("wait", 2);
            if (wait < 0) throw McpError.Invalid("'wait' is seconds, 0 or more.");
            string start = ReadStart(call, "on_spawn");
            bool overrideAi = call.Bool("override_ai"), snap = call.Bool("snap_to_floor", true), dryRun = call.Bool("dry_run");
            if (call.Has("entity") && !call.Has("composite")) throw McpError.Invalid("'entity' goes with 'composite'.");

            Npc npc = null;
            cTransform frame = null;
            List<Vector3> points = new List<Vector3>();
            Entity trigger = null;
            string triggerPin = null, npcName = null, prefix = null;
            int navClass = 3;
            NavigationMeshQuery query = null;
            string navWhy = null;
            List<string> notes = new List<string>();
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: !dryRun);
                Commands commands = content.Level.Commands;
                List<Npc> npcs = ReadNpcs(call, commands);
                int index = call.Int("placement", 0);
                if (index < 0 || index >= npcs.Count) throw McpError.Invalid("'placement' is 0-" + (npcs.Count - 1) + ".");
                npc = npcs[index];
                if (npcs.Count > 1) notes.Add(npc.Holder.name + " places it " + npcs.Count + " times: the route is in its script, so every placement walks it; the points were converted through placement " + index + ".");
                frame = HolderFrame(commands, npc);
                if (frame == null) throw new McpError(McpErrorCodes.Refused, npc.Holder.name + " is not placed in the level, so world points cannot be put in it. Place it first (or route an NPC that is placed).");
                npcName = McpScript.EntityName(commands, npc.Holder, npc.Entity);
                prefix = FreePrefix(commands, npc.Holder, call.Str("name")?.Trim() ?? "Route_" + npcName, "_Start", "_GoTo1", "_Point1");
                for (int i = 0; i < pointTokens.Count; i++)
                    points.Add(PlaceOf(commands, pointTokens[i], "points[" + i + "]").position);
                if (call.Has("trigger"))
                {
                    trigger = McpScript.FindEntity(commands, npc.Holder, call.Str("trigger"));
                    triggerPin = call.Str("trigger_pin") ?? EventPinOf(commands, npc.Holder, trigger);
                }
                if (start == "on_spawn" && npc.Archetype == null)
                    throw McpError.Invalid(npcName + " is a Character, not an NPC archetype instance, so it has no finished_spawning to start from: give start 'trigger' or 'none'.");
                navClass = NavClassOf(npc.Archetype, SettingOf(commands, npc, "character_class")?.Value);
                query = NavQuery(content.Level, out navWhy);
            });

            List<Floor> floors = snap ? Floors(call, points, query, navClass) : points.Select(o => new Floor() { Given = o, Point = o }).ToList();
            List<JObject> stood = snap ? McpEditor.UI(() => { Commands commands = McpEditor.RequireCommands(forEditing: false); return floors.Select(o => o.Describe(commands)).ToList(); }) : null;
            JArray route = new JArray();
            List<string> warnings = new List<string>();
            for (int i = 0; i < floors.Count; i++)
            {
                JObject row = new JObject() { ["point"] = i + 1, ["position"] = McpValues.Vector(floors[i].Point) };
                if (snap) row["floor"] = stood[i];
                if (query != null)
                {
                    row["navmesh"] = NavAt(query, floors[i].Point, navClass, out bool on);
                    if (!on) warnings.Add("Point " + (i + 1) + " is not on navmesh for class " + NavClassNames[navClass] + " (as last built).");
                    int next = i + 1 < floors.Count ? i + 1 : (mode == "loop" ? 0 : -1);
                    if (next >= 0 && next != i)
                    {
                        NavigationMeshQuery.PathResult path;
                        lock (_navLock) path = query.FindPath(floors[i].Point, floors[next].Point, new NavigationMeshQuery.Options() { Classes = 1 << navClass, IncludeDisabled = true });
                        JObject leg = new JObject() { ["to"] = next + 1, ["reachable"] = path.Reachable };
                        if (path.Reachable) leg["length_m"] = Math.Round(path.Length, 1);
                        else leg["why"] = path.Failure ?? "the path stops short of it";
                        row["leg"] = leg;
                        if (!path.Reachable) warnings.Add("The navmesh (as last built) has no way from point " + (i + 1) + " to point " + (next + 1) + " for class " + NavClassNames[navClass] + ": it will stop there (the failed move waits and goes on to the next).");
                    }
                }
                route.Add(row);
            }
            if (query == null && navWhy != null) notes.Add(navWhy);

            //The points in the NPC's composite, each marker facing the next point
            List<cTransform> markers = new List<cTransform>();
            for (int i = 0; i < floors.Count; i++)
            {
                int next = i + 1 < floors.Count ? i + 1 : (mode == "loop" ? 0 : i);
                Vector3 toward = floors[next].Point - floors[i].Point;
                toward.Y = 0;
                Vector3 facing = toward.LengthSquared() < 1e-6f ? Vector3.Zero : InstanceTransform.LookRotation(toward);
                markers.Add(InstanceTransform.ToLocal(frame, new cTransform(floors[i].Point, facing)));
            }

            string startText = start == "on_spawn" ? "when " + npcName + " finishes spawning" : start == "trigger" ? "when " + McpEditor.UI(() => Event(McpEditor.RequireCommands(forEditing: false), npc.Holder, trigger, triggerPin)) + " fires" : "when script fires " + prefix + "_Start.trigger";
            JObject result = new JObject()
            {
                ["npc"] = npcName,
                ["composite"] = npc.Holder.name,
                ["space"] = "world",
                ["mode"] = mode,
                ["move"] = move.ToLowerInvariant(),
                ["wait_s"] = wait,
                ["starts"] = startText,
                ["route"] = route,
            };
            foreach (string note in notes) call.Note(note);
            if (warnings.Count != 0) result["warnings"] = new JArray(warnings);
            if (dryRun)
            {
                result["dry_run"] = true;
                result["would_make"] = floors.Count + " PositionMarkers, " + floors.Count + " CMD_GoTo, " + (wait > 0 ? (mode == "loop" ? floors.Count : floors.Count - 1) : 0) + " CMD_Idle and a TriggerBindCharacter in " + npc.Holder.name;
                result["next"] = "Call again without dry_run to make it, as one undo step.";
                return result;
            }

            JObject made = new JObject();
            string page = call.Str("page") ?? prefix;
            McpScriptEdit.Run("Route for " + npcName, npc.Holder, edit =>
            {
                Commands commands = edit.Commands;
                Composite holder = npc.Holder;
                edit.UsePage(holder, page);
                List<FunctionEntity> gotos = new List<FunctionEntity>(), waits = new List<FunctionEntity>(), marks = new List<FunctionEntity>();
                for (int i = 0; i < floors.Count; i++)
                {
                    FunctionEntity marker = edit.AddFunction(holder, FunctionType.PositionMarker, prefix + "_Point" + (i + 1));
                    edit.SetParameter(holder, marker, "position", Transform(markers[i]));
                    marks.Add(marker);
                    FunctionEntity go = edit.AddFunction(holder, FunctionType.CMD_GoTo, prefix + "_GoTo" + (i + 1));
                    edit.SetParameter(holder, go, "move_type", new JValue(move));
                    edit.SetParameter(holder, go, "override_all_ai", new JValue(overrideAi));
                    edit.AddLink(holder, go, "Waypoint", marker, "reference");
                    gotos.Add(go);
                    bool last = i == floors.Count - 1;
                    if (wait > 0 && !(last && mode == "once"))
                    {
                        FunctionEntity idle = edit.AddFunction(holder, FunctionType.CMD_Idle, prefix + "_Wait" + (i + 1));
                        edit.SetParameter(holder, idle, "duration", new JValue(wait));
                        edit.SetParameter(holder, idle, "override_all_ai", new JValue(overrideAi));
                        waits.Add(idle);
                    }
                    else waits.Add(null);
                }
                for (int i = 0; i < gotos.Count; i++)
                {
                    FunctionEntity next = i + 1 < gotos.Count ? gotos[i + 1] : (mode == "loop" ? gotos[0] : null);
                    if (waits[i] != null)
                    {
                        edit.AddLink(holder, gotos[i], "succeeded", waits[i], "apply_start");
                        edit.AddLink(holder, gotos[i], "failed", waits[i], "apply_start");
                        if (next != null) edit.AddLink(holder, waits[i], "finished", next, "apply_start");
                    }
                    else if (next != null)
                    {
                        edit.AddLink(holder, gotos[i], "succeeded", next, "apply_start");
                        edit.AddLink(holder, gotos[i], "failed", next, "apply_start");
                    }
                }
                FunctionEntity bind = edit.AddFunction(holder, FunctionType.TriggerBindCharacter, prefix + "_Start");
                edit.AddLink(holder, bind, "characters", npc.Entity, npc.Archetype != null ? "npc_reference" : "reference");
                edit.AddLink(holder, bind, "bound_trigger", gotos[0], "apply_start");
                if (start == "on_spawn") edit.AddLink(holder, npc.Entity, "finished_spawning", bind, "trigger");
                else if (start == "trigger") edit.AddLink(holder, trigger, triggerPin, bind, "trigger");
                made["bind"] = McpScript.Brief(commands, holder, bind);
                made["goto"] = new JArray(gotos.Select(o => McpScript.Id(o.shortGUID)));
                made["wait"] = new JArray(waits.Where(o => o != null).Select(o => McpScript.Id(o.shortGUID)));
                made["markers"] = new JArray(marks.Select(o => McpScript.Id(o.shortGUID)));
                edit.Select.Add(bind);
            }, show: false);
            result["made"] = made;
            result["stop"] = "To stop it, bind the NPC with another TriggerBindCharacter whose bound_trigger goes to each " + prefix + "_GoTo's and _Wait's apply_stop, or despawn it.";
            result["next"] = "save_level with build=true, then in game runtime_utils action 'activity' shows the moves firing (apply_start, succeeded, failed).";
            return result;
        }
        #endregion

        #region play_character_animation
        private static object PlayCharacterAnimation(McpCall call)
        {
            JToken characterToken = call.Token("character") ?? throw McpError.Invalid("'character' is required: an NPC's path from the root composite (describe_npc lists them), or 'player'.");
            bool player = characterToken.Type == JTokenType.String && string.Equals(((string)characterToken).Trim(), "player", StringComparison.OrdinalIgnoreCase);
            string clipName = call.Str("animation", required: true).Trim();
            bool secondary = call.Bool("secondary"), dryRun = call.Bool("dry_run");
            string start = ReadStart(call, player ? "none" : "on_spawn");
            if (player && start == "on_spawn") throw McpError.Invalid("The player has no finished_spawning to start from: give start 'trigger' (with a trigger) or 'none'.");
            if (call.Has("loop") && call.Has("play_count")) throw McpError.Invalid("Give 'loop' or 'play_count', not both.");
            if (call.Has("yaw") && !call.Has("at")) throw McpError.Invalid("'yaw' faces the marker 'at' makes: give 'at' too.");
            if (secondary && call.Has("at")) throw McpError.Invalid("A secondary animation plays where the character is, over what it is doing: it takes no 'at' marker.");

            Npc npc = null;
            Composite composite = null;
            Anim.AnimationSet set = null;
            Anim.ClipReference clip = null;
            cTransform marker = null;
            Entity trigger = null;
            string triggerPin = null, who = null;
            List<string> warnings = new List<string>();
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: !dryRun);
                Commands commands = content.Level.Commands;
                Composite root = commands.EntryPoints[0];
                string setName = call.Str("animation_set")?.Trim();
                if (!player)
                {
                    npc = ReadNpcs(call, commands, "character")[0];
                    who = McpScript.EntityName(commands, npc.Holder, npc.Entity);
                    composite = call.Has("composite") ? McpScript.FindComposite(commands, call.Str("composite")) : npc.Holder;
                    if (composite != npc.Holder)
                        throw McpError.Invalid(who + " is in " + npc.Holder.name + ": build it there (leave out 'composite'). A TriggerBindCharacter only links to an NPC in its own composite.");
                    if (setName == null)
                    {
                        Setting animSet = SettingOf(commands, npc, "anim_set");
                        setName = animSet?.Value?.Type == JTokenType.String ? (string)animSet.Value : null;
                        if (string.IsNullOrEmpty(setName)) throw McpError.Invalid(who + "'s anim_set could not be worked out: give 'animation_set'.");
                    }
                }
                else
                {
                    who = "the player";
                    composite = call.Has("composite") ? McpScript.FindComposite(commands, call.Str("composite")) : root;
                }

                Anim animations = Singleton.Animations;
                if (animations == null || !animations.Loaded)
                    throw McpError.Busy("ANIMATION.PAK is not loaded yet, so the clip cannot be checked. Try again in a moment.");
                if (setName != null)
                {
                    set = McpGlobalAssetChecks.FindSet(setName);
                    if (set == null) throw McpError.NotFound("animation set", setName, animations.Sets.Where(o => o.Kind != Anim.AnimationKind.Environment).Select(o => o.Name), "list_animation_sets kind character lists them.");
                }
                else
                {
                    List<Anim.AnimationSet> holding = animations.Sets.Where(o => o.Kind != Anim.AnimationKind.Environment && McpGlobalAssetChecks.FindClip(o, clipName) != null).ToList();
                    if (holding.Count == 0) throw McpError.NotFound("character animation clip", clipName, animations.Sets.Where(o => o.Kind != Anim.AnimationKind.Environment).SelectMany(o => o.Contexts.SelectMany(c => c.Clips)).Select(o => o.Name).Distinct(), "list_animations finds clips by name.");
                    //The player plays first-person sets: among several, those come first
                    List<Anim.AnimationSet> firstPerson = holding.Where(IsFirstPerson).ToList();
                    if (holding.Count > 1 && firstPerson.Count == 1) holding = firstPerson;
                    if (holding.Count > 1) throw McpError.Ambiguous("character animation sets", clipName, holding.Take(12).Select(o => new JObject() { ["name"] = o.Name, ["type"] = "rig " + o.Skeleton }), "Give 'animation_set'.");
                    set = holding[0];
                }
                if (player && !IsFirstPerson(set))
                    warnings.Add(set.Name + " is not a first-person set: the player plays the 1ST_PERSON sets (1ST_PERSON, 1ST_PERSON_RIPLEY, 1ST_PERSON_SPACESUIT...), so this clip may not play on them (list_animation_sets filter 1ST_PERSON).");
                if (set.Kind == Anim.AnimationKind.Environment) throw McpError.Invalid(set.Name + " is a prop's animation set: a character plays a character set (PlayEnvironmentAnimation plays props; describe_animated_prop).");
                clip = McpGlobalAssetChecks.FindClip(set, clipName);
                if (clip == null)
                {
                    List<string> elsewhere = animations.Sets.Where(o => o != set && o.Kind != Anim.AnimationKind.Environment && McpGlobalAssetChecks.FindClip(o, clipName) != null).Select(o => o.Name).Take(8).ToList();
                    throw McpError.NotFound("clip in " + set.Name, clipName, set.Contexts.SelectMany(o => o.Clips).Select(o => o.Name),
                        (elsewhere.Count != 0 ? "These sets have it: " + string.Join(", ", elsewhere) + " (pass animation_set). " : "") + "list_animations set=" + set.Name + " lists its clips.");
                }
                if (!string.IsNullOrWhiteSpace(clip.Context?.Name))
                    warnings.Add("'" + clip.Name + "' is in " + set.Name + "'s " + clip.Context.Name + " context: it plays only while the character is in that state.");
                //Whether the set plays on this character's rig: its own set's rig, or one the game retargets onto it
                if (npc != null)
                {
                    Setting own = SettingOf(commands, npc, "anim_set");
                    Anim.AnimationSet ownSet = own?.Value?.Type == JTokenType.String ? McpGlobalAssetChecks.FindSet((string)own.Value) : null;
                    if (ownSet != null && ownSet != set && !McpGlobalAssetChecks.SetsPlayableOn(ownSet.Skeleton).Any(o => o.Item1 == set))
                        warnings.Add(set.Name + " is authored on " + set.Skeleton + " and nothing retargets it onto " + who + "'s rig " + ownSet.Skeleton + " (its anim_set " + ownSet.Name + "): it may not play. list_animations plays_on=" + ownSet.Name + " lists clips that do.");
                }

                if (call.Has("at"))
                {
                    cTransform at = PlaceOf(commands, call.Token("at"), "at");
                    Vector3 facing = call.Has("yaw") ? new Vector3(0, (float)call.Num("yaw"), 0) : at.rotation;
                    cTransform frame = McpSpatialTools.FrameOf(commands, composite, null, "a composite placed once (build it in the root)", out JObject _);
                    marker = InstanceTransform.ToLocal(frame, new cTransform(at.position, facing));
                }
                if (call.Has("trigger"))
                {
                    trigger = McpScript.FindEntity(commands, composite, call.Str("trigger"));
                    triggerPin = call.Str("trigger_pin") ?? EventPinOf(commands, composite, trigger);
                }
            });

            FunctionType type = secondary ? FunctionType.CHR_PlaySecondaryAnimation : FunctionType.CMD_PlayAnimation;
            string name = McpEditor.UI(() => FreePrefix(McpEditor.RequireCommands(forEditing: false), composite, call.Str("name")?.Trim() ?? clip.Name, "", "_Bind", "_Marker"));
            JObject result = new JObject()
            {
                ["character"] = who,
                ["composite"] = composite.name,
                ["command"] = type.ToString(),
                ["animation_set"] = set.Name,
                ["animation"] = clip.Name,
            };
            if (clip.Duration > 0) result["duration_s"] = Math.Round(clip.Duration, 3);
            if (marker != null) result["marker"] = new JObject() { ["space"] = "composite " + composite.name, ["position"] = McpValues.Vector(marker.position), ["rotation"] = McpValues.Vector(marker.rotation) };
            result["starts"] = start == "on_spawn" ? "when " + who + " finishes spawning" : start == "trigger" ? "when " + McpEditor.UI(() => Event(McpEditor.RequireCommands(forEditing: false), composite, trigger, triggerPin)) + " fires" : "when script fires " + name + "_Bind.trigger";
            if (dryRun)
            {
                result["dry_run"] = true;
                if (warnings.Count != 0) result["warnings"] = new JArray(warnings);
                result["next"] = "Call again without dry_run to make it, as one undo step.";
                return result;
            }

            FunctionEntity command = null;
            McpScriptEdit.Run("Play " + clip.Name + " on " + who, composite, edit =>
            {
                Commands commands = edit.Commands;
                if (call.Has("page")) edit.UsePage(composite, call.Str("page"));
                command = edit.AddFunction(composite, type, name);
                edit.SetParameter(composite, command, "AnimationSet", new JValue(set.Name));
                edit.SetParameter(composite, command, "Animation", new JValue(clip.Name));
                if (call.Bool("loop")) edit.SetParameter(composite, command, "PlayCount", new JValue(-1));
                else if (call.Has("play_count")) edit.SetParameter(composite, command, "PlayCount", new JValue(call.Int("play_count")));
                if (call.Has("play_speed")) edit.SetParameter(composite, command, "PlaySpeed", new JValue(call.Num("play_speed")));
                if (call.Has("blend_in")) edit.SetParameter(composite, command, "BlendInTime", new JValue(call.Num("blend_in")));
                if (call.Has("blend_out")) edit.SetParameter(composite, command, "BlendOutTime", new JValue(call.Num("blend_out")));
                if (call.Has("allow_interruption")) edit.SetParameter(composite, command, "AllowInterruption", new JValue(call.Bool("allow_interruption")));
                if (!secondary)
                {
                    if (call.Has("start_frame")) edit.SetParameter(composite, command, "StartFrame", new JValue(call.Int("start_frame")));
                    if (call.Has("end_frame")) edit.SetParameter(composite, command, "EndFrame", new JValue(call.Int("end_frame")));
                    edit.SetParameter(composite, command, "override_all_ai", new JValue(call.Bool("override_ai", true)));
                }
                else if (call.Has("start_frame") || call.Has("end_frame") || call.Has("override_ai"))
                    warnings.Add("start_frame, end_frame and override_ai are CMD_PlayAnimation's: a secondary animation does not take them.");
                if (marker != null)
                {
                    FunctionEntity place = edit.AddFunction(composite, FunctionType.PositionMarker, name + "_Marker");
                    edit.SetParameter(composite, place, "position", Transform(marker));
                    edit.AddLink(composite, command, "Marker", place, "reference");
                    edit.SetParameter(composite, command, "LocationConvergence", new JValue(true));
                    edit.SetParameter(composite, command, "OrientationConvergence", new JValue(true));
                }
                FunctionEntity bind = edit.AddFunction(composite, FunctionType.TriggerBindCharacter, name + "_Bind");
                if (player)
                {
                    FunctionEntity thePlayer = edit.AddFunction(composite, FunctionType.VariableThePlayer, null);
                    edit.AddLink(composite, bind, "characters", thePlayer, "reference");
                }
                else
                    edit.AddLink(composite, bind, "characters", npc.Entity, npc.Archetype != null ? "npc_reference" : "reference");
                edit.AddLink(composite, bind, "bound_trigger", command, "apply_start");
                if (start == "on_spawn") edit.AddLink(composite, npc.Entity, "finished_spawning", bind, "trigger");
                else if (start == "trigger") edit.AddLink(composite, trigger, triggerPin, bind, "trigger");
                result["made"] = new JObject() { ["command"] = McpScript.Brief(commands, composite, command), ["bind"] = McpScript.Brief(commands, composite, bind) };
                edit.Select.Add(command);
            });

            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                foreach (string problem in McpGlobalAssetChecks.AnimationProblems(commands, composite, command)) warnings.Add(problem);
            });
            if (warnings.Count != 0) result["warnings"] = new JArray(warnings);
            result["pins"] = new JObject()
            {
                ["finished"] = "fires when it has played",
                ["Interrupted"] = "fires when something cut it short",
                ["command_started"] = "fires as it starts",
                ["apply_stop"] = "method: stops it (fire it through a TriggerBindCharacter binding the same character)",
            };
            result["next"] = "Chain what follows from " + name + ".finished. It reaches the game at save_level; in game runtime_utils action 'activity' shows it firing.";
            return result;
        }
        #endregion
        #endregion
    }
}
