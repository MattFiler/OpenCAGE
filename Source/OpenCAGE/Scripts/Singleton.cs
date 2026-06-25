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
using System.Xml;

namespace OpenCAGE
{
    public static class Singleton
    {
        public static CommandsEditor Editor;

        //Metadata
        public static string PathToAI = "";
        public static PatchManager.Platform Platform = PatchManager.Platform.UNKNOWN;
        public static bool IsSteamworks = false;
        public static string Version = "";

        //Set via the "-disable_viewport" launch argument to hard-disable the level viewer even if it is installed
        public static bool DisableViewport = false;

        //Global localised string DBs for English
        public static Dictionary<string, TextDB> GlobalTextDBs = new Dictionary<string, TextDB>();

        //Animation content from ANIMATIONS.PAK
        public static List<string> AllSkeletons = new List<string>();
        public static List<string> AllAnimTrees = new List<string>();
        public static SortedDictionary<string, HashSet<string>> AllAnimations = new SortedDictionary<string, HashSet<string>>(); //Anim Set, Animations
        public static Dictionary<string, HashSet<string>> GenderedSkeletons = new Dictionary<string, HashSet<string>>(); //Gender, Skeletons

        //Global animation strings
        public static AnimationStrings AnimationStrings;
        public static AnimationStrings AnimationStrings_Debug;

        //Global assets
        public static Global Global;

        //Load events
        public static Action<LevelContent> OnLevelLoaded;
        public static Action<LevelContent> OnLevelAssetsLoaded;

        //Reload events
        public static Action<Entity> OnEntityReloaded;
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
        public static Action OnSaved;
        public static Action OnParameterModified;
        public static Action OnResourceModified;
        public static Action OnNodeStyleChanged;
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

            //Load animation data
            PAK2 animPAK = new PAK2(Singleton.PathToAI + "/DATA/GLOBAL/ANIMATION.PAK");

            //Create global
            Global = new Global(Singleton.PathToAI + "\\DATA\\ENV\\GLOBAL\\", animPAK);

            //Load all male/female skeletons
            List<PAK2.File> skeletonDefs = animPAK.Entries.FindAll(o => o.Filename.Length > 17 && o.Filename.Substring(0, 17) == "DATA\\SKELETONDEFS");
            for (int i = 0; i < skeletonDefs.Count; i++)
            {
                string skeleton = Path.GetFileNameWithoutExtension(skeletonDefs[i].Filename);
                XmlNode skeletonType = new BML(skeletonDefs[i].Content).Content.SelectSingleNode("//SkeletonDef/LoResReferenceSkeleton");
                if (skeletonType?.InnerText == "MALE" || skeletonType?.InnerText == "FEMALENPC")
                {
                    if (!GenderedSkeletons.ContainsKey(skeletonType?.InnerText))
                        GenderedSkeletons.Add(skeletonType?.InnerText, new HashSet<string>());
                    GenderedSkeletons[skeletonType?.InnerText].Add(skeleton);
                }
                File.Delete(skeleton);
            }

            //Anim string dbs
            AnimationStrings = new AnimationStrings(animPAK.Entries.FirstOrDefault(o => o.Filename.Contains("ANIM_STRING_DB.BIN")).Content);
            AnimationStrings_Debug = new AnimationStrings(animPAK.Entries.FirstOrDefault(o => o.Filename.Contains("ANIM_STRING_DB_DEBUG.BIN")).Content);

            //Load all skeleton names
            List<PAK2.File> skeletonNames = animPAK.Entries.FindAll(o => o.Filename.Length > 24 && o.Filename.Substring(0, 24) == "DATA\\ANIM_SYS\\SKELE\\DEFS");
            for (int i = 0; i < skeletonNames.Count; i++)
                AllSkeletons.Add(AnimationStrings_Debug.Entries[Convert.ToUInt32(Path.GetFileNameWithoutExtension(skeletonNames[i].Filename))]);
            AllSkeletons.Sort();

            //Load all anim sets
            List<PAK2.File> animClipDbs = animPAK.Entries.FindAll(o => { string path = Path.GetFileName(o.Filename); if (path.Length < ("_ANIM_CLIP_DB.BIN").Length) return false; return path.Substring(path.Length - ("_ANIM_CLIP_DB.BIN").Length) == "_ANIM_CLIP_DB.BIN"; });
            for (int i = 0; i < animClipDbs.Count; i++)
            {
                uint animSetID = Convert.ToUInt32(Path.GetFileName(animClipDbs[i].Filename).Split('_')[0]);
                string animSet = AnimationStrings_Debug.Entries[animSetID];
                HashSet<string> animations = new HashSet<string>();
                using (BinaryReader reader = new BinaryReader(new MemoryStream(animClipDbs[i].Content)))
                {
                    //This fixes a weird thing where there's an unknown variable offset at the start
                    int offset = 4;
                    while (true)
                    {
                        reader.BaseStream.Position = offset;
                        if (reader.ReadUInt32() == animSetID)
                            break;
                        offset += 1;
                    }
                    reader.BaseStream.Position += 4;

                    int countAnimNames = reader.ReadInt32();
                    int countAnimFileNames = reader.ReadInt32();
                    for (int x = 0; x < countAnimNames; x++)
                    {
                        animations.Add(AnimationStrings_Debug.Entries[reader.ReadUInt32()]);
                        reader.BaseStream.Position += 4;
                    }

                    //TODO: There's more info here. Useful?
                }
                AllAnimations.Add(animSet, animations);
            }
            foreach (KeyValuePair<string, HashSet<string>> anims in AllAnimations)
            {
                List<string> animList = anims.Value.ToList();
                animList.Sort();
                anims.Value.Clear();
                foreach (string anim in animList)
                    anims.Value.Add(anim);
            }

            //Load all anim trees
            List<PAK2.File> animTreeDbs = animPAK.Entries.FindAll(o => { string path = Path.GetFileName(o.Filename); if (path.Length < ("_ANIM_TREE_DB.BIN").Length) return false; return path.Substring(path.Length - ("_ANIM_TREE_DB.BIN").Length) == "_ANIM_TREE_DB.BIN"; });
            for (int i = 0; i < animTreeDbs.Count; i++)
                AllAnimTrees.Add(AnimationStrings_Debug.Entries[Convert.ToUInt32(Path.GetFileName(animTreeDbs[i].Filename).Split('_')[0])]);
            AllAnimTrees.Sort();

            /*
            //Load all animations by anim set (NOTE: no longer using this as the ID gives the filename, not the anim name, but keeping it for future reference)
            List<PAK2.File> streamedAnims = animPAK.Entries.FindAll(o => o.Filename.Length > 24 && o.Filename.Substring(0, 24) == "DATA\\ANIM_SYS\\STREAMED64");
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

            _loadedAnimationContent = true;
            OnAnimationsLoaded?.Invoke();
            _loadedGlobalAssets = true;
            OnGlobalAssetsLoaded?.Invoke();
        }
    }

    public static class EditorClipboard
    {
        public static Entity Entity = null;
    }
}
