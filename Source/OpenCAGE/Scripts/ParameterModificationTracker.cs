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
            if (_parameterTracker == null)
                return false;

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
            if (_parameterTracker == null)
                return;

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

        /* Clear a parameter's modified state (e.g. when it's reset back to its default value) */
        public static void ClearParameterModified(ShortGuid composite, ShortGuid entity, ShortGuid parameter)
        {
            if (_parameterTracker == null)
                return;

            if (!_parameterTracker.modified_params.TryGetValue(composite, out Dictionary<ShortGuid, HashSet<ShortGuid>> composite_entities))
                return;
            if (!composite_entities.TryGetValue(entity, out HashSet<ShortGuid> entity_parameters))
                return;
            entity_parameters.Remove(parameter);
        }

        /* Carry an entity's modification state over to a copy of it (clones get a new GUID, so the
           tracker would otherwise treat every parameter on the copy as unmodified) */
        public static void CopyEntityModifications(ShortGuid sourceComposite, ShortGuid sourceEntity, ShortGuid targetComposite, ShortGuid targetEntity)
        {
            if (_parameterTracker == null)
                return;

            if (_parameterTracker.modified_params.TryGetValue(sourceComposite, out Dictionary<ShortGuid, HashSet<ShortGuid>> sourceEntities)
                && sourceEntities.TryGetValue(sourceEntity, out HashSet<ShortGuid> sourceParameters))
            {
                foreach (ShortGuid parameter in sourceParameters)
                    SetParameterModified(targetComposite, targetEntity, parameter);
            }

            if (IsDefaultsApplied(sourceComposite, sourceEntity))
                SetDefaultsApplied(targetComposite, targetEntity);
        }

        /* One composite's rows from both tables, copied into tables of the caller's own: what a composite
           archive carries beside the script, so a ported entity keeps its modified-parameter marks and
           is not handed a fresh set of defaults the first time it is inspected. */
        public static void ExportCompositeRows(ShortGuid composite, CompositeParameterModificationTable modifications, EntityAppliedDefaultsTable defaults)
        {
            if (_parameterTracker != null && modifications != null
                && _parameterTracker.modified_params.TryGetValue(composite, out Dictionary<ShortGuid, HashSet<ShortGuid>> entities))
            {
                modifications.modified_params[composite] = CopyRows(entities);
            }
            if (_defaultsTracker != null && defaults != null
                && _defaultsTracker.applied_defaults.TryGetValue(composite, out HashSet<ShortGuid> applied))
            {
                defaults.applied_defaults[composite] = new HashSet<ShortGuid>(applied);
            }
        }

        /* The reverse: a composite's rows from tables read out of an archive replace whatever this level
           held for that composite ID (a copy being overwritten, or nothing). */
        public static void ImportCompositeRows(ShortGuid composite, CompositeParameterModificationTable modifications, EntityAppliedDefaultsTable defaults)
        {
            CopyCompositeRows(composite, modifications, defaults, _parameterTracker, _defaultsTracker);
        }

        /// <summary>
        /// One composite's rows from one pair of tables into another, replacing what the target held for
        /// that composite ID. The tables can be anyone's - the level open in the editor, or ones read
        /// out of a level on disk that is not the loaded one.
        /// </summary>
        public static void CopyCompositeRows(ShortGuid composite,
            CompositeParameterModificationTable fromModifications, EntityAppliedDefaultsTable fromDefaults,
            CompositeParameterModificationTable toModifications, EntityAppliedDefaultsTable toDefaults)
        {
            if (toModifications != null && fromModifications != null
                && fromModifications.modified_params.TryGetValue(composite, out Dictionary<ShortGuid, HashSet<ShortGuid>> entities))
            {
                toModifications.modified_params[composite] = CopyRows(entities);
            }
            if (toDefaults != null && fromDefaults != null
                && fromDefaults.applied_defaults.TryGetValue(composite, out HashSet<ShortGuid> applied))
            {
                toDefaults.applied_defaults[composite] = new HashSet<ShortGuid>(applied);
            }
        }

        private static Dictionary<ShortGuid, HashSet<ShortGuid>> CopyRows(Dictionary<ShortGuid, HashSet<ShortGuid>> entities)
        {
            Dictionary<ShortGuid, HashSet<ShortGuid>> copy = new Dictionary<ShortGuid, HashSet<ShortGuid>>();
            foreach (KeyValuePair<ShortGuid, HashSet<ShortGuid>> entity in entities)
                copy[entity.Key] = new HashSet<ShortGuid>(entity.Value);
            return copy;
        }

        /* Get if default parameters have been applied to an entity */
        public static bool IsDefaultsApplied(ShortGuid composite, ShortGuid entity)
        {
            if (_defaultsTracker == null)
                return false;

            HashSet<ShortGuid> composites;
            if (!_defaultsTracker.applied_defaults.TryGetValue(composite, out composites))
                return false;
            return composites.Contains(entity);
        }

        /* Set if default parameters have been applied to an entity */
        public static void SetDefaultsApplied(ShortGuid composite, ShortGuid entity)
        {
            if (_defaultsTracker == null)
                return;

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

        /// <summary>
        /// Ids the last generated table found used twice - a composite in the script twice, or a proxy
        /// under a function's id. The script loads regardless, but links through such an entity resolve
        /// to whichever the lookup finds first, so the user is told.
        /// </summary>
        public static readonly List<string> IntegrityWarnings = new List<string>();

        private static void LoadModifications(string filepath)
        {
            IntegrityWarnings.Clear();
            _parameterTracker = (CompositeParameterModificationTable)CustomTable.ReadTable(filepath, CustomTableType.COMPOSITE_PARAMETER_MODIFICATION);
            if (_parameterTracker == null || _parameterTracker.modified_params.Count == 0)
            {
                _parameterTracker = GenerateModificationTable(_commands, IntegrityWarnings);
                Debug.Log("Modification Tracker", "Generated info for " + _parameterTracker.modified_params.Count + " composites with parameter modifications!");
                if (IntegrityWarnings.Count != 0)
                {
                    //On the loader thread; the message waits for the window
                    string text = "This level's script uses the same id for more than one thing:\n\n" + string.Join("\n", IntegrityWarnings.Take(10))
                        + (IntegrityWarnings.Count > 10 ? "\n...and " + (IntegrityWarnings.Count - 10) + " more" : "")
                        + "\n\nIt loads, but links through such an entity resolve to whichever one is found first. Restore a backup of the level, or rebuild the composite.";
                    try { Singleton.Editor?.BeginInvoke(new Action(() => System.Windows.Forms.MessageBox.Show(text, "Duplicate ids in script", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning))); }
                    catch { }
                }
            }
            else
            {
                Debug.Log("Modification Tracker", "Loaded info for " + _parameterTracker.modified_params.Count + " composites with parameter modifications!");
            }

            _defaultsTracker = (EntityAppliedDefaultsTable)CustomTable.ReadTable(filepath, CustomTableType.ENTITY_APPLIED_DEFAULTS);
            if (_defaultsTracker == null) _defaultsTracker = new EntityAppliedDefaultsTable();
            Debug.Log("Modification Tracker", "Loaded " + _defaultsTracker.applied_defaults.Count + " composites with defaults applied!");
        }
        /// <summary>
        /// The table a level gets when it has none: every parameter an entity carries counts as modified,
        /// since nothing says otherwise. Used for the loaded level on first load, and for a level on disk
        /// that ported composites are being written into - a partial table written over nothing would
        /// leave every other composite looking untouched the first time that level is opened.
        /// </summary>
        public static CompositeParameterModificationTable GenerateModificationTable(Commands commands, List<string> warnings = null)
        {
            CompositeParameterModificationTable table = new CompositeParameterModificationTable();
            if (commands == null)
                return table;
            foreach (Composite composite in commands.Entries)
            {
                if (composite == null)
                    continue;

                /* Commands.Entries is a list and functions and proxies are separate dictionaries, so a
                   script file can carry one composite id twice, or a proxy under a function's id, and
                   still load (the PAK reader checks neither; old tooling minted such ids). Neither may
                   throw here (crash 393, on the loader thread): a row is keyed by id, so to the lookup
                   the two are one entity - it gets the union of their parameters. */
                Dictionary<ShortGuid, HashSet<ShortGuid>> entities;
                if (!table.modified_params.TryGetValue(composite.shortGUID, out entities))
                {
                    entities = new Dictionary<ShortGuid, HashSet<ShortGuid>>();
                    table.modified_params.Add(composite.shortGUID, entities);
                }
                else
                {
                    string warning = "Composite " + composite.name + " (" + composite.shortGUID.ToByteString() + ") is in the script more than once";
                    Debug.Log("Modification Tracker", warning);
                    warnings?.Add(warning);
                }
                foreach (FunctionEntity entity in composite.functions)
                    AddGeneratedRow(composite, entities, entity, warnings);
                foreach (ProxyEntity entity in composite.proxies)
                    AddGeneratedRow(composite, entities, entity, warnings);
            }
            return table;
        }

        private static void AddGeneratedRow(Composite composite, Dictionary<ShortGuid, HashSet<ShortGuid>> entities, Entity entity, List<string> warnings)
        {
            HashSet<ShortGuid> modified;
            if (entities.TryGetValue(entity.shortGUID, out modified))
            {
                string warning = "Entity id " + entity.shortGUID.ToByteString() + " in " + composite.name + " is both a function and a proxy";
                Debug.Log("Modification Tracker", warning);
                warnings?.Add(warning);
                modified.UnionWith(PopulateModified(entity));
            }
            else
            {
                entities.Add(entity.shortGUID, PopulateModified(entity));
            }
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
