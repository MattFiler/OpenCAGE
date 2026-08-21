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

                //Written by the alphalight baker from the entity's mesh/probe placement
                if (function.function == FunctionType.ModelReference &&
                    (paramName == ShortGuids.alpha_light_offset_x || paramName == ShortGuids.alpha_light_offset_y
                        || paramName == ShortGuids.alpha_light_scale_x || paramName == ShortGuids.alpha_light_scale_y
                        || paramName == ShortGuids.alpha_light_average_normal))
                    return true;
            }
            return false;
        }
    }
}
