using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAGE
{
    public static class Singleton
    {
        public static CommandsEditor Editor;

        //Metadata
        public static string PathToAI = "";
        public static PatchManager.Platform Platform = PatchManager.Platform.UNKNOWN;
        public static string Version = "";
        public static string BetaName = "";
        public static bool IsPrimaryInstance = true;
        public static bool DontRequireAIexe = false;

        //Viewport stuff
        public static bool ViewportEnabled = true;
        public static string ViewportExecutablePath
        {
            get
            {
#if DEBUG
                return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Source", "Dependencies", "LevelViewer", "Build", "CathodeEditorGodot.exe"));
#else
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "levelviewer", "CathodeEditorGodot.exe");
#endif
            }
        }

        //Global localised string DBs for English
        public static Dictionary<string, TextDB> GlobalTextDBs = new Dictionary<string, TextDB>();

        //Animation content from ANIMATIONS.PAK
        public static List<string> AllSkeletons = new List<string>();
        public static List<string> AllAnimTrees = new List<string>();
        public static SortedDictionary<string, HashSet<string>> AllAnimations = new SortedDictionary<string, HashSet<string>>(); //Anim Set, Animations
        public static Dictionary<string, HashSet<string>> GenderedSkeletons = new Dictionary<string, HashSet<string>>(); //Gender, Skeletons

        /// <summary>
        /// Make a newly imported clip visible to whatever lists a set's animations. The lists above
        /// are built once, at startup, by scanning ANIMATION.PAK - rescanning would mean reading the
        /// whole PAK again for one new clip, so an import says so here and the pick lists keep up.
        /// </summary>
        public static void RegisterAnimation(string set, string clip)
        {
            if (string.IsNullOrEmpty(set) || string.IsNullOrEmpty(clip)) return;
            if (!AllAnimations.ContainsKey(set)) AllAnimations.Add(set, new HashSet<string>());
            AllAnimations[set].Add(clip);
        }

        //Global animation strings
        public static AnimationStrings AnimationStrings;
        public static AnimationStrings AnimationStrings_Debug;

        //Global assets
        public static Global Global;
        public static Animation Animations { get { return Global?.Animations; } }
        public static bool AnimationsLoaded => Global?.Animations != null;

        //Load events
        public static Action<LevelContent> OnLevelLoaded;
        public static Action<LevelContent> OnLevelAssetsLoaded;
        /// <summary>
        /// The level is being closed (on the UI thread, before the next one starts loading). Anything holding on to
        /// its entities, composites or resources lets go here; waiting for the next OnLevelLoaded keeps the old level
        /// in memory for the whole of the next load, or for good if nothing else is loaded.
        /// </summary>
        public static Action<LevelContent> OnLevelClosing;

        //Reload events
        public static Action<Entity> OnEntityReloaded;
        /// <summary>Several entities selected together, first one first (the one the viewer anchors on).</summary>
        public static Action<List<Entity>> OnEntitiesReloaded;
        /// <summary>The inspector went from having a selection to having none, so anything mirroring the
        /// selection (the level viewer's highlight and gizmo) should clear too. OnEntityReloaded only
        /// ever fires for an entity, so a clear had nothing to listen to and the viewer stayed lit.</summary>
        public static Action OnSelectionCleared;
        public static Action<Composite> OnCompositeReloaded;

        //Selection events
        public static Action<Entity> OnEntitySelected;
        public static Action<Composite> OnCompositeSelected;

        //Misc events
        public static Action OnCAGEAnimationEditorOpened;
        public static Action<Entity, string> OnEntityRenamed;
        public static Action<Composite, string> OnCompositeRenamed;
        public static Action<cTransform, Entity> OnEntityMoved;
        public static Action<Entity, Parameter, bool> OnEntityParameterModified;
        public static Action<Entity> OnEntityDeleted;
        public static Action<Entity, Composite> OnEntityDeletePending;
        public static Action<Composite> OnCompositeDeleted;
        /// <summary>A script edit step (a refactor, an MCP edit, or the undo/redo of one) changed these composites.</summary>
        public static Action<IReadOnlyList<Composite>> OnCompositesModified;
        public static Action OnSaved;
        public static Action OnParameterModified;
        public static Action OnResourceModified;

        /// <summary>
        /// Something has changed in ANIMATION.PAK. That data is shared by every level rather than
        /// owned by one, so anything showing it refreshes rather than assuming it only changes from
        /// the animation browser.
        /// </summary>
        public static Action OnAnimationsModified;
        public static Action OnNodeStyleChanged;

        /// <summary>
        /// A pin delay on this entity changed. Delays are entity parameters, but they are drawn on the
        /// pins of every node for that entity - and the same entity can be on more than one page, so the
        /// page that was edited is not the only one showing the old value.
        /// </summary>
        public static Action<Entity> OnPinDelayModified;
        /// <summary>
        /// Showing this entity in the inspector filled in parameters it didn't have (its defaults). Nothing was edited, so
        /// no edit event goes out, but anything showing the entity's values has more to show.
        /// </summary>
        public static Action<Entity> OnEntityDefaultsApplied;
        public static Action<SelectEnumString> OnEnumStringUIShown;
        public static Action OnResetConfigs;

        //Composite display events
        public static Action<CompositeDisplay> OnCompositeDisplayOpening;
        public static Action<CompositeDisplay> OnCompositeDisplayClosing;

        //Entity about to be / being added
        public static Action OnEntityAddPending;
        public static Action<Entity> OnEntityAdded;
        public static Action OnCompositeAddPending;
        public static Action<Composite> OnCompositeAdded;

        //Get fallback material when level loaded
        public static Materials.Material FallbackMaterial
        {
            get
            {
                Materials.Material fallbackMaterial = Editor?.CompositeBrowser?.Content?.Level?.Materials?.Entries?.FirstOrDefault(o => o.Name == "FALLBACK_MATERIAL");
                if (fallbackMaterial == null) fallbackMaterial = Editor?.CompositeBrowser?.Content?.Level?.Materials?.Entries?[0];
                return fallbackMaterial;
            }
        }

        //Backup state tracking to prevent saving a level while backup is ongoing
        public enum BackupState
        {
            NONE,
            SINGLE_LEVEL,
            ALL_LEVELS,
            //Not a backup: the Mod Manager is changing the game's files (see LoadedGameFiles), and nothing may save over them meanwhile
            APPLYING_MODS,
        }
        public static BackupState CurrentBackupState = BackupState.NONE;
        /// <summary>What the backup state stands for, in words for saying why something must wait: "A level backup is running", "Mods are being applied".</summary>
        public static string BusyText => CurrentBackupState == BackupState.APPLYING_MODS ? "Mods are being applied" : "A level backup is running";
        public static string BackupLevel = ""; 

        public static Action OnAnimationsLoaded;
        public static bool LoadedAnimationContent => _loadedAnimationContent;
        private static bool _loadedAnimationContent = false;

        public static Action OnGlobalAssetsLoaded;
        public static bool LoadedGlobalAssets => _loadedGlobalAssets;
        private static bool _loadedGlobalAssets = false;

        /* Load all shared global data */
        public static void LoadGlobals()
        {
            //Populate localised text string databases (in English)
            if (Directory.Exists(Singleton.PathToAI + "/DATA/TEXT"))
            {
                List<string> textList = Directory.GetFiles(Singleton.PathToAI + "/DATA/TEXT/ENGLISH/", "*.TXT", SearchOption.AllDirectories).ToList<string>();
                {
                    TextDB[] strings = new TextDB[textList.Count];
                    Parallel.For(0, textList.Count, (i) =>
                    {
                        strings[i] = new TextDB(textList[i]);
                    });
                    for (int i = 0; i < textList.Count; i++)
                        GlobalTextDBs.Add(Path.GetFileNameWithoutExtension(textList[i].ToUpper()), strings[i]);
                }
            }

            Debug.Log("Asset Loader", "Loading anim data");

            //Create global & load animation data
            Global = new Global(PathToAI + "\\DATA\\ENV\\GLOBAL\\");
            AnimationPakWrite.Stamp(Global.Animations);
            ListAnimations(Global.Animations)();

            _loadedAnimationContent = true;
            OnAnimationsLoaded?.Invoke();
            _loadedGlobalAssets = true;
            OnGlobalAssetsLoaded?.Invoke();
        }

        /// <summary>
        /// Load the animations again from ANIMATION.PAK, after something else changed it (the Mod Manager): the windows showing
        /// them as they were close, and every list of them follows. What wasn't saved of them is let go - see what that is
        /// first (<see cref="AnimationPakWrite.Unsaved"/>). False, with why, when the file won't load: they then stay as they were.
        /// </summary>
        public static bool ReloadAnimations(out string error)
        {
            error = null;
            string path = Global?.Animations?.PAK?.Filepath;
            if (string.IsNullOrEmpty(path))
            {
                error = "the animations were never loaded";
                return false;
            }
            Animation loaded;
            try { loaded = new Animation(new PAK2(path), false); }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
            if (!loaded.Loaded)
            {
                error = "ANIMATION.PAK couldn't be read";
                return false;
            }
            //Listed before anything is let go of: one that can't be leaves the animations as they were
            Action useLists;
            try { useLists = ListAnimations(loaded); }
            catch (Exception e)
            {
                error = "ANIMATION.PAK couldn't be read (" + e.Message + ")";
                return false;
            }

            //Windows showing the animations as they were close (anything they had unsaved was saved, or let go, before the file changed)
            foreach (System.Windows.Forms.Form window in System.Windows.Forms.Application.OpenForms.Cast<System.Windows.Forms.Form>().ToList())
            {
                if (window.IsDisposed)
                    continue;
                if (window is EditBlendSets blendSets)
                    blendSets.CloseDiscarding();
                else if (window is AnimTrees.AnimTreeEditor || window is EditAnimations || window is AnimationPreview)
                    window.Close();
            }
            AnimTrees.AnimTreeClipboard.Clear();
            AnimTreeLayoutManager.Reset();
            MCP.McpAnimationAssetTools.ForgetUnwrittenChanges();

            Global.Animations = loaded;
            AnimationPakWrite.Stamp(loaded);
            useLists();
            OnAnimationsModified?.Invoke();
            return true;
        }

        /* The lists of what ANIMATION.PAK holds - skeletons, sets and their clips, trees - by name. All are built first; the
           action returned puts them in place of the last ones, so nothing reading one ever sees it half-built, and nothing
           changes when the PAK can't be listed. Only the cheap part of the PAK is read for them (clip databases, trees and
           skeleton definitions): the clip sections wait until something needs them. */
        private static Action ListAnimations(Animation loaded)
        {
            AnimationStrings strings = loaded.Strings, debugStrings = loaded.StringsDebug;
            Dictionary<string, HashSet<string>> genderedSkeletons = new Dictionary<string, HashSet<string>>();
            List<string> allSkeletons = new List<string>();
            SortedDictionary<string, HashSet<string>> allAnimations = new SortedDictionary<string, HashSet<string>>();
            List<string> allAnimTrees = new List<string>();

            //Male/female skeletons, by the reference rig their definition names
            foreach (Animation.SkeletonDef def in loaded.SkeletonDefs.Values)
            {
                if (def.ReferenceSkeleton != "MALE" && def.ReferenceSkeleton != "FEMALENPC")
                    continue;
                if (!genderedSkeletons.TryGetValue(def.ReferenceSkeleton, out HashSet<string> skeletons))
                    genderedSkeletons.Add(def.ReferenceSkeleton, skeletons = new HashSet<string>());
                skeletons.Add(def.Name);
            }

            //Every skeleton a character can be given
            allSkeletons.AddRange(loaded.CompiledSkeletonDefs);
            allSkeletons.Sort();

            //Every anim set, with the clips it holds itself (not those only its contexts hold)
            foreach (AnimClipDB database in loaded.ClipDatabases)
            {
                if (string.IsNullOrEmpty(database.Character) || allAnimations.ContainsKey(database.Character))
                    continue;
                List<string> clips = database.Animations.Select(o => o.Name).Distinct().ToList();
                clips.Sort();
                allAnimations.Add(database.Character, new HashSet<string>(clips));
            }

            //Every anim tree set
            allAnimTrees.AddRange(loaded.Trees.Select(o => o.Set));
            allAnimTrees.Sort();

            /*
            //Load all animations by anim set (NOTE: no longer using this as the ID gives the filename, not the anim name, but keeping it for future reference)
            List<PAK2.File> streamedAnims = Global.Animations.PAK.Entries.FindAll(o => o.Filename.Length > 24 && o.Filename.Substring(0, 24) == "DATA\\ANIM_SYS\\STREAMED64");
            for (int i = 0; i < streamedAnims.Count; i++)
            {
                string[] filepathParts = Path.GetFileNameWithoutExtension(streamedAnims[i].Filename).Split('_');
                string animationName = AnimationStrings_Debug.Entries[Convert.ToUInt32(filepathParts[filepathParts.Length - 1])];
                string animSetName = "";
                using (BinaryReader reader = new BinaryReader(new MemoryStream(streamedAnims[i].Content)))
                {
                    reader.BaseStream.Position = 4;
                    animSetName = AnimationStrings_Debug.Entries[reader.ReadUInt32()];
                }
                if (animSetName == "FLOATMAN") continue; //NOTE: Skipping "FLOATMAN" as it seems to be the dialogue animations, which are just auto applied by Speech.
                HashSet<string> anims;
                if (!AllAnimations.TryGetValue(animSetName, out anims))
                {
                    anims = new HashSet<string>();
                    AllAnimations.Add(animSetName, anims);
                }
                anims.Add(Path.GetFileName(animationName).ToLower());
            }
            */

            return () =>
            {
                //Anim string dbs - the ones the animations already parsed, rather than another copy of them
                AnimationStrings = strings;
                AnimationStrings_Debug = debugStrings;
                GenderedSkeletons = genderedSkeletons;
                AllSkeletons = allSkeletons;
                AllAnimations = allAnimations;
                AllAnimTrees = allAnimTrees;
            };
        }
    }

}
