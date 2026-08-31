using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// Works out which entity type declares each of an entity's parameters, so the parameter grid
    /// can group its rows into one category per contributing type: a Door's own parameters sit
    /// under "Door", the ones it inherits under each ancestor's name, in inheritance order down
    /// the grid. Instanced composites group their variables under the composite's own name, and
    /// proxies and aliases group by what they resolve to. Anything no type claims - user-added
    /// parameters, and the name row - falls into the plain "Parameters" group at the bottom.
    ///
    /// The declaring type comes from the shipped vanilla entity table: each type's own (uninherited)
    /// parameter list, walked up the same base-class chain GetAllParameters composes from, with the
    /// most derived type winning when a name appears at several levels.
    /// </summary>
    public static class ParameterGroupProvider
    {
        public const string DefaultGroup = "Parameters";

        //A pure function type's map depends only on the vanilla table, so it is computed once
        private static readonly Dictionary<FunctionType, Dictionary<ShortGuid, string>> _functionCache = new Dictionary<FunctionType, Dictionary<ShortGuid, string>>();

        /* Can a grouped view be derived for this entity? */
        public static bool HasGroups(Entity entity)
        {
            if (entity == null)
                return false;

            switch (entity.variant)
            {
                case EntityVariant.FUNCTION:
                case EntityVariant.PROXY:
                case EntityVariant.ALIAS:
                    return true;
                default:
                    return false;
            }
        }

        /* The group display name for each of the entity's parameters. Null when nothing can be
           derived; parameters absent from the map belong in DefaultGroup. The names carry leading
           tabs - invisible in the grid - so the categories sort in inheritance order rather than
           alphabetically. */
        public static Dictionary<ShortGuid, string> GetGroups(Entity entity, Composite composite, Commands commands)
        {
            if (entity == null || commands?.Utils == null)
                return null;

            bool cacheable = entity is FunctionEntity cacheKey && cacheKey.function.IsFunctionType;
            if (cacheable)
            {
                FunctionType function = ((FunctionEntity)entity).function.AsFunctionType;
                if (_functionCache.TryGetValue(function, out Dictionary<ShortGuid, string> cached))
                    return cached;
            }

            Dictionary<ShortGuid, string> groups;
            try
            {
                GroupBuilder builder = new GroupBuilder(commands);
                Collect(entity, composite, commands, builder, 0);
                groups = builder.Finish();
            }
            catch
            {
                //A hole in the vanilla table shouldn't take the grid down - it just loses grouping
                groups = null;
            }

            if (cacheable)
                _functionCache[((FunctionEntity)entity).function.AsFunctionType] = groups;

            return groups;
        }

        private static void Collect(Entity entity, Composite composite, Commands commands, GroupBuilder builder, int depth)
        {
            if (entity == null || depth > 4)
                return;

            switch (entity.variant)
            {
                case EntityVariant.FUNCTION:
                    FunctionEntity function = (FunctionEntity)entity;
                    if (function.function.IsFunctionType)
                    {
                        builder.AddChain(function.function.AsFunctionType);
                    }
                    else
                    {
                        //An instanced composite: its variables under the composite's own name, then
                        //the interface every instance carries
                        Composite instanced = commands.GetComposite(function.function);
                        if (instanced != null)
                            builder.AddGroup(ShortName(instanced.name), instanced.variables.Select(o => o.name));
                        builder.AddChain(FunctionType.CompositeInterface);
                    }
                    break;

                case EntityVariant.PROXY:
                    (Composite proxiedComposite, Entity proxiedEntity) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveProxy((ProxyEntity)entity));
                    Collect(proxiedEntity, proxiedComposite, commands, builder, depth + 1);
                    builder.AddChain(FunctionType.ProxyInterface);
                    break;

                case EntityVariant.ALIAS:
                    (Composite aliasedComposite, Entity aliasedEntity) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveAlias((AliasEntity)entity, composite));
                    Collect(aliasedEntity, aliasedComposite, commands, builder, depth + 1);
                    break;
            }
        }

        private static string ShortName(string compositeName)
        {
            if (string.IsNullOrEmpty(compositeName))
                return DefaultGroup;

            int cut = compositeName.LastIndexOfAny(new char[] { '\\', '/' });
            return cut == -1 ? compositeName : compositeName.Substring(cut + 1);
        }

        /// <summary>
        /// Accumulates groups in first-seen order, each parameter claimed by the first group that
        /// lists it - which, walked most-derived-first, makes overrides land on the derived type.
        /// </summary>
        private sealed class GroupBuilder
        {
            private readonly Commands _commands;
            private readonly Dictionary<ShortGuid, int> _assignments = new Dictionary<ShortGuid, int>();
            private readonly List<string> _names = new List<string>();

            public GroupBuilder(Commands commands)
            {
                _commands = commands;
            }

            public void AddChain(FunctionType start)
            {
                FunctionType? current = start;
                HashSet<FunctionType> visited = new HashSet<FunctionType>();
                while (current != null && visited.Add(current.Value))
                {
                    AddGroup(current.Value.ToString(), _commands.Utils.GetAllParameters(current.Value).Select(o => o.Item1));
                    current = _commands.Utils.GetInheritedFunction(current.Value);
                }
            }

            public void AddGroup(string name, IEnumerable<ShortGuid> parameters)
            {
                int index = -1;
                foreach (ShortGuid parameter in parameters)
                {
                    if (_assignments.ContainsKey(parameter))
                        continue;

                    if (index == -1)
                    {
                        //The two chains a proxy walks can share ancestors - contributions with the
                        //same name join the existing group rather than duplicating it
                        index = _names.IndexOf(name);
                        if (index == -1)
                        {
                            index = _names.Count;
                            _names.Add(name);
                        }
                    }

                    _assignments[parameter] = index;
                }
            }

            public Dictionary<ShortGuid, string> Finish()
            {
                if (_assignments.Count == 0)
                    return null;

                //The grid sorts category names alphabetically and renders leading tabs as nothing,
                //so tab prefixes - deeper in the chain, fewer tabs - hold the declaration order,
                //and the unprefixed DefaultGroup lands at the bottom
                string[] display = new string[_names.Count];
                for (int i = 0; i < _names.Count; i++)
                    display[i] = new string('\t', _names.Count - i) + _names[i];

                Dictionary<ShortGuid, string> result = new Dictionary<ShortGuid, string>(_assignments.Count);
                foreach (KeyValuePair<ShortGuid, int> assignment in _assignments)
                    result[assignment.Key] = display[assignment.Value];
                return result;
            }
        }
    }
}
