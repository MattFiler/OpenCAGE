using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CommandsEditor.DockPanels;
using OpenCAGE;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.Xml;

namespace CommandsEditor
{
    //Wrappers around CathodeLib utils, and some utils for formatting strings
    public class EditorUtils
    {
        private LevelContent Content;
        public EditorUtils(LevelContent content)
        {
            Content = content;
        }

        /* Some additional composite info for rich display in editor */
        public enum CompositeType
        {
            IS_GENERIC_COMPOSITE,
            IS_ROOT,
            IS_PAUSE_MENU,
            IS_GLOBAL,
            IS_DISPLAY_MODEL,
        }
        public CompositeType GetCompositeType(Composite composite)
        {
            return GetCompositeType(composite.name);
        }
        public CompositeType GetCompositeType(string composite)
        {
            string c = composite.Replace('/', '\\');
            if (Content.Level.Commands.EntryPoints[0].name.Replace('/', '\\') == c) return CompositeType.IS_ROOT;
            if (Content.Level.Commands.EntryPoints[1].name.Replace('/', '\\') == c) return CompositeType.IS_PAUSE_MENU;
            if (Content.Level.Commands.EntryPoints[2].name.Replace('/', '\\') == c) return CompositeType.IS_GLOBAL;
            if (c.Length > ("DisplayModel:").Length && c.Substring(0, ("DisplayModel:").Length) == "DisplayModel:") return CompositeType.IS_DISPLAY_MODEL;
            return CompositeType.IS_GENERIC_COMPOSITE;
        }

        /* Generate all composite instance information for Commands */
        private Dictionary<ShortGuid, List<Tuple<ShortGuid, ShortGuid[]>>> _compositeInstancePaths = new Dictionary<ShortGuid, List<Tuple<ShortGuid, ShortGuid[]>>>();
        private CancellationTokenSource _prevTaskToken = null;
        public void GenerateCompositeInstances(Commands commands, bool runOnThread = true)
        {
            if (_prevTaskToken != null)
                _prevTaskToken.Cancel();

            _compositeInstancePaths.Clear();

            _prevTaskToken = new CancellationTokenSource();

            //if (runOnThread)
            //    Task.Run(() => Content.EditorUtils.GenerateCompositeInstancesRecursive(commands, commands.EntryPoints[0], new List<ShortGuid>(), _prevTaskToken.Token), _prevTaskToken.Token);
            //else
                Content.EditorUtils.GenerateCompositeInstancesRecursive(commands, commands.EntryPoints[0], new List<ShortGuid>(), _prevTaskToken.Token);
        }
        private void GenerateCompositeInstancesRecursive(Commands commands, Composite composite, List<ShortGuid> hierarchy, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            if (!_compositeInstancePaths.ContainsKey(composite.shortGUID))
                _compositeInstancePaths.Add(composite.shortGUID, new List<Tuple<ShortGuid, ShortGuid[]>>());

            _compositeInstancePaths[composite.shortGUID].Add(new Tuple<ShortGuid, ShortGuid[]>(hierarchy.GenerateCompositeInstanceID(false), hierarchy.ToArray()));

            foreach (var function in composite.functions_dictionary.Values)
            {
                if (function.function.IsFunctionType) continue;

                if (ct.IsCancellationRequested) break;

                List<ShortGuid> newHierarchy = new List<ShortGuid>(hierarchy.ConvertAll(x => x));
                newHierarchy.Add(function.shortGUID);

                Composite newComposite = commands.GetComposite(function.function);
                if (newComposite != null) GenerateCompositeInstancesRecursive(commands, newComposite, newHierarchy, ct);
            }
        }

        /* Get all possible instance IDs for a given composite */
        public ShortGuid[] GetInstanceIDsForComposite(Composite composite)
        {
            if (!_compositeInstancePaths.ContainsKey(composite.shortGUID))
                return new ShortGuid[0];

            List<Tuple<ShortGuid, ShortGuid[]>> hierarchies = _compositeInstancePaths[composite.shortGUID];
            ShortGuid[] instanceIDs = new ShortGuid[hierarchies.Count];
            for (int i = 0; i < hierarchies.Count; i++)
                instanceIDs[i] = hierarchies[i].Item1;
            return instanceIDs;
        }

        /* Get all possible instance IDs for a given composite */
        public EntityPath[] GetHierarchiesForComposite(Composite composite)
        {
            if (!_compositeInstancePaths.ContainsKey(composite.shortGUID))
                return new EntityPath[0];

            List<Tuple<ShortGuid, ShortGuid[]>> hierarchies = _compositeInstancePaths[composite.shortGUID];
            EntityPath[] paths = new EntityPath[hierarchies.Count];
            for (int i = 0; i < hierarchies.Count; i++)
                paths[i] = new EntityPath(hierarchies[i].Item2);
            return paths;
        }

        /* Get all possible hierarchies for a given entity */
        public List<EntityPath> GetHierarchiesForEntity(Composite composite, Entity entity)
        {
            List<EntityPath> formattedHierarchies = new List<EntityPath>();
            if (_compositeInstancePaths.ContainsKey(composite.shortGUID))
            {
                List<Tuple<ShortGuid, ShortGuid[]>> hierarchies = _compositeInstancePaths[composite.shortGUID];
                for (int i = 0; i < hierarchies.Count; i++)
                {
                    //TODO: reduce the need for this fiddling
                    List<ShortGuid> hierarchy = hierarchies[i].Item2.ToList();
                    if (hierarchy.Count != 0 && hierarchy[hierarchy.Count - 1] == ShortGuid.Invalid)
                        hierarchy.RemoveAt(hierarchy.Count - 1); 
                    hierarchy.Add(entity.shortGUID);
                    formattedHierarchies.Add(new EntityPath(hierarchy.ToArray()));
                }
            }
            return formattedHierarchies;
        }

        /* Get a composite (& instance path) from a given composite instance ID */
        public (Composite, EntityPath) GetCompositeFromInstanceID(Commands commands, ShortGuid instanceID)
        {
            if (instanceID == ShortGuid.InstanceGuid)
                return (commands.EntryPoints[0], new EntityPath());

            (Composite compositeResult, EntityPath entityPathResult) = (null, null);
            object lockObject = new object();
            bool found = false;

            ParallelOptions parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            Parallel.ForEach(_compositeInstancePaths, parallelOptions, (compositeInstancePaths, state) =>
            {
                if (found) return;

                foreach (Tuple<ShortGuid, ShortGuid[]> path in compositeInstancePaths.Value)
                {
                    if (path.Item2.Length == 0 || (path.Item2.Length == 1 && path.Item2[0] == ShortGuid.Invalid))
                        continue;

                    if (path.Item1 == instanceID)
                    {
                        lock (lockObject)
                        {
                            if (!found)
                            {
                                compositeResult = commands.GetComposite(compositeInstancePaths.Key);
                                entityPathResult = new EntityPath(path.Item2);
                                found = true;

                                cancellationTokenSource.Cancel();
                                state.Stop();
                            }
                        }
                    }
                }
            });
            return (compositeResult, entityPathResult);
        }

        [Obsolete("This function is safe to use but not performant. It's intended for test code only.")]
        public (Composite, EntityPath, Entity) GetZoneFromInstanceID(Commands commands, ShortGuid instanceID)
        {
            if (instanceID == new ShortGuid("01-00-00-00"))
            {
                //global zone
                return (null, new EntityPath(new ShortGuid[1] { new ShortGuid("01-00-00-00") }), null);
            }
            if (instanceID == new ShortGuid("00-00-00-00"))
            {
                //global zone
                return (null, new EntityPath(new ShortGuid[1] { new ShortGuid("00-00-00-00") }), null);
            }

            for (int i = 0; i < commands.Entries.Count; i++)
            {
                foreach (var function in commands.Entries[i].functions_dictionary.Values)
                {
                    if (function.function != FunctionType.Zone)
                        continue;

                    List<EntityPath> zonePaths = GetHierarchiesForEntity(commands.Entries[i], function);
                    for (int p = 0; p < zonePaths.Count; p++)
                    {
                        if (zonePaths[p].GenerateZoneID() == instanceID)
                        {
                            return (commands.Entries[i], zonePaths[p], function);
                        }
                    }
                }
            }
            return (null, null, null);
        }

        /* Get the hierarchy for a commands entity reference (used to link legacy resource/mvr stuff) */
        public EntityPath GetHierarchyFromHandle(EntityHandle reference)
        {
            EntityPath toReturn = null;
            object lockObj = new object(); 

            Parallel.ForEach(_compositeInstancePaths, (pair, state) =>
            {
                foreach (var instance in pair.Value)
                {
                    if (instance.Item1 == reference.composite_instance_id)
                    {
                        lock (lockObj)
                        {
                            if (toReturn == null)
                            {
                                toReturn = new EntityPath(instance.Item2);
                                toReturn.AddNextStep(reference.entity_id);
                            }
                        }

                        state.Stop(); 
                        break;
                    }
                }

                if (toReturn != null)
                    state.Stop(); 
            });

            return toReturn;
        }


        /* Utility: generate nice entity name to display in UI */
        public string GenerateEntityName(Entity entity, Composite composite, bool regenCache = false)
        {
            if (Content.Level.Commands == null)
                return entity.shortGUID.ToByteString();

            if (hasFinishedCachingEntityNames && regenCache)
            {
                if (!cachedEntityName.ContainsKey(composite.shortGUID)) cachedEntityName.Add(composite.shortGUID, new Dictionary<ShortGuid, string>());

                if (cachedEntityName[composite.shortGUID].ContainsKey(entity.shortGUID)) cachedEntityName[composite.shortGUID].Remove(entity.shortGUID);
                cachedEntityName[composite.shortGUID].Add(entity.shortGUID, GenerateEntityNameInternal(entity, composite));
            }

            if (!cachedEntityName.ContainsKey(composite.shortGUID))
                cachedEntityName.Add(composite.shortGUID, new Dictionary<ShortGuid, string>());

            if (hasFinishedCachingEntityNames && cachedEntityName[composite.shortGUID].TryGetValue(entity.shortGUID, out string name))
                return name;

            return GenerateEntityNameInternal(entity, composite);
        }
        public string GenerateEntityNameWithoutID(Entity entity, Composite composite, bool regenCache = false)
        {
            return GenerateEntityName(entity, composite, regenCache).Substring(14);
        }
        private string GenerateEntityNameInternal(Entity entity, Composite composite)
        {
            string desc = "";
            switch (entity.variant)
            {
                case EntityVariant.VARIABLE:
                    desc = "[" + ((VariableEntity)entity).type.ToUIString() + "] " + ShortGuidUtils.FindString(((VariableEntity)entity).name);
                    break;
                case EntityVariant.FUNCTION:
                    Composite funcComposite = Content.Level.Commands.GetComposite(((FunctionEntity)entity).function);
                    if (funcComposite != null)
                        desc = Content.Level.Commands.Utils.GetEntityName(composite.shortGUID, entity.shortGUID) + " (" + funcComposite.name + ")";
                    else
                        desc = Content.Level.Commands.Utils.GetEntityName(composite.shortGUID, entity.shortGUID) + " (" + ((FunctionType)((FunctionEntity)entity).function.AsUInt32).ToString() + ")";
                    break;
                case EntityVariant.ALIAS:
                    desc = "[ALIAS] " + Content.Level.Commands.Utils.GetResolvedAsString(Content.Level.Commands.Utils.ResolveAlias((AliasEntity)entity, composite), SettingsManager.GetBool(Singleton.Settings.ShowShortGuids));
                    break;
                case EntityVariant.PROXY:
                    desc = "[PROXY] " + Content.Level.Commands.Utils.GetEntityName(composite.shortGUID, entity.shortGUID) + " (" + Content.Level.Commands.Utils.GetResolvedAsString(Content.Level.Commands.Utils.ResolveProxy((ProxyEntity)entity), SettingsManager.GetBool(Singleton.Settings.ShowShortGuids)) + ")";
                    break;
            }
            bool showID = SettingsManager.GetBool(Singleton.Settings.ShowShortGuids);
            return (showID ? "[" + entity.shortGUID.ToByteString() + "] " : "") + desc;
        }

        /* Generate a cache of entity names */
        private bool hasFinishedCachingEntityNames = false;
        private Dictionary<ShortGuid, Dictionary<ShortGuid, string>> cachedEntityName = new Dictionary<ShortGuid, Dictionary<ShortGuid, string>>();
        public void GenerateEntityNameCache(CommandsEditor mainInst)
        {
            if (Content.Level.Commands == null) return;
            hasFinishedCachingEntityNames = false;
            mainInst?.EnableButtons(false, "Generating caches...");
            cachedEntityName.Clear();
            for (int i = 0; i < Content.Level.Commands.Entries.Count; i++)
            {
                Composite comp = Content.Level.Commands.Entries[i];
                if (!cachedEntityName.ContainsKey(comp.shortGUID))
                    cachedEntityName.Add(comp.shortGUID, new Dictionary<ShortGuid, string>());
                List<Entity> ents = comp.GetEntities();
                for (int x = 0; x < ents.Count; x++)
                    if (!cachedEntityName[comp.shortGUID].ContainsKey(ents[x].shortGUID))
                        cachedEntityName[comp.shortGUID].Add(ents[x].shortGUID, GenerateEntityNameInternal(ents[x], comp));
            }
            mainInst?.EnableButtons(true, "");
            hasFinishedCachingEntityNames = true;
        }

        /* Utility: generate a list of suggested parameters for an entity */
        public List<string> GenerateParameterListAsString(Entity entity, Composite composite)
        {
            List<string> items = new List<string>();
            if (entity == null) return items;
            List<(ShortGuid, ParameterVariant, DataType)> parameters = Content.Level.Commands.Utils.GetAllParameters(entity, composite);
            foreach ((ShortGuid, ParameterVariant, DataType) parameter in parameters)
            {
                items.Add(parameter.Item1.ToString());
                if (parameter.Item2 == ParameterVariant.METHOD_PIN)
                {
                    ShortGuid relay = Content.Level.Commands.Utils.GetRelay(parameter.Item1);
                    if (relay != ShortGuid.Invalid)
                        items.Add(relay.ToString());
                }
            }
            items.Sort();
            return items;
        }
        public List<ListViewItem> GenerateParameterListAsListViewItem(Entity entity, Composite composite)
        {
            List<ListViewItem> items = new List<ListViewItem>();
            if (entity == null) return items;
            List<(ShortGuid, ParameterVariant, DataType)> parameters = Content.Level.Commands.Utils.GetAllParameters(entity, composite);
            foreach ((ShortGuid, ParameterVariant,DataType) parameter in parameters)
            {
                items.Add(ParameterDefinitionToListViewItem(parameter.Item1, parameter.Item3, parameter.Item2));
            }
            return items;
        }
        private ListViewItem ParameterDefinitionToListViewItem(ShortGuid name, DataType datatype = DataType.FLOAT, ParameterVariant usage = ParameterVariant.PARAMETER)
        {
            ListViewItem item = new ListViewItem(name.ToString());
            item.SubItems.Add(datatype.ToString());
            item.SubItems.Add(usage.ToString());
            item.Tag = new ParameterListViewItemTag() { ShortGUID = name, Usage = usage };
            return item;
        }
        public struct ParameterListViewItemTag
        {
            public ParameterVariant Usage;
            public ShortGuid ShortGUID;
        }

        /* Utility: force a string to be numeric */
        public static string ForceStringNumeric(string str, bool allowDots = false)
        {
            string editedText = "";
            bool hasIncludedDot = false;
            bool hasIncludedMinus = false;
            for (int i = 0; i < str.Length; i++)
            {
                if (Char.IsNumber(str[i]) || (str[i] == '.' && allowDots) || (str[i] == '-'))
                {
                    if (str[i] == '-' && hasIncludedMinus) continue;
                    if (str[i] == '-' && i != 0) continue;
                    if (str[i] == '-') hasIncludedMinus = true;
                    if (str[i] == '.' && hasIncludedDot) continue;
                    if (str[i] == '.') hasIncludedDot = true;
                    editedText += str[i];
                }
            }
            if (editedText == "") editedText = "0";
            if (editedText == "-") editedText = "-0";
            if (editedText == ".") editedText = "0";
            return editedText;
        }

        /* Populates a combobox with available levels and selects the appropriate one - you should update OPT_LoadToMap on selected change */
        public static void PopulateLevelDropdown(ComboBox dropdown)
        {
            string toSelect = Singleton.Editor?.CommandsDisplay?.Content?.Level?.Name;
            if (toSelect == null) toSelect = SettingsManager.GetString(Singleton.Settings.LastSelectedLevel);

            dropdown.BeginUpdate();
            dropdown.Items.Clear();
            dropdown.Items.AddRange(Level.GetLevels(Singleton.PathToAI).ToArray());
            dropdown.SelectedItem = toSelect.ToUpper();
            if (dropdown.SelectedIndex == -1)
            {
                if (dropdown.Items.Contains("PRODUCTION/FRONTEND")) dropdown.SelectedItem = "PRODUCTION/FRONTEND";
                else dropdown.SelectedIndex = 0;
            }
            dropdown.EndUpdate();
        }

        /* Utility: get composite name */
        public static string GetCompositeName(Composite comp)
        {
            if (comp == null)
                return "";
            string[] cont = comp.name.Replace('\\', '/').Split('/');
            return cont[cont.Length - 1];
        }

        /* Utility: close the game down */
        public static void CloseAI(List<string> additionalProcesses = null)
        {
            List<Process> allProcesses = new List<Process>(Process.GetProcessesByName("AI"));
            if (additionalProcesses != null)
            {
                foreach (string additionalProcess in additionalProcesses)
                {
                    allProcesses.AddRange(Process.GetProcessesByName(additionalProcess));
                }
            }
            for (int x = 0; x < allProcesses.Count; x++)
            {
                try
                {
                    allProcesses[x]?.Kill();
                    allProcesses[x]?.WaitForExit();
                }
                catch { }
            }
        }

        /* Utility: get the image/group index for an entity, for populating ListViewItems */
        public static (int, int) GetIndexesForListViewItem(Entity entity, Composite composite, Commands commands)
        {
            //Keep these indexes in sync with ListViewGroup 
            int imageIndex = 0;
            int groupIndex = 0;
            switch (entity.variant)
            {
                case EntityVariant.VARIABLE:
                    CompositePinInfoTable.PinInfo pinInfo = commands.Utils.GetPinInfo(composite, (VariableEntity)entity);
                    if (pinInfo != null)
                    {
                        switch ((CompositePinType)pinInfo.PinTypeGUID.AsUInt32)
                        {
                            case CompositePinType.CompositeInputAnimationInfoVariablePin:
                            case CompositePinType.CompositeInputBoolVariablePin:
                            case CompositePinType.CompositeInputDirectionVariablePin:
                            case CompositePinType.CompositeInputFloatVariablePin:
                            case CompositePinType.CompositeInputIntVariablePin:
                            case CompositePinType.CompositeInputObjectVariablePin:
                            case CompositePinType.CompositeInputPositionVariablePin:
                            case CompositePinType.CompositeInputStringVariablePin:
                            case CompositePinType.CompositeInputVariablePin:
                            case CompositePinType.CompositeInputZoneLinkPtrVariablePin:
                            case CompositePinType.CompositeInputZonePtrVariablePin:
                            case CompositePinType.CompositeInputEnumVariablePin:
                            case CompositePinType.CompositeInputEnumStringVariablePin:
                            case CompositePinType.CompositeMethodPin:
                                imageIndex = 6;
                                break;
                            case CompositePinType.CompositeOutputAnimationInfoVariablePin:
                            case CompositePinType.CompositeOutputBoolVariablePin:
                            case CompositePinType.CompositeOutputDirectionVariablePin:
                            case CompositePinType.CompositeOutputFloatVariablePin:
                            case CompositePinType.CompositeOutputIntVariablePin:
                            case CompositePinType.CompositeOutputObjectVariablePin:
                            case CompositePinType.CompositeOutputPositionVariablePin:
                            case CompositePinType.CompositeOutputStringVariablePin:
                            case CompositePinType.CompositeOutputVariablePin:
                            case CompositePinType.CompositeOutputZoneLinkPtrVariablePin:
                            case CompositePinType.CompositeOutputZonePtrVariablePin:
                            case CompositePinType.CompositeOutputEnumVariablePin:
                            case CompositePinType.CompositeOutputEnumStringVariablePin:
                            case CompositePinType.CompositeTargetPin:
                            case CompositePinType.CompositeReferencePin:
                                imageIndex = 5;
                                break;
                        }
                    }
                    else
                    {
                        imageIndex = 0;
                    }
                    groupIndex = 0;
                    break;
                case EntityVariant.FUNCTION:
                    if (!((FunctionEntity)entity).function.IsFunctionType)
                    {
                        groupIndex = 2;
                        imageIndex = 2;
                    }
                    else
                    {
                        groupIndex = 1;
                        imageIndex = 1;
                    }
                    break;
                case EntityVariant.PROXY:
                    groupIndex = 3;
                    imageIndex = 3;
                    break;
                case EntityVariant.ALIAS:
                    groupIndex = 4;
                    imageIndex = 4;
                    break;
            }
            return (imageIndex, groupIndex);
        }

        /* Utility: work out if any proxies/overrides reference the currently selected entity */
        public bool IsEntityReferencedExternally(Entity entity, CancellationToken ct)
        {
            bool found = false;
            Parallel.ForEach(Content.Level.Commands.Entries, (comp, status) =>
            {
                Parallel.ForEach(comp.proxies, (prox, status2) =>
                {
                    if (found || ct.IsCancellationRequested)
                        status2.Stop();
                    if (Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveProxy(prox)).Item2 == entity) 
                        found = true;
                });
                Parallel.ForEach(comp.aliases, (alias, status2) =>
                {
                    if (found || ct.IsCancellationRequested)
                        status2.Stop();
                    if (Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveAlias(alias, comp)).Item2 == entity) 
                        found = true;
                });
                List<FunctionEntity> triggerSequences = comp.GetFunctionEntitiesOfType(FunctionType.TriggerSequence);
                Parallel.ForEach(triggerSequences, (trigEnt, status2) =>
                {
                    if (found || ct.IsCancellationRequested)
                        status2.Stop();

                    TriggerSequence trig = (TriggerSequence)trigEnt;
                    Parallel.ForEach(trig.sequence, (trigger, status3) =>
                    {
                        if (found || ct.IsCancellationRequested)
                            status3.Stop();
                        if (Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveAlias(trigger.connectedEntity.path, comp)).Item2 == entity)
                            found = true;
                    });
                });
                List<FunctionEntity> cageAnims = comp.GetFunctionEntitiesOfType(FunctionType.CAGEAnimation);
                Parallel.ForEach(cageAnims, (animEnt, status2) =>
                {
                    if (found || ct.IsCancellationRequested)
                        status2.Stop();

                    CAGEAnimation anim = (CAGEAnimation)animEnt;
                    Parallel.ForEach(anim.connections, (connection, status3) =>
                    {
                        if (found || ct.IsCancellationRequested)
                            status3.Stop();
                        if (Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveAlias(connection.connectedEntity.path, comp)).Item2 == entity) 
                            found = true;
                    });
                });

                if (found || ct.IsCancellationRequested)
                    status.Stop();
            });
            return found;
        }

        /* Utility: try figure out what zone this entity is in (if any) */
        public void TryFindZoneForEntity(Entity entity, Composite startComposite, out Composite composite, out FunctionEntity zone, CancellationToken ct)
        {
            Func<Composite, FunctionEntity> findZone = comp => {
                if (comp == null) return null;

                FunctionEntity toReturn = null;
                ShortGuid compositesGUID = ShortGuidUtils.Generate("composites");

                List<FunctionEntity> triggerSequences = comp.GetFunctionEntitiesOfType(FunctionType.TriggerSequence);
                Parallel.ForEach(triggerSequences, (Action<FunctionEntity, ParallelLoopState>)((trigEnt, status) =>
                {
                    TriggerSequence trig = (TriggerSequence)trigEnt;
                    Parallel.ForEach(trig.sequence, (Action<TriggerSequence.SequenceEntry, ParallelLoopState>)((trigger, status2) =>
                    {
                        if (Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveAlias(trigger.connectedEntity.path, comp)).Item2 == entity)
                        {
                            List<FunctionEntity> zones = comp.functions.FindAll(o => o.function == FunctionType.Zone);
                            Parallel.ForEach(zones, (z, status3) =>
                            {
                                Parallel.ForEach(z.childLinks, (link, status4) =>
                                {
                                    if (link.thisParamID == compositesGUID && link.linkedEntityID == trig.shortGUID)
                                    {
                                        toReturn = z;

                                        status.Stop();
                                        status2.Stop();
                                        status3.Stop();
                                        status4.Stop();
                                    }

                                    if (ct.IsCancellationRequested)
                                        status4.Stop();
                                });

                                if (ct.IsCancellationRequested)
                                    status3.Stop();
                            });
                        }

                        if (ct.IsCancellationRequested)
                            status2.Stop();
                    }));

                    if (ct.IsCancellationRequested)
                        status.Stop();
                }));

                return toReturn;
            };

            composite = startComposite;
            zone = findZone(composite);
            if (zone != null) return;

            foreach (Composite comp in Content.Level.Commands.Entries)
            {
                composite = comp;
                zone = findZone(composite);
                if (zone != null) return;
            }
        }

        [Obsolete("This function is safe to use but not performant. It's intended for test code only.")]
        public string GetAllZonesForEntity(Entity entity)
        {
            ShortGuid compositesGUID = ShortGuidUtils.Generate("composites");
            string toReturn = "";
            List<ShortGuid> foundIDs = new List<ShortGuid>();
            foreach (Composite comp in Content.Level.Commands.Entries)
            {
                List<FunctionEntity> triggerSequences = comp.functions_dictionary.Values.Where(o => o.function == FunctionType.TriggerSequence).ToList();
                foreach (FunctionEntity trigEnt in triggerSequences)
                {
                    TriggerSequence trig = (TriggerSequence)trigEnt;
                    foreach (TriggerSequence.SequenceEntry trigger in trig.sequence)
                    {
                        if (Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveAlias(trigger.connectedEntity.path, comp)).Item2 == entity)
                        {
                            List<FunctionEntity> zones = comp.functions_dictionary.Values.Where(o => o.function == FunctionType.Zone).ToList();
                            foreach (FunctionEntity z in zones)
                            {
                                foreach (EntityConnector link in z.childLinks)
                                {
                                    if (link.thisParamID == compositesGUID && link.linkedEntityID == trig.shortGUID)
                                    {
                                        if (foundIDs.Contains(z.shortGUID))
                                            continue;

                                        Parameter p = z.GetParameter("name");
                                        string name = "";
                                        if (p != null && p.content.dataType == DataType.STRING)
                                            name = ((cString)p.content).value;

                                        foundIDs.Add(z.shortGUID);
                                        toReturn += "\n[Found zone entity '" + z.shortGUID.ToByteString() + "' (" + name + ") in composite '" + comp.name + "']\n";
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return toReturn;
        }
    }

    //Various extensions
    public static class EditorExtensions
    {
        /* Try and localise a string */
        public static string TryLocalise(this string str)
        {
            //Check English level-specific strings first, if a level is loaded
            if (Singleton.Editor?.CommandsDisplay?.Content != null)
                foreach (KeyValuePair<string, TextDB> entry in Singleton.Editor.CommandsDisplay.Content.Level.Strings["ENGLISH"])
                    if (entry.Value.Entries.TryGetValue(str, out string localised))
                        return localised;

            //Check English global strings
            foreach (KeyValuePair<string, TextDB> entry in Singleton.GlobalTextDBs)
                if (entry.Value.Entries.TryGetValue(str, out string localised))
                    return localised;

            return str;
        }
    }

    //Enum UI string converter
    public static class EnumUI
    {
        static Dictionary<CompositePinType, string> _compositePinType = new Dictionary<CompositePinType, string>();
        static Dictionary<DataType, string> _dataType = new Dictionary<DataType, string>();

        static EnumUI()
        {
            _compositePinType.Add(CompositePinType.CompositeMethodPin, "Method");
            _compositePinType.Add(CompositePinType.CompositeTargetPin, "Target");
            _compositePinType.Add(CompositePinType.CompositeReferencePin, "Reference");
            _compositePinType.Add(CompositePinType.CompositeOutputVariablePin, "Output Untyped");
            _compositePinType.Add(CompositePinType.CompositeOutputStringVariablePin, "Output String");
            _compositePinType.Add(CompositePinType.CompositeOutputBoolVariablePin, "Output Bool");
            _compositePinType.Add(CompositePinType.CompositeOutputFloatVariablePin, "Output Float");
            _compositePinType.Add(CompositePinType.CompositeOutputIntVariablePin, "Output Integer");
            _compositePinType.Add(CompositePinType.CompositeOutputPositionVariablePin, "Output Transform");
            _compositePinType.Add(CompositePinType.CompositeOutputDirectionVariablePin, "Output Vector");
            _compositePinType.Add(CompositePinType.CompositeOutputEnumVariablePin, "Output Enum");
            _compositePinType.Add(CompositePinType.CompositeOutputEnumStringVariablePin, "Output Enum String");
            _compositePinType.Add(CompositePinType.CompositeOutputObjectVariablePin, "Output Object");
            _compositePinType.Add(CompositePinType.CompositeOutputAnimationInfoVariablePin, "Output Animation Info");
            _compositePinType.Add(CompositePinType.CompositeOutputZoneLinkPtrVariablePin, "Output Zone Link Ptr");
            _compositePinType.Add(CompositePinType.CompositeOutputZonePtrVariablePin, "Output Zone Ptr");
            _compositePinType.Add(CompositePinType.CompositeInputVariablePin, "Input Untyped");
            _compositePinType.Add(CompositePinType.CompositeInputStringVariablePin, "Input String");
            _compositePinType.Add(CompositePinType.CompositeInputBoolVariablePin, "Input Bool");
            _compositePinType.Add(CompositePinType.CompositeInputFloatVariablePin, "Input Float");
            _compositePinType.Add(CompositePinType.CompositeInputIntVariablePin, "Input Integer");
            _compositePinType.Add(CompositePinType.CompositeInputPositionVariablePin, "Input Transform");
            _compositePinType.Add(CompositePinType.CompositeInputDirectionVariablePin, "Input Vector");
            _compositePinType.Add(CompositePinType.CompositeInputEnumVariablePin, "Input Enum");
            _compositePinType.Add(CompositePinType.CompositeInputEnumStringVariablePin, "Input Enum String");
            _compositePinType.Add(CompositePinType.CompositeInputObjectVariablePin, "Input Object");
            _compositePinType.Add(CompositePinType.CompositeInputAnimationInfoVariablePin, "Input Animation Info");
            _compositePinType.Add(CompositePinType.CompositeInputZoneLinkPtrVariablePin, "Input Zone Link Ptr");
            _compositePinType.Add(CompositePinType.CompositeInputZonePtrVariablePin, "Input Zone Ptr");

            _dataType.Add(DataType.BOOL, "Bool");
            _dataType.Add(DataType.INTEGER, "Integer");
            _dataType.Add(DataType.FLOAT, "Float");
            _dataType.Add(DataType.STRING, "String");
            _dataType.Add(DataType.FILEPATH, "Filepath");
            _dataType.Add(DataType.SPLINE, "Spline");
            _dataType.Add(DataType.VECTOR, "Vector");
            _dataType.Add(DataType.TRANSFORM, "Transform");
            _dataType.Add(DataType.ENUM, "Enum");
            _dataType.Add(DataType.ENUM_STRING, "Enum String");
            _dataType.Add(DataType.RESOURCE, "Resource");
            _dataType.Add(DataType.OBJECT, "Object");
            _dataType.Add(DataType.ZONE, "Zone");
            _dataType.Add(DataType.ZONE_LINK, "Zone Link");
            _dataType.Add(DataType.RESOURCE_ID, "Resource ID");
            _dataType.Add(DataType.REFERENCE_FRAME, "Reference Frame");
            _dataType.Add(DataType.ANIMATION_INFO, "Animation Info");
            _dataType.Add(DataType.COLOUR, "Colour");
            _dataType.Add(DataType.NONE, "None");
        }

        public static string ToUIString(this CompositePinType type)
        {
            return _compositePinType[type];
        }
        public static CompositePinType ToCompositePinType(this string str)
        {
            return _compositePinType.FirstOrDefault(o => o.Value == str).Key;
        }

        public static string ToUIString(this DataType type)
        {
            return _dataType[type];
        }
        public static DataType ToDataType(this string str)
        {
            return _dataType.FirstOrDefault(o => o.Value == str).Key;
        }
    }

    public static class EnumExtensions
    {
        public static IEnumerable<TEnum> GetValuesInDeclarationOrder<TEnum>() where TEnum : Enum
        {
            Type enumType = typeof(TEnum);
            FieldInfo[] fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
            return fields.OrderBy(f => f.MetadataToken).Select(f => (TEnum)f.GetValue(null));
        }

        public static IEnumerable<string> GetNamesInDeclarationOrder<TEnum>() where TEnum : Enum
        {
            Type enumType = typeof(TEnum);
            FieldInfo[] fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
            return fields.OrderBy(f => f.MetadataToken).Select(f => f.Name);
        }
    }

    public static class Steam
    {
        public enum Achievements 
        {
            FIRST_LOAD, // User has loaded a level for the first time
            FIRST_SAVE, // User has performed their first save of a level
            ONE_HUNDRED_SAVES, // User has saved 100 times
            CREATE_A_NEW_ENTITY, // User has created a new entity for the first time
            ONE_HUNDRED_ENTITIES, // User has created 100 new entities
            LAUNCHED_GAME, // User has launched in to the game
            BACKUP_CREATED, // User has made their first backup
            DOCUMENTATION_CHECKED, // User has visited the documentation
            CONFIG_MODIFIED, // User has modified a config file
            BEHAVIOUR_TREE_TOOL_LAUNCHED, // User has launched the behaviour tree tool
            ASSETS_MODIFIED, // User has modified some assets
            LEVEL_VIEWER_LAUNCHED, // User has launched the level viewer for the first time
            GALAXY_MODIFIED, // User has modified the galaxy config
        }

        public static void UnlockAchievement(Achievements achievement)
        {
#if SHIP_BUILD
            if (!Singleton.IsSteamworks)
                return;

            bool result = SteamUserStats.SetAchievement(achievement.ToString());
            if (result)
                SteamUserStats.StoreStats();
            else
                Console.WriteLine("Failed to unlock achievement: " + achievement.ToString());
#endif
        }

        public enum RichPresences
        {
            EditingLevel,

            NO_PRESENCE
        }

        private static RichPresences _currentRP = RichPresences.NO_PRESENCE;
        private static string _currentAI = "";

        public static void UpdatePresence(RichPresences presence, string additionalInfo = "")
        {
#if SHIP_BUILD
            if (!Singleton.IsSteamworks)
                return;

            if (presence == _currentRP && additionalInfo == _currentAI)
                return;

            if (presence == RichPresences.NO_PRESENCE)
            {
                SteamFriends.ClearRichPresence();
                return;
            }

            if (additionalInfo != "")
                SteamFriends.SetRichPresence("AdditionalInfo", additionalInfo);
            SteamFriends.SetRichPresence("steam_display", "#" + presence.ToString());

            _currentRP = presence;
            _currentAI = additionalInfo;
#endif
        }
    }
}
