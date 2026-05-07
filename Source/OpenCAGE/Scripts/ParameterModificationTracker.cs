using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAGE
{
    public static class ParameterModificationTracker
    {
        private static CompositeParameterModificationTable _parameterTracker;
        private static EntityAppliedDefaultsTable _defaultsTracker;

        public static Commands LinkedCommands => _commands;
        private static Commands _commands;

        static ParameterModificationTracker()
        {

        }

        /* Get if a parameter has been modified, so we can display it as such in the UI */
        public static bool IsParameterModified(ShortGuid composite, ShortGuid entity, ShortGuid parameter)
        {
            Dictionary<ShortGuid, HashSet<ShortGuid>> composite_entities;
            if (!_parameterTracker.modified_params.TryGetValue(composite, out composite_entities))
                return false;

            HashSet<ShortGuid> entity_parameters;
            if (!composite_entities.TryGetValue(entity, out entity_parameters))
                return false;

            return entity_parameters.Contains(parameter);
        }

        /* Set that a parameter has been modified, so we can display it as such in the UI */
        public static void SetParameterModified(ShortGuid composite, ShortGuid entity, ShortGuid parameter)
        {
            Dictionary<ShortGuid, HashSet<ShortGuid>> composite_entities;
            if (!_parameterTracker.modified_params.TryGetValue(composite, out composite_entities))
            {
                composite_entities = new Dictionary<ShortGuid, HashSet<ShortGuid>>();
                _parameterTracker.modified_params.Add(composite, composite_entities);
            }

            HashSet<ShortGuid> entity_parameters;
            if (!composite_entities.TryGetValue(entity, out entity_parameters))
            {
                entity_parameters = new HashSet<ShortGuid>();
                composite_entities.Add(entity, entity_parameters);
            }

            entity_parameters.Add(parameter);
        }

        /* Get if default parameters have been applied to an entity */
        public static bool IsDefaultsApplied(ShortGuid composite, ShortGuid entity)
        {
            HashSet<ShortGuid> composites;
            if (!_defaultsTracker.applied_defaults.TryGetValue(composite, out composites))
                return false;
            return composites.Contains(entity);
        }

        /* Set if default parameters have been applied to an entity */
        public static void SetDefaultsApplied(ShortGuid composite, ShortGuid entity)
        {
            HashSet<ShortGuid> composites;
            if (!_defaultsTracker.applied_defaults.TryGetValue(composite, out composites))
            {
                composites = new HashSet<ShortGuid>();
                _defaultsTracker.applied_defaults.Add(composite, composites);
            }
            composites.Add(entity);
        }

        //TODO: should really deprecate this LinkCommands on everything and just do it on the level load event
        public static void LinkCommands(Commands commands)
        {
            if (_commands != null)
            {
#if AUTO_POPULATE_PARAMS
                _commands.OnLoadSuccess -= LoadModifications;
                _commands.OnSaveSuccess -= SaveModifications;
#endif
            }

            _commands = commands;
            if (_commands == null) return;

#if AUTO_POPULATE_PARAMS
            _commands.OnLoadSuccess += LoadModifications;
            _commands.OnSaveSuccess += SaveModifications;

            LoadModifications(_commands.Filepath);
#endif
        }

        private static void LoadModifications(string filepath)
        {
            _parameterTracker = (CompositeParameterModificationTable)CustomTable.ReadTable(filepath, CustomTableType.COMPOSITE_PARAMETER_MODIFICATION);
            if (_parameterTracker == null || _parameterTracker.modified_params.Count == 0)
            {
                _parameterTracker = new CompositeParameterModificationTable();
                if (_commands != null)
                {
                    foreach (Composite composite in _commands.Entries)
                    {
                        Dictionary<ShortGuid, HashSet<ShortGuid>> entities = new Dictionary<ShortGuid, HashSet<ShortGuid>>();
                        _parameterTracker.modified_params.Add(composite.shortGUID, entities);
                        foreach (FunctionEntity entity in composite.functions)
                        {
                            entities.Add(entity.shortGUID, PopulateModified(entity));
                        }
                        foreach (ProxyEntity entity in composite.proxies)
                        {
                            entities.Add(entity.shortGUID, PopulateModified(entity));
                        }
                    }
                }
                Debug.Log("Modification Tracker", "Generated info for " + _parameterTracker.modified_params.Count + " composites with parameter modifications!");
            }
            else
            {
                Debug.Log("Modification Tracker", "Loaded info for " + _parameterTracker.modified_params.Count + " composites with parameter modifications!");
            }

            _defaultsTracker = (EntityAppliedDefaultsTable)CustomTable.ReadTable(filepath, CustomTableType.ENTITY_APPLIED_DEFAULTS);
            if (_defaultsTracker == null) _defaultsTracker = new EntityAppliedDefaultsTable();
            Debug.Log("Modification Tracker", "Loaded " + _defaultsTracker.applied_defaults.Count + " composites with defaults applied!");
        }
        private static HashSet<ShortGuid> PopulateModified(Entity entity)
        {
            HashSet<ShortGuid> modified = new HashSet<ShortGuid>();
            foreach (Parameter parameter in entity.parameters)
            {
                modified.Add(parameter.name);
            }
            return modified;
        }

        private static void SaveModifications(string filepath)
        {
            CustomTable.WriteTable(filepath, CustomTableType.COMPOSITE_PARAMETER_MODIFICATION, _parameterTracker);
            Debug.Log("Modification Tracker", "Saved info for " + _parameterTracker.modified_params.Count + " composites with parameter modifications!");

            CustomTable.WriteTable(filepath, CustomTableType.ENTITY_APPLIED_DEFAULTS, _defaultsTracker);
            Debug.Log("Modification Tracker", "Saved " + _defaultsTracker.applied_defaults.Count + " composites with defaults applied!");
        }
    }
}
