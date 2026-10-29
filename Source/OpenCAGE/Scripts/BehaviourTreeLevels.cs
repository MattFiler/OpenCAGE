using CATHODE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// Each level lists the root behaviour trees its characters can run (WORLD/BEHAVIOR_TREE.DB). The list is generated from
    /// the character attribute configs when the level is saved (CathodeLib's Level.Save), so after the configs or the trees
    /// change, levels that haven't been saved since still have the old list - and a character whose tree its level doesn't
    /// list, or a list holding a tree that no longer loads, crashes the game. This finds those levels; it changes nothing.
    /// </summary>
    public static class BehaviourTreeLevels
    {
        public static string DataFolder => Path.GetFullPath(Path.Combine(Singleton.PathToAI, "DATA"));

        /// <summary>What the configs currently ask of every level's list (cached until they change).</summary>
        public static BehaviorTreeDB.Requirements Requirements => BehaviorTreeDB.ReadRequirements(DataFolder);

        /// <summary>
        /// The levels (by path under DATA/ENV) whose list doesn't match the configs: one of the trees they ask for is missing,
        /// or a tree it lists can't be loaded. Empty when the configs can't be read.
        /// </summary>
        public static List<string> OutOfStep(BehaviorTreeDB.Requirements requirements)
        {
            List<string> levels = new List<string>();
            string env = Path.Combine(DataFolder, "ENV");
            if (requirements == null || !requirements.Readable || !Directory.Exists(env))
                return levels;

            foreach (string world in Directory.EnumerateDirectories(env, "WORLD", SearchOption.AllDirectories))
            {
                string path = Path.Combine(world, "BEHAVIOR_TREE.DB");
                if (!File.Exists(path))
                    continue;

                //The game (and so a save) uses a BSPNostromo level's <level>_PATCH/WORLD instead of its own: check the list it
                //reads, under the name the level is opened by
                string level = Path.GetDirectoryName(world).Substring(env.Length).TrimStart('\\', '/').Replace('\\', '/');
                bool nostromo = level.StartsWith("PRODUCTION/DLC/BSPNOSTROMO", StringComparison.OrdinalIgnoreCase);
                if (nostromo && !level.EndsWith("_PATCH", StringComparison.OrdinalIgnoreCase) && Directory.Exists(Path.Combine(env, level + "_PATCH", "WORLD")))
                    continue;
                if (nostromo && level.EndsWith("_PATCH", StringComparison.OrdinalIgnoreCase))
                    level = level.Substring(0, level.Length - "_PATCH".Length);

                List<string> listed = new BehaviorTreeDB(path).Entries;
                if (requirements.Trees.Any(o => !listed.Contains(o)) || listed.Any(o => !requirements.IsLoadable(o)))
                    levels.Add(level);
            }
            return levels;
        }

        /// <summary>
        /// What a user should hear after the configs or trees changed: problems with the configs, and the levels to save.
        /// Null when there is nothing to say.
        /// </summary>
        public static string Describe()
        {
            BehaviorTreeDB.Requirements requirements = Requirements;
            List<string> levels = OutOfStep(requirements);
            List<string> lines = new List<string>(requirements.Problems);
            if (levels.Count != 0)
                lines.Add(levels.Count + " level" + (levels.Count == 1 ? "" : "s") + " still list" + (levels.Count == 1 ? "s" : "") + " the old behaviour trees: " +
                    string.Join(", ", levels.Take(10)) + (levels.Count > 10 ? " and " + (levels.Count - 10) + " more" : "") +
                    ". A level's list is regenerated when the level is saved, so save a level before playing it.");
            return lines.Count == 0 ? null : string.Join("\n\n", lines);
        }
    }
}
