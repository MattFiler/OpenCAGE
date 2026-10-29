#if ENABLE_MOD_PACKAGES
using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.Modding
{
    /// <summary>
    /// The part of the user's changes to a level that a mod carries, as Create a Mod's list chose it: composites, assets
    /// changed in place, the level's own data. Built onto the level as it shipped by <see cref="ModLevelMerger.Extract"/>.
    /// </summary>
    public class LevelPart
    {
        /// <summary>The composites whose changes go in - added, changed or deleted ones.</summary>
        public HashSet<ShortGuid> Composites = new HashSet<ShortGuid>();
        /// <summary>The level's textures, models and materials changed in place that go in, by name.</summary>
        public HashSet<string> Textures = new HashSet<string>(), Models = new HashSet<string>(), Materials = new HashSet<string>();
        /// <summary>The level's own data that goes in (its galaxy, its character accessories...), as <see cref="CathodeLib.LevelMerger.StandaloneData"/> names it.</summary>
        public HashSet<string> StandaloneData = new HashSet<string>();
        /// <summary>Files of the level folder a level save doesn't write (its text), taken as they are: game-relative paths.</summary>
        public HashSet<string> LooseFiles = new HashSet<string>();
        /// <summary>Build the result as Save &amp; Build does (the user's level was built), rather than only save it.</summary>
        public bool Build;

        public bool IsEmpty => Composites.Count == 0 && Textures.Count == 0 && Models.Count == 0 && Materials.Count == 0 && StandaloneData.Count == 0 && LooseFiles.Count == 0;
    }

    /// <summary>
    /// What part of a level a chosen composite can't work without: the user's changes to other composites it reaches into
    /// for something the level didn't ship with - a pin added to a composite it instances, an entity added inside one that
    /// an alias, a trigger sequence or an animation of it points at. Composites the level didn't ship come with whatever
    /// uses them anyway.
    /// </summary>
    public static class LevelPartDependencies
    {
        /// <summary>The composites the chosen ones need that weren't chosen, each with why.</summary>
        public static Dictionary<ShortGuid, string> Needed(Level user, Level original, ISet<ShortGuid> chosen)
        {
            Dictionary<ShortGuid, string> needed = new Dictionary<ShortGuid, string>();
            HashSet<ShortGuid> seen = new HashSet<ShortGuid>(chosen);
            Queue<ShortGuid> queue = new Queue<ShortGuid>(chosen);
            while (queue.Count != 0)
            {
                Composite composite = user.Commands.GetComposite(queue.Dequeue());
                if (composite == null)
                    continue;
                foreach (KeyValuePair<Composite, string> reached in Reached(composite, user, original))
                {
                    if (!seen.Add(reached.Key.shortGUID))
                        continue;
                    needed[reached.Key.shortGUID] = reached.Value;
                    queue.Enqueue(reached.Key.shortGUID);
                }
                //A composite the level didn't ship comes with what uses it anyway, but what it needs in turn has to be looked for
                foreach (FunctionEntity function in composite.functions)
                {
                    Composite instanced = user.Commands.GetComposite(function.function);
                    if (instanced != null && original.Commands.GetComposite(instanced.shortGUID) == null && seen.Add(instanced.shortGUID))
                        queue.Enqueue(instanced.shortGUID);
                }
            }
            return needed;
        }

        private static string Leaf(Composite composite)
        {
            string name = (composite?.name ?? "").Replace('\\', '/');
            return name.Substring(name.LastIndexOf('/') + 1);
        }

        /* The composites this one reaches into for something the original level's copy of them doesn't have */
        private static IEnumerable<KeyValuePair<Composite, string>> Reached(Composite composite, Level user, Level original)
        {
            //A value or a link given an instance through a pin its composite didn't ship with
            foreach (FunctionEntity function in composite.functions)
            {
                Composite instanced = user.Commands.GetComposite(function.function);
                Composite shipped = instanced == null ? null : original.Commands.GetComposite(instanced.shortGUID);
                if (shipped == null)
                    continue;
                HashSet<ShortGuid> pins = new HashSet<ShortGuid>(function.parameters.Select(o => o.name));
                pins.UnionWith(function.childLinks.Select(o => o.thisParamID));
                foreach (Entity entity in composite.GetEntities())
                    foreach (EntityConnector link in entity.childLinks)
                        if (link.linkedEntityID == function.shortGUID)
                            pins.Add(link.linkedParamID);
                HashSet<ShortGuid> shippedPins = new HashSet<ShortGuid>(shipped.variables.Select(o => o.name));
                if (instanced.variables.Any(o => pins.Contains(o.name) && !shippedPins.Contains(o.name)))
                    yield return new KeyValuePair<Composite, string>(instanced, Leaf(composite) + " uses a pin added to it");
            }

            //A path to an entity added inside an instanced composite
            List<KeyValuePair<EntityPath, string>> paths = new List<KeyValuePair<EntityPath, string>>();
            foreach (AliasEntity alias in composite.aliases)
                paths.Add(new KeyValuePair<EntityPath, string>(alias.alias, "an alias in " + Leaf(composite)));
            foreach (FunctionEntity function in composite.functions)
            {
                if (function is TriggerSequence sequence)
                    foreach (TriggerSequence.SequenceEntry entry in sequence.sequence)
                        paths.Add(new KeyValuePair<EntityPath, string>(entry.connectedEntity, "a trigger sequence in " + Leaf(composite)));
                else if (function is CAGEAnimation animation)
                    foreach (CAGEAnimation.Connection connection in animation.connections)
                        paths.Add(new KeyValuePair<EntityPath, string>(connection.connectedEntity, "an animation in " + Leaf(composite)));
            }
            foreach (KeyValuePair<EntityPath, string> path in paths)
            {
                Composite current = composite;
                foreach (ShortGuid hop in path.Key?.path ?? new ShortGuid[0])
                {
                    if (hop.IsInvalid)
                        break;
                    Entity entity = current.GetEntityByID(hop);
                    if (entity == null)
                        break;
                    if (current != composite)
                    {
                        Composite shipped = original.Commands.GetComposite(current.shortGUID);
                        if (shipped != null && shipped.GetEntityByID(hop) == null)
                            yield return new KeyValuePair<Composite, string>(current, path.Value + " points at something added to it");
                    }
                    Composite next = entity is FunctionEntity function ? user.Commands.GetComposite(function.function) : null;
                    if (next == null)
                        break;
                    current = next;
                }
            }
        }
    }
}
#endif
