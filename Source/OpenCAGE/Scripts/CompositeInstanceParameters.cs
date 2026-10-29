using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;

namespace OpenCAGE
{
    /// <summary>
    /// A composite instance is configured through the variable entities on the composite it points at,
    /// rather than through a function's own parameter list. Those variables all carry pin variants, so
    /// the ordinary defaults pass (which asks for PARAMETER/STATE_PARAMETER) never reaches them and an
    /// instance would show only the generic interface parameters.
    /// </summary>
    public static class CompositeInstanceParameters
    {
        /// <summary>
        /// Give a composite instance a parameter for each input pin on the composite it points at, filled
        /// with that composite's own default. Only adds what isn't already there, so it is safe to call on
        /// every inspection. Returns how many were added.
        /// </summary>
        public static int Ensure(Entity entity, Commands commands)
        {
            if (entity == null || commands?.Utils == null)
                return 0;

            FunctionEntity instance = entity as FunctionEntity;
            if (instance == null && entity.variant == EntityVariant.PROXY)
                instance = commands.Utils.GetResolvedTarget(commands.Utils.ResolveProxy((ProxyEntity)entity)).Item2 as FunctionEntity;
            if (instance == null || instance.function.IsFunctionType)
                return 0;

            Composite pointed = commands.GetComposite(instance.function);
            if (pointed == null)
                return 0;

            int added = 0;
            foreach (VariableEntity variable in pointed.variables)
            {
                if (entity.GetParameter(variable.name) != null)
                    continue;

                //a variable with no pin info is already treated as a PARAMETER, so the defaults pass covers it
                CompositePinInfoTable.PinInfo pin = commands.Utils.GetPinInfo(pointed, variable);
                if (pin == null || commands.Utils.PinTypeToParameterVariant(pin.PinTypeGUID) != ParameterVariant.INPUT_PIN)
                    continue;

                /* Reference-style pins (OBJECT, ZONE, ZONE_LINK) are fed by links and hold no value of
                 * their own: retail never stores one, and there is no default of the right type to build,
                 * so building one anyway would quietly change the parameter's type. Leave them to the
                 * flowgraph rather than writing a wrongly typed value into the level. */
                ParameterData value = commands.Utils.CreateDefaultParameterData(variable, pointed, variable.name);
                if (value == null || value.dataType != variable.type)
                    continue;

                /* An enum has no natural empty value, so a pin the composite gives no default for comes
                 * back as the "nothing chosen" sentinel. Writing that into the level would invent a value
                 * retail never stores, so leave it for the flowgraph rather than guess an index. */
                if (value is cEnum chosen && chosen.enumIndex < 0)
                    continue;

                entity.AddParameter(variable.name, value, ParameterVariant.INPUT_PIN, false);
                added++;
            }
            return added;
        }
    }
}
