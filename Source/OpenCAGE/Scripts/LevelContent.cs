using CATHODE;
using CATHODE.EXPERIMENTAL;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Xml.XPath;
using static CathodeLib.CompositeFlowgraphTable;

namespace CommandsEditor
{
    public class LevelContent : IDisposable
    {
        private bool _disposed = false;
        
        public Level Level;
        public Dictionary<Composite, Dictionary<Entity, ListViewItem>> composite_content_cache = new Dictionary<Composite, Dictionary<Entity, ListViewItem>>();
        public EditorUtils EditorUtils = null; //TODO: this should really be refactored. hacked in legacy stuff.

        public bool IsVanilla = false; //The user has not yet saved this level using OpenCAGE

        private Thread _globalUpdateThread = null;

        public LevelContent(string levelName)
        {
            Level = new Level(Singleton.PathToAI + "/DATA/ENV/" + levelName + "/", Singleton.Global, false);
        }

        public void Load()
        {
            Level.Load();
            if (!Level.Commands.Loaded || Level.Commands.EntryPoints == null || Level.Commands.EntryPoints[0] == null)
            {
                MessageBox.Show("Failed to load the level.\nPlease reset your game files!", "Load failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Level.Commands.Entries = Level.Commands.Entries.OrderBy(o => o.name).ToList();

            //Import Global stuff, if required
#if IMPORT_GLOBAL_ASSETS
            _globalUpdateThread = new Thread(() => {
                Level.ImportFromGlobal();

                //TODO: should ensure AI is closed before doing this!
                Singleton.Global.Textures.Entries.Clear();
                Singleton.Global.Textures.Save();
            });
            _globalUpdateThread.Start();
#endif

            //Link up commands to utils and cache some things
            FlowgraphLayoutManager.LinkCommands(this);
            ParameterModificationTracker.LinkCommands(Level.Commands);

            //If we're loading for the first time...
            if (!Level.Commands.Utils.Flags.HasBeenModified)
            {
                //Tidy up composite names so things look nicer - only need to do this for PAK, BIN has this info
                if (Path.GetFileName(Level.Commands.Filepath.ToUpper()) == "COMMANDS.PAK")
                    Level.Commands.Utils.SetPrettyNames();

                //Correct the root composite name - by default it's a full filepath which looks gross
                Level.Commands.EntryPoints[0].name = EditorUtils.GetCompositeName(Level.Commands.EntryPoints[0]);

                //Apply material remappings
                ShortGuid mapping = ShortGuidUtils.Generate("mapping");
                FlowgraphMeta.SupportedLevel levelID;
                bool hasLevelID = Enum.TryParse(Path.GetFileName(Level.Name).ToUpper(), out levelID);
                foreach (MaterialMappingTable.Mapping map in CustomTable.Vanilla.MaterialMappings.Mappings)
                {
                    if (!map.AlwaysUse && (!hasLevelID || !map.SupportedLevels.HasFlag(levelID)))
                        continue;

                    Composite comp = Level.Commands.GetComposite(map.CompositeID);
                    Entity ent = comp?.GetEntityByID(map.EntityID);
                    ent?.AddParameter(mapping, new cResource(null, map.MappingID));
                }
                foreach (MaterialMappingTable.MappingAlias map in CustomTable.Vanilla.MaterialMappings.MappingAliases)
                {
                    if (!map.AlwaysUse && (!hasLevelID || !map.SupportedLevels.HasFlag(levelID)))
                        continue;

                    EntityPath path = new EntityPath(map.EntityPath.ToArray());
                    Composite comp = Level.Commands.GetComposite(map.CompositeID);
                    if (comp == null)
                        continue;
                    bool didFind = false;
                    foreach (KeyValuePair<ShortGuid, AliasEntity> alias in comp.aliases_dictionary)
                    {
                        if (alias.Value.alias == path)
                        {
                            didFind = true;
                            alias.Value.AddParameter(mapping, new cResource(null, map.MappingID));
                            break;
                        }
                    }
                    if (!didFind)
                    {
                        comp.AddAlias(path.path).AddParameter(mapping, new cResource(null, map.MappingID));
                    }
                }

                //Remember that we were modified so we don't do this again
                Level.Commands.Utils.Flags.HasBeenModified = true;
            }

            //Correct all Entity names that are actually pointers to resources
            foreach (Composite comp in Level.Commands.Entries)
            {
                foreach (FunctionEntity func in comp.functions)
                {
                    if (func.function.AsFunctionType == FunctionType.EnvironmentModelReference)
                    {
                        //Lookup skeleton name
                    }
                    else if (func.function.AsFunctionType == FunctionType.PhysicsSystem)
                    {
                        //Lookup Havok name
                    }
                    else if (func.function.AsFunctionType == FunctionType.ModelReference)
                    {
                        //If no renderable, or renderable can't be looked up, delete?
                    }
                    else if (func.function.AsFunctionType == FunctionType.RadiosityProxy)
                    {
                        //Delete? I think these are always unresolvable in retail?
                    }
                }
            }

            Singleton.OnLevelLoaded?.Invoke(this);
        }

        public void Save()
        {
            Level.Save();
#if !IMPORT_GLOBAL_ASSETS
            //TODO - we can't actually save the global textures without re-saving every other level as it'll screw with indexes - need to make a utility to make this simpler.
            //Singleton.Global?.Textures?.Save();
#endif
            IsVanilla = false;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (_globalUpdateThread != null)
                {
                    _globalUpdateThread.Abort();
                    _globalUpdateThread = null;
                }

                if (Level?.Commands != null)
                {
                    if (FlowgraphLayoutManager.LinkedCommands == Level.Commands)
                    {
                        FlowgraphLayoutManager.LinkCommands(null);
                    }
                    if (ParameterModificationTracker.LinkedCommands == Level.Commands)
                    {
                        ParameterModificationTracker.LinkCommands(null);
                    }
                }

                if (composite_content_cache != null)
                {
                    foreach (var compositeDict in composite_content_cache.Values)
                    {
                        if (compositeDict != null)
                        {
                            foreach (var kvp in compositeDict)
                            {
                                if (kvp.Value != null && kvp.Value.Tag != null)
                                {
                                    kvp.Value.Tag = null;
                                }
                            }
                            compositeDict.Clear();
                        }
                    }
                    composite_content_cache.Clear();
                }

                EditorUtils = null;

                if (Level != null)
                {
                    Level.OnLoadTick = null;
                    Level.OnSaveTick = null;
                    Level = null;
                }
                
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
            }

            _disposed = true;
        }

        ~LevelContent()
        {
            Dispose(false);
        }

        //FOR TESTING ONLY!! Loads a LevelContent object for the given level on the current thread, and generates ShortGuids for every string.
        [Obsolete("This function is safe to use but not performant. It's intended for test code only.")]
        public static LevelContent DEBUG_LoadUnthreadedAndPopulateShortGuids(string level)
        {
            LevelContent content = new LevelContent(level);
            content.Level.Load();

            content.EditorUtils = new EditorUtils(content);
            content.EditorUtils.GenerateEntityNameCache(Singleton.Editor);
            content.EditorUtils.GenerateCompositeInstances(content.Level.Commands, false);

            //TODO: maybe we want this to happen normally??
            for (int i = 0; i < content.Level.SoundEventData.Entries.Count; i++)
            {
                for (int x = 0; x < content.Level.SoundEventData.Entries[i].events.Count; x++)
                {
                    ShortGuidUtils.Generate(content.Level.SoundEventData.Entries[i].events[x].name);
                }
            }
            foreach (ParameterVariant enumValue in Enum.GetValues(typeof(ParameterVariant)))
                ShortGuidUtils.Generate(enumValue.ToString());
            foreach (DataType enumValue in Enum.GetValues(typeof(DataType)))
                ShortGuidUtils.Generate(enumValue.ToString());
            foreach (ObjectType enumValue in Enum.GetValues(typeof(ObjectType)))
                ShortGuidUtils.Generate(enumValue.ToString());
            foreach (FunctionType enumValue in Enum.GetValues(typeof(FunctionType)))
                ShortGuidUtils.Generate(enumValue.ToString());
            foreach (ResourceType enumValue in Enum.GetValues(typeof(ResourceType)))
                ShortGuidUtils.Generate(enumValue.ToString());
            foreach (EnumType enumValue in Enum.GetValues(typeof(EnumType)))
                ShortGuidUtils.Generate(enumValue.ToString());

            ShortGuidUtils.Generate("AnimatedModel");

            foreach (Composite composite in content.Level.Commands.Entries)
                foreach (Entity entity in composite.GetEntities())
                    ShortGuidUtils.Generate(content.Level.Commands.Utils.GetEntityName(composite, entity));

            foreach (Models.CS2 cs2 in content.Level.Models.Entries)
            {
                ShortGuidUtils.Generate(cs2.Name);
                foreach (Models.CS2.Component component in cs2.Components)
                    foreach (Models.CS2.Component.LOD lod in component.LODs)
                        ShortGuidUtils.Generate(lod.Name);
            }

            foreach (Materials.Material material in content.Level.Materials.Entries)
                ShortGuidUtils.Generate(material.Name);

            //List<string> entNames = EntityUtils.GetAllVanillaNames();
            //foreach (string entName in entNames)
            //    ShortGuidUtils.Generate(entName);

            return content;
        }

        public enum CacheMethod
        {
            CHECK_OR_POPULATE_CACHE,
            IGNORE_AND_OVERWRITE_CACHE,
            IGNORE_CACHE,
        }

        public ListViewItem GenerateListViewItem(Entity entity, Composite composite, CacheMethod cacheMethod = CacheMethod.CHECK_OR_POPULATE_CACHE)
        {
            if (_disposed || Level?.Commands == null)
                throw new ObjectDisposedException(nameof(LevelContent));

            if (cacheMethod == CacheMethod.CHECK_OR_POPULATE_CACHE)
            {
                if (composite_content_cache.TryGetValue(composite, out Dictionary<Entity, ListViewItem> items))
                    if (items.TryGetValue(entity, out ListViewItem cachedItem))
                        return cachedItem;
            }

            ListViewItem item = new ListViewItem()
            {
                Tag = entity
            };
            switch (entity.variant)
            {
                case EntityVariant.VARIABLE:
                    item.Text = ShortGuidUtils.FindString(((VariableEntity)entity).name);
                    CompositePinInfoTable.PinInfo variableInfo = Level.Commands.Utils.GetPinInfo(composite, (VariableEntity)entity);
                    item.SubItems.Add(variableInfo != null ? ((CompositePinType)variableInfo.PinTypeGUID.AsUInt32).ToUIString() : ((VariableEntity)entity).type.ToUIString());
                    break;
                case EntityVariant.FUNCTION:
                    item.Text = Level.Commands.Utils.GetEntityName(composite.shortGUID, entity.shortGUID);
                    Composite funcComposite = Level.Commands.GetComposite(((FunctionEntity)entity).function);
                    if (funcComposite != null) item.SubItems.Add(EditorUtils.GetCompositeName(funcComposite));
                    else item.SubItems.Add(((FunctionType)(((FunctionEntity)entity).function.AsUInt32)).ToString());
                    break;
                case EntityVariant.ALIAS:
                    item.Text = Level.Commands.Utils.GetResolvedAsString(Level.Commands.Utils.ResolveAlias((AliasEntity)entity, composite), SettingsManager.GetBool(Singleton.Settings.ShowShortGuids));
                    item.SubItems.Add("");
                    break;
                case EntityVariant.PROXY:
                    item.Text = Level.Commands.Utils.GetEntityName(composite.shortGUID, entity.shortGUID); 
                    item.SubItems.Add(Level.Commands.Utils.GetResolvedAsString(Level.Commands.Utils.ResolveProxy((ProxyEntity)entity), SettingsManager.GetBool(Singleton.Settings.ShowShortGuids)));
                    break;
            }
            item.SubItems.Add(entity.shortGUID.ToByteString());

            switch (cacheMethod)
            {
                case CacheMethod.CHECK_OR_POPULATE_CACHE:
                    //we wanted to check the cache and it wasn't there, so lets add it
                    if (!composite_content_cache.ContainsKey(composite))
                    {
                        composite_content_cache.Add(composite, new Dictionary<Entity, ListViewItem>());
                    }
                    if (!composite_content_cache[composite].ContainsKey(entity))
                    {
                        composite_content_cache[composite].Add(entity, item);
                    }
                    break;

                case CacheMethod.IGNORE_AND_OVERWRITE_CACHE:
                    //we want to write (or overwrite) the cache, so lets do that
                    if (composite_content_cache.TryGetValue(composite, out Dictionary<Entity, ListViewItem> items))
                    {
                        if (items.ContainsKey(entity))
                        {
                            items[entity] = item;
                        }
                        else
                        {
                            items.Add(entity, item);
                        }
                    }
                    else
                    {
                        Dictionary<Entity, ListViewItem> dict = new Dictionary<Entity, ListViewItem>();
                        dict.Add(entity, item);
                        composite_content_cache.Add(composite, dict);
                    }
                    break;
            }

            return item;
        }
    }
}
