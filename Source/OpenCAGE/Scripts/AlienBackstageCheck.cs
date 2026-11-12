using CATHODE;
using CATHODE.Enums;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// A level that can spawn the alien needs at least one PathfindingAlienBackstageNode, or the game crashes the first time
    /// the alien breaks out to go backstage: flamed until it retreats, despawned, or from an ambush. ATOMIC_BREAKOUT::start
    /// asks find_nearest_pathfinding_alien_backstage_node_position_to_pos for the nearest vent, which reads the list of
    /// backstage nodes before it checks there is one - a null read in a level with none. Nothing in the alien's AI stops it
    /// first: the behaviour tree's SHOULD_BREAKOUT branch runs ActionBreakout without ConditionCanBreakout. (Found on
    /// HALO_FLOOD, 30 Oct 2026.) Retail keeps its backstage nodes in vent composites, Archetypes\Jobs\VentTraversalMarker
    /// and its 6M, 9m and _Backstage kin. Save & Build runs this check and says what it finds; the save goes ahead.
    /// Commands is only read. Any thread.
    /// </summary>
    public static class AlienBackstageCheck
    {
        //What makes a Character the alien: its rig, animations and animation tree, on the Character itself or on the
        //variables that feed it (retail's Xenomorph_NPC sets them on its own variables, for the Character in NPC_Logic), or
        //its class
        private static readonly ShortGuid[] _rigParameters = new[] { "reference_skeleton", "anim_set", "anim_tree_set" }.Select(o => ShortGuidUtils.Generate(o)).ToArray();
        private static readonly ShortGuid _characterClass = ShortGuidUtils.Generate("character_class");
        private static readonly ShortGuid _characterClassEnum = ShortGuidUtils.Generate("CHARACTER_CLASS");

        /* Two retail levels ship an alien and no backstage node, and play as shipped: BSP_TORRENS's alien is the epilogue's
           (spawned for its cutscene, despawned behind a fade to black) and TECH_MUTHRCORE's is in its ambient mission. The
           aliens these composites place are not counted, so neither level is flagged as it ships - an alien placed anywhere
           else in them is. */
        private static readonly HashSet<string> _shippedWithoutNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"SCRIPT_StoryMission\M34_Epilogue\M34_Pt01\M34_Pt01",
            @"SCRIPT_AmbientMission\TECH_MuthrCore\AmbientMission_01\AMB_01_SCRIPT",
        };

        public sealed class Result
        {
            /// <summary>The placed composites that make a Character the alien: an alien archetype, or one holding the Character.</summary>
            public List<Composite> Aliens = new List<Composite>();
            /// <summary>PathfindingAlienBackstageNode entities in composites the level places.</summary>
            public int PlacedNodes;
            /// <summary>PathfindingAlienBackstageNode entities in composites the level holds but never places.</summary>
            public int UnplacedNodes;
            /// <summary>The game will crash the first time the alien breaks out.</summary>
            public bool Crashes => Aliens.Count != 0 && PlacedNodes == 0;
        }

        public static Result Check(Commands commands)
        {
            Result result = new Result();
            if (commands?.Entries == null)
                return result;

            //Composites only count where the level places them: those it holds but never places are not made in game
            HashSet<Composite> placed = Placed(commands, null);
            foreach (Composite composite in commands.Entries)
            {
                if (composite == null)
                    continue;
                int nodes = composite.functions.Count(o => o.function.IsFunctionType && o.function.AsFunctionType == FunctionType.PathfindingAlienBackstageNode);
                if (placed.Contains(composite))
                    result.PlacedNodes += nodes;
                else
                    result.UnplacedNodes += nodes;
            }
            if (result.PlacedNodes != 0)
                return result;

            foreach (Composite composite in Placed(commands, _shippedWithoutNodes))
                if (MakesAlien(composite))
                    result.Aliens.Add(composite);
            result.Aliens.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        /// <summary>What to tell the user when the level will crash the first time the alien breaks out, or null.</summary>
        public static string Warning(Commands commands)
        {
            Result result = Check(commands);
            if (!result.Crashes)
                return null;
            List<string> aliens = result.Aliens.Select(o => o.name).ToList();
            string where = string.Join(", ", aliens.Take(3)) + (aliens.Count > 3 ? " and " + (aliens.Count - 3) + " more" : "");
            return "The alien can spawn in this level (" + where + "), but the level has no PathfindingAlienBackstageNode" +
                (result.UnplacedNodes != 0 ? " placed (it holds " + result.UnplacedNodes + " in composites it never places)" : "") +
                ", so the game will crash the first time the alien retreats or despawns: when it is flamed, sent away by the script, or after an ambush. " +
                "Give the alien vents to go backstage through - File > Alien Vents... (or MCP add_alien_vents) finds spots under ceilings and places retail's vent composites " +
                "(Archetypes\\Jobs\\VentTraversalMarker, 6M, 9m), 3 or more spread out - then Save & Build again, with the navmesh baker on, to bake the backstage navmesh.";
        }

        /* The composites placed from the level's entry points (its root, GLOBAL and the pause menu) down, not going into
           those in skip. A level without entry points has every composite counted. */
        private static HashSet<Composite> Placed(Commands commands, HashSet<string> skip)
        {
            HashSet<Composite> placed = new HashSet<Composite>();
            Stack<Composite> todo = new Stack<Composite>();
            Composite[] entryPoints = commands.EntryPoints;
            if (entryPoints == null || entryPoints[0] == null)
            {
                foreach (Composite composite in commands.Entries)
                    if (composite != null && (skip == null || !skip.Contains(composite.name)))
                        placed.Add(composite);
                return placed;
            }
            foreach (Composite entry in entryPoints)
                if (entry != null && (skip == null || !skip.Contains(entry.name)) && placed.Add(entry))
                    todo.Push(entry);
            while (todo.Count != 0)
            {
                foreach (FunctionEntity function in todo.Pop().functions)
                {
                    if (function.function.IsFunctionType)
                        continue;
                    Composite child = commands.GetComposite(function.function);
                    if (child != null && (skip == null || !skip.Contains(child.name)) && placed.Add(child))
                        todo.Push(child);
                }
            }
            return placed;
        }

        /* Whether a composite makes a Character the alien: a Character in it with the ALIEN class, or anything in it - the
           Character itself, the composite's own variables (an archetype's settings), an instance's or an alias's override -
           giving a Character setting the ALIEN rig, animation set or animation tree. The facehugger's are its own (FACEHUGGER):
           it never goes backstage. */
        private static bool MakesAlien(Composite composite)
        {
            foreach (Entity entity in composite.GetEntities())
            {
                foreach (Parameter parameter in entity.parameters)
                {
                    if (parameter?.content == null)
                        continue;
                    if (parameter.content is cString text && Array.IndexOf(_rigParameters, parameter.name) != -1 &&
                        string.Equals(text.value?.Trim(), "ALIEN", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (parameter.name == _characterClass && parameter.content is cEnum value && value.enumID == _characterClassEnum &&
                        value.enumIndex == (int)CHARACTER_CLASS.ALIEN && entity is FunctionEntity function &&
                        function.function.IsFunctionType && function.function.AsFunctionType == FunctionType.Character)
                        return true;
                }
            }
            return false;
        }
    }
}
