using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;

namespace OpenCAGE
{
    /// <summary>
    /// Entity parameters that are derived from resource/object bindings and should not be edited in the UI.
    /// Values are written internally (instancing / Commands save).
    /// </summary>
    internal static class EntityParameterVisibility
    {
        public static bool IsHiddenFromEditor(Entity entity, ShortGuid paramName)
        {
            if (entity is FunctionEntity function)
            {
                if (function.function == FunctionType.PhysicsSystem && paramName == ShortGuids.system_index)
                    return true;

                if (function.function == FunctionType.EnvironmentMap &&
                    (paramName == ShortGuids.Texture_Index || paramName == ShortGuids.environmentmap_index))
                    return true;
            }
            return false;
        }
    }
}
