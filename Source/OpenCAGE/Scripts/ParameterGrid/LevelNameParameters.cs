using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// Parameters that name the level a SwitchLevel loads, which the parameter grid offers the game's
    /// levels for (issue 700), and how the game's scripts spell a level.
    ///
    /// SwitchLevel's own level_name is never set in the shipped levels. It is linked to the LevelName
    /// variable of Archetypes\Script\Mission\SwitchLevel, and the level is set on each instance of that
    /// composite - or one level further out, on the Lift_logic, Lift_logic_Large and Suicide_Barriers
    /// composites that instance it and pass their own LevelName down. So a parameter counts when it is
    /// level_name on a SwitchLevel, or a composite variable whose links carry it to one; proxies and
    /// aliases count when what they point at does. AmbientScriptMemory's LevelName (the current level,
    /// spelled without "Production\") goes nowhere near a SwitchLevel, and stays plain text.
    /// </summary>
    public static class LevelNameParameters
    {
        private static readonly ShortGuid _levelName = ShortGuidUtils.Generate("level_name");

        //Nested composites pass the value down a few levels at most: this only stops a cycle of aliases
        private const int MaxDepth = 16;

        /* Does this parameter of this entity end up as the level a SwitchLevel loads? */
        public static bool IsLevelName(Entity entity, Composite composite, ShortGuid parameter, Commands commands)
        {
            if (entity == null || composite == null || commands?.Utils == null)
                return false;
            return TakesLevelName(entity, composite, parameter, commands, new HashSet<(ShortGuid, ShortGuid)>(), 0);
        }

        private static bool TakesLevelName(Entity entity, Composite composite, ShortGuid parameter, Commands commands, HashSet<(ShortGuid, ShortGuid)> visited, int depth)
        {
            if (entity == null || composite == null || depth > MaxDepth)
                return false;

            switch (entity.variant)
            {
                case EntityVariant.FUNCTION:
                    return FunctionTakesLevelName(((FunctionEntity)entity).function, parameter, commands, visited, depth);

                case EntityVariant.VARIABLE:
                    //A variable's value row is its default, which is what reaches whatever it is linked to
                    VariableEntity variable = (VariableEntity)entity;
                    return parameter == variable.name && VariableFeedsLevelName(composite, variable.name, commands, visited, depth + 1);

                case EntityVariant.PROXY:
                    ProxyEntity proxy = (ProxyEntity)entity;
                    (Composite proxiedComposite, Entity proxiedEntity) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveProxy(proxy));
                    if (proxiedEntity != null)
                        return TakesLevelName(proxiedEntity, proxiedComposite, parameter, commands, visited, depth + 1);

                    //A dead proxy still knows what it pointed at
                    return FunctionTakesLevelName(proxy.function, parameter, commands, visited, depth);

                case EntityVariant.ALIAS:
                    (Composite aliasedComposite, Entity aliasedEntity) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveAlias((AliasEntity)entity, composite));
                    return TakesLevelName(aliasedEntity, aliasedComposite, parameter, commands, visited, depth + 1);
            }
            return false;
        }

        /* A SwitchLevel's level_name, or a parameter of an instanced composite that it passes on to one */
        private static bool FunctionTakesLevelName(ShortGuid function, ShortGuid parameter, Commands commands, HashSet<(ShortGuid, ShortGuid)> visited, int depth)
        {
            if (function.IsFunctionType)
                return function == FunctionType.SwitchLevel && parameter == _levelName;
            Composite instanced = commands.GetComposite(function);
            return instanced != null && VariableFeedsLevelName(instanced, parameter, commands, visited, depth + 1);
        }

        /* Is the composite's variable of this name linked to something that takes a level name? */
        private static bool VariableFeedsLevelName(Composite composite, ShortGuid name, Commands commands, HashSet<(ShortGuid, ShortGuid)> visited, int depth)
        {
            if (!visited.Add((composite.shortGUID, name)))
                return false;

            VariableEntity variable = composite.variables.FirstOrDefault(o => o.name == name);
            if (variable == null)
                return false;

            //A link is kept on one end only - a value pin's usually on the entity that reads it - so look at both
            foreach (EntityConnector link in variable.childLinks)
            {
                if (link.thisParamID == name && TakesLevelName(composite.GetEntityByID(link.linkedEntityID), composite, link.linkedParamID, commands, visited, depth))
                    return true;
            }
            foreach (Entity entity in composite.GetEntities())
            {
                foreach (EntityConnector link in entity.childLinks)
                {
                    if (link.linkedEntityID == variable.shortGUID && link.linkedParamID == name && TakesLevelName(entity, composite, link.thisParamID, commands, visited, depth))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A level as a SwitchLevel names it, from the way Level.GetLevels lists what is on disk: PRODUCTION/SCI_HUB
        /// becomes Production\SCI_HUB - "Production\" and the folder path, the way MAIN.PKG lists a level. Case and
        /// slashes don't look to matter to the game (retail ships "Production\TECH_Hub", "Production\Tech_Hub" and
        /// "production/ENG_ReactorCore" side by side, and the launcher boots "PRODUCTION/SCI_HUB"), so nothing here
        /// keeps a table of spellings: a level added on disk is offered the same way as a shipped one.
        /// </summary>
        public static string ToScriptName(string level)
        {
            if (string.IsNullOrWhiteSpace(level))
                return "";

            string name = level.Trim().Replace('/', '\\').Trim('\\');
            if (name.StartsWith("PRODUCTION\\", StringComparison.OrdinalIgnoreCase))
                name = "Production" + name.Substring("PRODUCTION".Length);
            return name;
        }

        /* Do two names mean the same level, however each is spelled? */
        public static bool IsSameLevel(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                return false;
            return Normalise(a) == Normalise(b);
        }

        private static string Normalise(string level)
        {
            return level.Trim().Replace('\\', '/').Trim('/').ToUpperInvariant();
        }
    }
}
