using CATHODE;
using CathodeLib;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>The alien with no backstage vent node to break out to (<see cref="AlienBackstageCheck"/>), as a Level Check finding.</summary>
    internal sealed class AlienBackstageRule : LevelCheckRule
    {
        public override string Id => "alien_backstage";
        public override string Name => "Alien without vents";
        public override string About => "An alien that can spawn in a level with no PathfindingAlienBackstageNode crashes the game the first time it retreats (flamed, sent away, after an ambush).";

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            AlienBackstageCheck.Result result = AlienBackstageCheck.Check(context.Commands);
            if (result.Crashes)
            {
                LevelCheckFinding finding = new LevelCheckFinding()
                {
                    Severity = LevelCheckSeverity.Crash,
                    Title = "The alien has no vents to retreat into",
                    Text = AlienBackstageCheck.Warning(context.Commands),
                    Fix = AddVentsFix(0, 0),
                };
                foreach (Composite alien in result.Aliens)
                    finding.Sources.Add(new LevelCheckSource() { Composite = alien, Text = alien.name });
                found.Add(finding);
                return;
            }

            //The vents the user placed or changed, as the navmesh baker and the game use them (Scripts\AlienVents\AlienVentsCheck.cs):
            //a network of fewer than 3 (no backstage sheet), in a line, all within 10 m, not baked yet, off the navmesh
            VentReport vents = AlienVents.Check(context.Level, context.IsUsers);
            foreach (VentIssue issue in vents.Issues)
            {
                LevelCheckFinding finding = new LevelCheckFinding() { Key = issue.Key, Severity = issue.Severity, Title = issue.Title, Text = issue.Text };
                foreach (PlacedVent vent in issue.Vents.Take(20))
                    finding.Sources.Add(new LevelCheckSource()
                    {
                        Composite = vent.Instance != null ? vent.Holder : vent.NodeComposite,
                        Entity = (Entity)vent.Instance ?? vent.Node,
                        Chain = vent.Instance != null ? vent.Chain.Take(vent.Chain.Count - 1).ToList() : vent.Chain,
                        Text = vent.Where + " " + McpRegion.Format(vent.Bottom),
                    });
                if (issue.Kind == "no_sheet") finding.Fix = AddVentsFix(issue.Network, Math.Max(1, 3 - issue.Vents.Count));
                else if (issue.Kind == "colinear") finding.Fix = AddVentsFix(issue.Network, 1);
                else if (issue.Kind == "close_together") finding.Fix = AddVentsFix(issue.Network, 2);
                found.Add(finding);
            }
        }

        /* Add vents: from the Level Check window, the Alien Vents window (pick the spots); from MCP fix_level_check, found and placed at once */
        private static LevelCheckFix AddVentsFix(int network, int count)
        {
            return new LevelCheckFix()
            {
                Label = "Add vents...",
                Detail = "Finds spots on the navmesh under ceilings retail's vent composites fit (VentTraversalMarker 4.25 m, 6M, 9m) and places " +
                    (count > 0 ? count + " more vent(s)" : "3 or more vents, spread out,") + " in network " + network + " as one undo step, bringing the vent composites in from a retail level of this install first " +
                    "(not undoable, as any import). From the Level Check window it opens File > Alien Vents to pick the spots. Then Save & Build with the navmesh baker to bake the alien's backstage.",
                Apply = fromTool =>
                {
                    if (!fromTool)
                    {
                        Popups.AlienVentsWindow.Open(network, count);
                        return "The Alien Vents window is open: Find Spots, tick the ones to keep, Place Ticked Vents, then Save & Build.";
                    }
                    LevelContent content = Singleton.Editor?.CompositeBrowser?.Content ?? throw new McpError("No level is open.");
                    VentsPlaced placed = AlienVents.AddAutomatically(AlienVents.EditorCall(), content, new VentOptions() { Count = count, Network = network }, "AI: Level Check: ", out VentProposal proposal);
                    return "Placed " + placed.Names.Count + " vent(s) in " + placed.Into.name + " (" + string.Join(", ", placed.Names) + "): " + string.Join(" ", placed.Notes.Concat(proposal.Notes)) +
                        " Save & Build with the navmesh baker to bake the backstage (Undo takes the vents out).";
                },
            };
        }
    }

    /// <summary>
    /// The game holds at most 18 characters at once (player and undespawned corpses included) in one fixed pool (ACTIVE_CHARACTERS,
    /// 0x12 entries). A spawn checks for a free slot without reserving it, so a 19th character or two spawns racing for the last slot
    /// write through a null pointer (request_character, GOG AI+0x3d04f6 / Steam 0x7D0546). Each placed Character is one the script
    /// can have alive, so the count is what the level CAN reach; its script may keep some apart.
    /// </summary>
    internal sealed class CharacterCapRule : LevelCheckRule
    {
        public const int Pool = 18;
        /// <summary>Above this, one more spawn racing another can find the pool empty.</summary>
        public const int Safe = Pool - 1;

        public override string Id => "character_cap";
        public override string Name => "Character cap";
        public override string About => "The game holds 18 characters at once, the player and corpses included; the 19th crashes it. Counts the Characters the level places (each can be alive at once).";
        public override IEnumerable<FunctionType> Walks => new[] { FunctionType.Character };

        /* How many Characters each retail level places as shipped (this rule's own count: NPCs placed plus the player). Their scripts
           keep them under the cap by mission flow, so a retail level is only flagged when it places more than it shipped with. */
        internal static readonly Dictionary<string, int> RetailPlaced = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["PRODUCTION/BSP_LV426_PT01"] = 4, ["PRODUCTION/BSP_LV426_PT02"] = 4, ["PRODUCTION/BSP_TORRENS"] = 6,
            ["PRODUCTION/DLC/BSPNOSTROMO_RIPLEY"] = 3, ["PRODUCTION/DLC/BSPNOSTROMO_TWOTEAMS"] = 16,
            ["PRODUCTION/DLC/CHALLENGEMAP1"] = 2, ["PRODUCTION/DLC/CHALLENGEMAP3"] = 7, ["PRODUCTION/DLC/CHALLENGEMAP4"] = 12,
            ["PRODUCTION/DLC/CHALLENGEMAP5"] = 4, ["PRODUCTION/DLC/CHALLENGEMAP7"] = 3, ["PRODUCTION/DLC/CHALLENGEMAP9"] = 76,
            ["PRODUCTION/DLC/CHALLENGEMAP11"] = 7, ["PRODUCTION/DLC/CHALLENGEMAP12"] = 2, ["PRODUCTION/DLC/CHALLENGEMAP14"] = 5,
            ["PRODUCTION/DLC/CHALLENGEMAP16"] = 2, ["PRODUCTION/DLC/SALVAGEMODE1"] = 74, ["PRODUCTION/DLC/SALVAGEMODE2"] = 57,
            ["PRODUCTION/ENG_ALIEN_NEST"] = 17, ["PRODUCTION/ENG_REACTORCORE"] = 59, ["PRODUCTION/ENG_TOWPLATFORM"] = 30,
            ["PRODUCTION/FRONTEND"] = 1, ["PRODUCTION/HAB_AIRPORT"] = 39, ["PRODUCTION/HAB_CORPORATEPENT"] = 10,
            ["PRODUCTION/HAB_SHOPPINGCENTRE"] = 57, ["PRODUCTION/SCI_ANDROIDLAB"] = 73, ["PRODUCTION/SCI_HOSPITALLOWER"] = 34,
            ["PRODUCTION/SCI_HOSPITALUPPER"] = 28, ["PRODUCTION/SCI_HUB"] = 34, ["PRODUCTION/SOLACE"] = 5, ["PRODUCTION/TECH_COMMS"] = 59,
            ["PRODUCTION/TECH_HUB"] = 65, ["PRODUCTION/TECH_MUTHRCORE"] = 14, ["PRODUCTION/TECH_RND"] = 24, ["PRODUCTION/TECH_RND_HZDLAB"] = 22,
        };

        public sealed class Slot
        {
            public McpPlacements.Placement Placement;
            public FunctionEntity Character;
            public Composite Archetype;
            public bool Player, OnReset;
            /// <summary>The chain to the NPC: its archetype instance (the Character itself when it is placed on its own).</summary>
            public List<Entity> NpcChain;
            public string Kind;
        }

        /// <summary>The Characters the level places, each one possible character alive at once.</summary>
        public static List<Slot> Slots(LevelCheckContext context)
        {
            List<Slot> slots = new List<Slot>();
            //One character per NPC: an archetype holds more than one Character entity (its own, and NPC_Logic's, which stands for
            //the same character), so an NPC is counted once per placement of its archetype
            HashSet<string> npcs = new HashSet<string>();
            foreach (McpPlacements.Placement placement in context.RealPlacements(FunctionType.Character))
            {
                FunctionEntity character = placement.Chain[placement.Chain.Count - 1] as FunctionEntity;
                if (character == null) continue;
                Slot slot = new Slot() { Placement = placement, Character = character };
                slot.Player = LevelCheckContext.Bool(character, "is_player") == true;
                slot.OnReset = LevelCheckContext.Bool(character, "spawn_on_reset") == true;
                //The NPC is the outermost NPC archetype instance on the way down (a spawner placing an archetype, say)
                List<Composite> along = McpScript.CompositesAlong(context.Commands, FirstOf(context, placement), placement.Chain);
                int npcAt = placement.Chain.Count - 1;
                for (int i = 0; i < placement.Chain.Count - 1 && i + 1 < along.Count; i++)
                {
                    if (along[i + 1] != null && McpCharacterTools.IsNpcArchetype(along[i + 1])) { slot.Archetype = along[i + 1]; npcAt = i; break; }
                }
                if (!npcs.Add(string.Join("/", placement.Chain.Take(npcAt + 1).Select(o => o.shortGUID.ToByteString())))) continue;
                slot.NpcChain = placement.Chain.Take(npcAt + 1).ToList();
                slot.Kind = slot.Player ? "the player" : slot.Archetype != null ? slot.Archetype.name : "a Character in " + (context.HolderOf(placement)?.name ?? "?");
                slots.Add(slot);
            }
            return slots;
        }

        internal static Composite FirstOf(LevelCheckContext context, McpPlacements.Placement placement)
        {
            Entity first = placement.Chain[0];
            foreach (Composite entry in context.Commands.EntryPoints)
                if (entry != null && entry.GetEntityByID(first.shortGUID) == first) return entry;
            return context.Root;
        }

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            List<Slot> slots = Slots(context);
            int players = slots.Count(o => o.Player);
            //There is one player, however many player Characters the level holds (a challenge map has one per loadout, nine), and a
            //level with none of its own still has one
            int total = slots.Count(o => !o.Player) + 1;
            int atStart = slots.Count(o => o.OnReset && !o.Player) + 1;
            bool retail = RetailPlaced.TryGetValue(context.Level.Name, out int shipped);
            context.Report.Figures["characters"] = new JObject()
            {
                ["can_reach"] = total,
                ["at_level_start"] = atStart,
                ["player_characters"] = players,
                ["pool"] = Pool,
                ["retail_places"] = retail ? (JToken)shipped : null,
                ["by_kind"] = new JObject(slots.GroupBy(o => o.Kind).OrderByDescending(o => o.Count()).Select(o => new JProperty(o.Key, o.Count()))),
            };

            //Spawned together as the level starts: past the pool that is a crash on load, whatever the script does next
            if (atStart > Pool)
            {
                LevelCheckFinding start = new LevelCheckFinding()
                {
                    Key = "at_start",
                    Severity = LevelCheckSeverity.Crash,
                    Title = atStart + " characters spawn as the level starts (the game holds " + Pool + ", the player included)",
                    Text = "Every Character with spawn_on_reset spawns when the level loads, and the 19th character crashes the game. Untick spawn_on_reset on " +
                           (atStart - Safe) + " or more of them and spawn them later from the script, after others are despawned.",
                };
                foreach (Slot slot in slots.Where(o => o.OnReset && !o.Player))
                    start.Sources.Add(context.Source(slot.Placement, slot.Kind));
                found.Add(start);
            }

            if (total <= Safe) return;
            //A retail level as it ships keeps its characters apart by mission flow: only what was added on top of it is flagged
            if (retail && total <= shipped) return;
            LevelCheckFinding finding = new LevelCheckFinding()
            {
                Key = "can_reach",
                Severity = LevelCheckSeverity.Crash,
                Title = "The level can have " + total + " characters alive at once (the game holds " + Pool + ", the player included)",
                Text = "The game keeps every character - the player, each NPC and alien, and each corpse until it is despawned - in a pool of " + Pool +
                       "; the 19th crashes it, and so can a spawn racing another for the last slot. This level places " + total + " Characters" +
                       (retail ? " (the level shipped with " + shipped + ")" : "") + ", each of which the script can have alive. " +
                       "If the script never has more than " + Safe + " at a time (an intro actor despawned before the player spawns, one of two aliens chosen), dismiss this; " +
                       "otherwise despawn characters (and corpses: DespawnCharacter) before spawning more, or place fewer. The Debug Menu's Characters line shows the slots in use in game.",
            };
            foreach (IGrouping<string, Slot> kind in slots.GroupBy(o => o.Kind).OrderByDescending(o => o.Count()))
                foreach (Slot slot in kind)
                    finding.Sources.Add(context.Source(slot.Placement, slot.Kind + (slot.OnReset ? ", spawns at level start" : "")));
            found.Add(finding);
        }
    }

    /// <summary>
    /// An NPC whose display_model (as describe_npc resolves it for its placement: instance pins, aliases, the archetype's default) names a
    /// DisplayModel composite the level doesn't have: the game logs "Failed to find some valid components for a character spawner" and
    /// the NPC never appears.
    /// </summary>
    internal sealed class DisplayModelMissingRule : LevelCheckRule
    {
        public override string Id => "display_model_missing";
        public override string Name => "NPC with no display model";
        public override string About => "An NPC whose display_model names a DisplayModel composite the level doesn't have: the game can't make the character (\"Failed to find some valid components for a character spawner\").";
        public override IEnumerable<FunctionType> Walks => new[] { FunctionType.Character };

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            HashSet<string> models = new HashSet<string>(context.Commands.Entries.Where(o => o?.name != null && o.name.StartsWith("DisplayModel:", StringComparison.OrdinalIgnoreCase))
                .Select(o => o.name.Substring("DisplayModel:".Length)), StringComparer.OrdinalIgnoreCase);
            /* A display model with no composite of that name ('NPC', retail's usual) is a custom character: the game builds it from the
               level's character accessory sets (CHARACTERACCESSORYSETS.BIN), one entry per Character placement (its entity id and the
               composite instance it is in). An NPC with neither is the case the game can't build */
            HashSet<(ShortGuid, ShortGuid)> accessorised = new HashSet<(ShortGuid, ShortGuid)>(
                (context.Level.AccessorySets?.Entries ?? new List<CharacterAccessorySets.CharacterAttributes>()).Where(o => o?.character != null).Select(o => (o.character.entity_id, o.character.composite_instance_id)));
            Dictionary<string, List<CharacterCapRule.Slot>> missing = new Dictionary<string, List<CharacterCapRule.Slot>>(StringComparer.OrdinalIgnoreCase);
            int checkedNpcs = 0, custom = 0;
            foreach (CharacterCapRule.Slot slot in CharacterCapRule.Slots(context).Where(o => !o.Player))
            {
                //From the NPC (its archetype instance): the setting of the Character its npc_reference stands for, as describe_npc gives it
                JToken value = McpCharacterTools.ResolvedSetting(context.Commands, CharacterCapRule.FirstOf(context, slot.Placement), slot.NpcChain, null, "display_model");
                string name = value?.Type == JTokenType.String ? ((string)value)?.Trim() : null;
                if (string.IsNullOrEmpty(name)) continue;
                checkedNpcs++;
                if (name.StartsWith("DisplayModel:", StringComparison.OrdinalIgnoreCase)) name = name.Substring("DisplayModel:".Length);
                if (models.Contains(name)) continue;
                //The Character the archetype stands for (its npc_reference), from the root
                List<Entity> chain = new List<Entity>(slot.NpcChain);
                if (slot.Archetype != null) chain.AddRange(McpCharacterTools.MainCharacter(context.Commands, slot.Archetype, out _));
                EntityHandle handle = new EntityHandle(new EntityPath(chain.Select(o => o.shortGUID).Concat(new[] { ShortGuid.Invalid }).ToArray()));
                if (accessorised.Contains((handle.entity_id, handle.composite_instance_id))) { custom++; continue; }
                if (!missing.TryGetValue(name, out List<CharacterCapRule.Slot> list)) missing[name] = list = new List<CharacterCapRule.Slot>();
                list.Add(slot);
            }
            context.Report.Figures["display_models"] = new JObject() { ["in_level"] = models.Count, ["npcs_checked"] = checkedNpcs, ["custom_characters"] = custom, ["missing"] = new JArray(missing.Keys) };
            foreach (KeyValuePair<string, List<CharacterCapRule.Slot>> model in missing)
            {
                LevelCheckFinding finding = new LevelCheckFinding()
                {
                    Key = model.Key,
                    Severity = LevelCheckSeverity.Broken,
                    Title = model.Value.Count + " NPC" + (model.Value.Count == 1 ? " wears" : "s wear") + " display model '" + model.Key + "', which the level doesn't have",
                    Text = "There is no DisplayModel:" + model.Key + " composite in the level, and no character accessory set for these NPCs (what a custom character such as retail's 'NPC' is built from), so the game can't make the character: it logs \"Failed to find some valid components for a " +
                           "character spawner\" and the NPC never appears (a test that hit this crashed, with another problem present). Give the NPC a display model the level has " +
                           "(set_npc display_model, or the Character's display_model), or port DisplayModel:" + model.Key + " in from a level that has it.",
                };
                foreach (CharacterCapRule.Slot slot in model.Value.Take(10))
                    finding.Sources.Add(context.Source(slot.Placement, slot.Kind));
                found.Add(finding);
            }
        }
    }
}
