using AlienPAK;
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

namespace OpenCAGE
{
    public class LevelContent : IDisposable
    {
        private bool _disposed = false;
        
        public Level Level;
        public Dictionary<Composite, Dictionary<Entity, ListViewItem>> composite_content_cache = new Dictionary<Composite, Dictionary<Entity, ListViewItem>>();
        public EditorUtils EditorUtils = null; //TODO: this should really be refactored. hacked in legacy stuff.

        public bool IsVanilla = false; //The user has not yet saved this level using OpenCAGE

        public LevelContent(string levelName)
        {
            Level = new Level(Singleton.PathToAI + "/DATA/ENV/" + levelName + "/", Singleton.Global, false);
        }

        #region Collision compaction
        static LevelContent()
        {
            //A collision compaction (Save/Build option, MCP compact_collision) keeps every proxy the undo history can bring back
            CathodeLib.Havok.CollisionCompactor.HeldElsewhere = ProxiesHeldByUndo;
        }

        /// <summary>Options > Save/Build > Compact Collision On Build, for the instanced save about to run.</summary>
        private static void ApplyCollisionCompaction(Level level)
        {
            if (level != null)
                level.CompactCollisionOnSave = SettingsManager.GetBool(Settings.CompactCollisionOnBuild);
        }

        /// <summary>
        /// The collision proxies the undo and redo history still holds - the COLLISION_MAPPING rows and compounds reachable from any edit's
        /// fields: an entity or composite a delete keeps, a row a resource edit or a brush edit can put back, a proxy a pair edit made. An
        /// undo past a compaction then still finds its proxy. Null (nothing is pruned) when the walk cannot finish.
        /// </summary>
        internal static ICollection<HavokPackfile.StaticCompoundShape> ProxiesHeldByUndo(Level level)
        {
            List<Undo.IEdit> edits;
            try { edits = Undo.UndoStack.Current.HeldEdits(); }
            catch { return null; }

            HashSet<HavokPackfile.StaticCompoundShape> found = new HashSet<HavokPackfile.StaticCompoundShape>(new ByReference<HavokPackfile.StaticCompoundShape>());
            HashSet<object> seen = new HashSet<object>(new ByReference<object>());
            Queue<object> queue = new Queue<object>(edits.Cast<object>());
            Dictionary<Type, System.Reflection.FieldInfo[]> fieldsOf = new Dictionary<Type, System.Reflection.FieldInfo[]>();
            int budget = 2000000;
            while (queue.Count != 0)
            {
                if (--budget < 0)
                    return null;
                object o = queue.Dequeue();
                if (o == null || !seen.Add(o))
                    continue;
                if (o is HavokPackfile.StaticCompoundShape compound) { found.Add(compound); continue; }
                if (o is CollisionMaps.COLLISION_MAPPING row) { if (row.CollisionProxy != null) found.Add(row.CollisionProxy); continue; }

                //The level and the editor around it: what the live level holds is counted from the level itself
                if (o is string || o is Delegate || o is Type || o is System.Reflection.MemberInfo || o is Level || o is CathodeFile || o is HavokPackfile
                    || o is LevelContent || o is CommandsUtils || o is System.ComponentModel.Component || o is System.Windows.Forms.Control)
                    continue;
                Type type = o.GetType();
                if (type.IsPrimitive || type.IsEnum || type.IsPointer)
                    continue;
                if (o is System.Collections.IDictionary map)
                {
                    foreach (System.Collections.DictionaryEntry entry in map) { queue.Enqueue(entry.Key); queue.Enqueue(entry.Value); }
                    continue;
                }
                string ns = type.Namespace ?? "";
                bool ours = ns.StartsWith("CATHODE") || ns.StartsWith("CathodeLib") || ns.StartsWith("OpenCAGE");
                if (!ours && o is System.Collections.IEnumerable items)
                {
                    if (!(o is Array array) || !array.GetType().GetElementType().IsPrimitive)
                        foreach (object item in items) queue.Enqueue(item);
                    continue;
                }
                //CATHODE and OpenCAGE types, and the tuples and pairs edits keep them in
                if (!ours && !(type.IsGenericType && (ns == "System" || ns == "System.Collections.Generic")))
                    continue;
                if (!fieldsOf.TryGetValue(type, out System.Reflection.FieldInfo[] fields))
                {
                    List<System.Reflection.FieldInfo> list = new List<System.Reflection.FieldInfo>();
                    for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
                        foreach (System.Reflection.FieldInfo field in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                        {
                            Type f = field.FieldType;
                            if (f.IsPrimitive || f.IsEnum || f == typeof(string) || f.IsPointer || (f.IsValueType && !f.IsGenericType))
                                continue;
                            list.Add(field);
                        }
                    fieldsOf[type] = fields = list.ToArray();
                }
                foreach (System.Reflection.FieldInfo field in fields)
                {
                    object value;
                    try { value = field.GetValue(o); } catch { continue; }
                    if (value != null) queue.Enqueue(value);
                }
            }
            return found;
        }

        private sealed class ByReference<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }
        #endregion

        public void Load()
        {
            Level.Load();
            if (!Level.Commands.Loaded || Level.Commands.EntryPoints == null || Level.Commands.EntryPoints[0] == null)
            {
                MessageBox.Show("Failed to load the level.\nPlease reset your game files!", "Load failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Level.Commands.Entries = Level.Commands.Entries.OrderBy(o => o.name).ToList();

            //Link up commands to utils and cache some things
            FlowgraphLayoutManager.LinkCommands(this);
            CompositePreviewManager.LinkCommands(this);
            ParameterModificationTracker.LinkCommands(Level.Commands);

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

            EnsureEditorUtils();

            //What the level places as it is opened, to tell later whether it needs a Save & Build
            BuildTracker.LevelLoaded(this);

            Singleton.OnLevelLoaded?.Invoke(this);
        }

        public bool IsLevelDataLoaded => Level?.Commands != null && Level.Commands.Loaded && Level.Strings != null;

        public void EnsureEditorUtils(bool generateCompositeInstances = true)
        {
            if (EditorUtils != null)
                return;

            // Commands is enough to build editor utils; Strings may lag behind on some loads.
            if (Level?.Commands == null || !Level.Commands.Loaded)
                return;

            EditorUtils = new EditorUtils(this);
            if (generateCompositeInstances)
                EditorUtils.GenerateCompositeInstances(Level.Commands, false);
        }

        /// <summary>
        /// Bakers that failed on the last instanced save. A baker that throws is caught so one bad
        /// system cannot cost the user the whole save, which means the level silently keeps whatever
        /// that system had on disk - so the failure has to be reported rather than only logged.
        /// </summary>
        public IReadOnlyList<string> LastBakeWarnings { get; private set; }

        /// <summary>
        /// Everything else the last instanced save could not do as asked - a missing model, an
        /// unpacked texture, a shader that would not compile. Each is an edit that never reached
        /// the screen, and the console is not where a user will find out.
        /// </summary>
        public IReadOnlyList<string> LastWarnings { get; private set; }

        /// <summary>
        /// What the last instanced save found in the level itself that will crash the game - an alien that can spawn with no
        /// backstage vent node to break out to (<see cref="AlienBackstageCheck"/>). The save went ahead all the same: the
        /// level is as it was asked to be, and it is the user's to fix.
        /// </summary>
        public IReadOnlyList<string> LastLevelWarnings { get; private set; }

        /// <summary>
        /// Which bakers an instanced save runs. One left out keeps what that system already has on disk
        /// (which may no longer match the geometry) - except radiosity, whose files are cleared, since
        /// they would light the wrong movers.
        /// </summary>
        public class BakeSelection
        {
            public bool NavMesh = true, Cover = true, Radiosity = true, JobPositions = true, Alphalight = true, SoundNetworks = true;
            public bool All => NavMesh && Cover && Radiosity && JobPositions && Alphalight && SoundNetworks;
        }

        /// <summary>The bakers the next instanced save runs (null: all of them). That save takes it and clears it.</summary>
        public BakeSelection NextSaveBakers;

        /// <summary>
        /// The files the last save could not write. The level's files are swapped in all together once every one of them is
        /// written, so when this has any, none of them were and what was on disk is as it was.
        /// </summary>
        public IReadOnlyList<CathodeFile.SaveFailure> LastSaveFailures { get; private set; } = new CathodeFile.SaveFailure[0];

        /// <summary>Whether the last save got as far as swapping its files in (a save that throws after that is on disk all the same).</summary>
        public bool LastSaveWritten { get; private set; }

        public void Save(bool doInstancing)
        {
            BakeSelection bakers = NextSaveBakers;
            NextSaveBakers = null;
            Save(doInstancing, bakers);
        }

        /// <summary>
        /// Radiosity settings for a save, honouring the Options > Save/Build radiosity options. Always Fully Rebuild Radiosity
        /// regenerates the whole level's radiosity from scratch instead of keeping the shipped lighting and baking only what
        /// changed. Don't Rebuild Radiosity (when <paramref name="allowSkippingTheBake"/>) bakes nothing: the level keeps the
        /// radiosity it has, re-bound to the rebuilt models so the game can still read it - moved models keep the lighting of
        /// where they were, added ones are drawn without any until a build with it off. It wins over Always Fully Rebuild.
        /// </summary>
        /// <param name="allowSkippingTheBake">False for a build whose result is shipped rather than edited further (the Mod
        /// Manager's), which always bakes.</param>
        public static CathodeLib.Radiosity.RadiosityBakeSettings RadiositySettingsFromOptions(bool allowSkippingTheBake = true)
        {
            return new CathodeLib.Radiosity.RadiosityBakeSettings()
            {
                PatchRetailRuntime = !SettingsManager.GetBool(Settings.AlwaysFullyRebuildRadiosity),
                KeepExisting = allowSkippingTheBake && SettingsManager.GetBool(Settings.SkipRadiosityRebuild),
            };
        }

        /// <summary>
        /// An instanced save with every baker, as <see cref="Level.SaveInstanced()"/>, but with radiosity settings from the options
        /// (see <see cref="RadiositySettingsFromOptions"/>).
        /// </summary>
        public static Instancing SaveInstancedWithOptions(Level level, bool allowSkippingTheBake = true)
        {
            ApplyCollisionCompaction(level);
            return level.SaveInstanced(new CathodeLib.NavMesh.NavMeshBakeSettings(), new CathodeLib.NavMesh.CoverBakeSettings(), RadiositySettingsFromOptions(allowSkippingTheBake),
                new CathodeLib.NavMesh.JobPositionBakeSettings(), new CathodeLib.Alphalight.AlphalightBakeSettings(), new CathodeLib.Sound.SoundNetworkBakeSettings());
        }

        /// <summary>What the last instanced save's radiosity pass did (null when it ran none): see <see cref="Instancing.RadiosityResult"/>.</summary>
        public CathodeLib.Radiosity.RadiosityBaker.BakeResult LastRadiosityResult { get; private set; }

        public void Save(bool doInstancing, BakeSelection bakers)
        {
            //Pristine copies of every still-vanilla file in the level, before the save rewrites them
            Modding.ModServices.CaptureLevelBeforeSave(Level.Name);
            //Skinned models imported before their bounds were kept the game's way drew nothing in game
            ModelIO.RepairSkinnedBounds(Level, Singleton.AnimationsLoaded ? Singleton.Animations : null);
            //Materials whose samplers were cleared or features changed are laid out as the game reads them (texture slots in sampler order)
            Level.Materials?.NormaliseSamplerSlots();
            //A collision pair's ballistic half follows its entity: placed, switched, hidden, attached and overridden as it is, renamed
            //with it, gone with it. A failure here only loses that (logged), never the save
            try { CollisionPairs.KeepHalvesInStep(Level); }
            catch (Exception e) { Debug.Log("LevelContent", "Keeping the collision pairs' ballistic halves in step failed: " + e); }
            LastBakeWarnings = null;
            LastWarnings = null;
            LastLevelWarnings = null;
            LastRadiosityResult = null;
            LastSaveFailures = new CathodeFile.SaveFailure[0];
            LastSaveWritten = false;

            /* Every file the save writes is written beside where it belongs, and swapped in with the rest only once all of them
               are: a file that fails (read-only, open in another program, a full drive, a writer that throws) leaves the whole
               level on disk as it was, rather than half of it new. The exclusive master index is the one file the save writes
               straight to disk, so it is put back by hand. */
            string masterIndicesPath = Level.Filepath + (Level.Patched ? "_PATCH" : "") + "/WORLD/EXCLUSIVE_MASTER_RESOURCE_INDICES";
            byte[] masterIndices = File.Exists(masterIndicesPath) ? File.ReadAllBytes(masterIndicesPath) : null;
            bool committed = false;
            using (CathodeFile.SaveTransaction transaction = CathodeFile.BeginSaveTransaction())
            {
                try
                {
                    if (doInstancing)
                    {
                        Instancing pass;
                        if (bakers == null || bakers.All)
                        {
                            pass = SaveInstancedWithOptions(Level);
                        }
                        else
                        {
                            //A baker is skipped by passing null for its settings - alphalight bakes regardless, so it has its own switch
                            bool skippedAlphalight = Instancing.SkipAlphalightBake;
                            Instancing.SkipAlphalightBake = skippedAlphalight || !bakers.Alphalight;
                            ApplyCollisionCompaction(Level);
                            try
                            {
                                pass = Level.SaveInstanced(
                                    bakers.NavMesh ? new CathodeLib.NavMesh.NavMeshBakeSettings() : null,
                                    bakers.Cover ? new CathodeLib.NavMesh.CoverBakeSettings() : null,
                                    bakers.Radiosity ? RadiositySettingsFromOptions() : null,
                                    bakers.JobPositions ? new CathodeLib.NavMesh.JobPositionBakeSettings() : null,
                                    bakers.Alphalight ? new CathodeLib.Alphalight.AlphalightBakeSettings() : null,
                                    bakers.SoundNetworks ? new CathodeLib.Sound.SoundNetworkBakeSettings() : null);
                            }
                            finally
                            {
                                Instancing.SkipAlphalightBake = skippedAlphalight;
                            }
                        }
                        LastBakeWarnings = pass?.BakeWarnings;
                        LastWarnings = pass?.Warnings;
                        LastRadiosityResult = pass?.RadiosityResult;
                    }
                    else
                    {
                        Level.Save();
                    }
                    committed = transaction.Commit();
                }
                finally
                {
                    LastSaveFailures = transaction.Failures;
                    LastSaveWritten = committed;
                    if (!committed)
                        RestoreFile(masterIndicesPath, masterIndices);
                }
            }
            //Not saved: what the save would have settled (a build done, the level saved by OpenCAGE) stays as it was
            if (!committed)
                return;

            //The save generated the level's behaviour tree list from the character configs (the files were read just now, so
            //this is cached): a class whose tree can't be listed crashes the game when one of its characters spawns
            List<string> treeProblems = BehaviorTreeDB.ReadRequirements(Level.PathToData).Problems;
            if (treeProblems.Count != 0)
                LastWarnings = (LastWarnings ?? new List<string>()).Concat(treeProblems).ToList();

            //What in the level as it now is will crash the game: said by a build, never stopping it (the save is done)
            if (doInstancing)
                LastLevelWarnings = CheckLevel();

            //A build regenerates what the game derives from the script: from here it matches the script again
            List<string> skipped = new List<string>();
            if (doInstancing && bakers != null)
            {
                if (!bakers.NavMesh) skipped.Add("navmesh");
                if (!bakers.Cover) skipped.Add("cover");
                if (!bakers.Radiosity) skipped.Add("radiosity");
                if (!bakers.JobPositions) skipped.Add("job_positions");
                if (!bakers.Alphalight) skipped.Add("alphalight");
                if (!bakers.SoundNetworks) skipped.Add("sound");
            }
            BuildTracker.Saved(this, doInstancing, skipped);

            IsVanilla = false;
        }

        /* The Level Check a build runs (Scripts\LevelCheck: the alien's vents, the character cap, texture memory and the rest) on the
           level as built: what it finds that the user hasn't dismissed for this level, crashes first, or null for none. A check that
           throws only loses its own findings (logged), never the save; the report is kept for the Level Check window. */
        private List<string> CheckLevel()
        {
            List<string> found = LevelCheck.BuildWarnings(Level);
            return found.Count == 0 ? null : found;
        }

        /* A file the save wrote straight to disk, put back as it was before it: the save was not swapped in */
        private static void RestoreFile(string path, byte[] content)
        {
            if (content == null)
                return;
            try
            {
                File.WriteAllBytes(path, content);
            }
            catch (Exception e)
            {
                Debug.Log("LevelContent", "Could not put back " + path + " after a save that failed: " + e.Message);
            }
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
                //Before anything is taken apart, so the listeners can still tell what was this level's
                Singleton.OnLevelClosing?.Invoke(this);

                if (Level?.Commands != null)
                {
                    if (FlowgraphLayoutManager.LinkedCommands == Level.Commands)
                    {
                        FlowgraphLayoutManager.LinkCommands(null);
                    }
                    if (CompositePreviewManager.LinkedCommands == Level.Commands)
                    {
                        CompositePreviewManager.LinkCommands(null);
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

                //Static caches outlive the level: what they hold of it (texture data, the scripts) would stay in memory until
                //they are next filled
                MaterialApplier.ClearTextureCache();
                CageAnimationDrivers.Invalidate();
                Audio.SoundEventMetadata.Invalidate();

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
                    item.Text = Level.Commands.Utils.GetEntityName(composite, entity);
                    Composite funcComposite = Level.Commands.GetComposite(((FunctionEntity)entity).function);
                    if (funcComposite != null) item.SubItems.Add(EditorUtils.GetCompositeName(funcComposite));
                    else item.SubItems.Add(((FunctionType)(((FunctionEntity)entity).function.AsUInt32)).ToString());
                    break;
                case EntityVariant.ALIAS:
                    //Show the alias's name (its own, or the name of the entity it points at), with the path alongside
                    item.Text = Level.Commands.Utils.GetEntityName(composite, entity);
                    item.SubItems.Add(Level.Commands.Utils.GetResolvedAsString(Level.Commands.Utils.ResolveAlias((AliasEntity)entity, composite), SettingsManager.GetBool(Settings.ShowShortGuids)));
                    break;
                case EntityVariant.PROXY:
                    item.Text = Level.Commands.Utils.GetEntityName(composite, entity);
                    item.SubItems.Add(EditorUtils.DescribeProxyTarget(Level.Commands, (ProxyEntity)entity));
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

        public void ClearEntityListViewCache()
        {
            if (composite_content_cache == null)
                return;

            foreach (Dictionary<Entity, ListViewItem> compositeDict in composite_content_cache.Values)
            {
                if (compositeDict == null)
                    continue;

                foreach (KeyValuePair<Entity, ListViewItem> entry in compositeDict)
                {
                    if (entry.Value?.Tag != null)
                        entry.Value.Tag = null;
                }

                compositeDict.Clear();
            }

            composite_content_cache.Clear();
        }

        public void RemoveCachedEntity(Entity entity, Composite composite)
        {
            if (entity == null || composite == null || composite_content_cache == null)
                return;

            if (composite_content_cache.TryGetValue(composite, out Dictionary<Entity, ListViewItem> items))
                items.Remove(entity);
        }
    }
}
