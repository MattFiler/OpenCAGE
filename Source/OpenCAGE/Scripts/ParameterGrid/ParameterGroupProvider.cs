using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System.Collections.Generic;

namespace OpenCAGE
{
    /// <summary>
    /// Provides display group names for entity parameters in the inspector's parameter grid.
    /// No grouping data exists yet - register per-function-type mappings here when the data is available,
    /// and the grid will automatically switch to a categorised view for those types.
    /// </summary>
    public static class ParameterGroupProvider
    {
        public const string DefaultGroup = "Parameters";

        //Function type -> (parameter name -> group name)
        private static readonly Dictionary<FunctionType, Dictionary<string, string>> _groups = new Dictionary<FunctionType, Dictionary<string, string>>();

        /* Register grouping data for a function type (parameter name -> group name) */
        public static void RegisterGroups(FunctionType function, Dictionary<string, string> parameterGroups)
        {
            _groups[function] = parameterGroups;
        }

        /* Get the group for a parameter on an entity (null if no grouping data exists) */
        public static string GetGroup(Entity entity, string parameterName)
        {
            FunctionType? function = TryGetFunctionType(entity);
            if (function == null)
                return null;
            if (!_groups.TryGetValue(function.Value, out Dictionary<string, string> groups))
                return null;
            if (!groups.TryGetValue(parameterName, out string group))
                return null;
            return group;
        }

        /* Does any grouping data exist for this entity's type? */
        public static bool HasGroups(Entity entity)
        {
            FunctionType? function = TryGetFunctionType(entity);
            return function != null && _groups.ContainsKey(function.Value);
        }

        private static FunctionType? TryGetFunctionType(Entity entity)
        {
            if (entity is FunctionEntity functionEntity && functionEntity.function.IsFunctionType)
                return functionEntity.function.AsFunctionType;
            return null;
        }
    }
}
