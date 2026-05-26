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
                Materials.Material fallbackMaterial = Editor?.CommandsDisplay?.Content?.Level?.Materials?.Entries?.FirstOrDefault(o => o.Name == "FALLBACK_MATERIAL");
                if (fallbackMaterial == null) fallbackMaterial = Editor?.CommandsDisplay?.Content?.Level?.Materials?.Entries?[0];
                return fallbackMaterial;
            }
        }

        //Settings keys
        public static SettingsStrings Settings = new SettingsStrings();
        public class SettingsStrings
        {
            public readonly string ConnectToLevelViewer = "CE_ConnectToUnity";
            public readonly string NodeOpt = "CS_NodeView";
            public readonly string ShowShortGuids = "CS_ShowEntityIDs";
            public readonly string InstOpt = "CS_InstanceMode";
            public readonly string CompNameOnlyOpt = "CS_SearchOnlyCompName";
            public readonly string UseEntityTabs = "CS_UseEntityTabs";
            public readonly string ShowSavedMsgOpt = "CS_ShowSavedNotif";
            public readonly string ShowTexOpt = "CS_ShowTextures";
            public readonly string FileBrowserViewOpt = "CS_FileBrowserView";
            public readonly string EnableFileBrowser = "CS_FileBrowserEnabled";
            public readonly string AutoHideCompositeDisplay = "CS_FileBrowserAutoHide";
            public readonly string KeepUsesWindowOpen = "CS_KeepUsesWindowOpen";
            public readonly string EntitySplitWidth = "CS_EntitySplitWidth";
            public readonly string CompositeSplitWidth = "CS_CompositeSplitWidth2";
            public readonly string CommandsSplitWidth = "CS_CommandsSplitWidth";
            public readonly string WindowWidth = "CS_WindowWidth";
            public readonly string WindowHeight = "CS_WindowHeight";
            public readonly string WindowState = "CS_WindowState";
            public readonly string NodegraphState = "CS_NodegraphState";
            public readonly string NodegraphWidth = "CS_NodegraphWidth";
            public readonly string NodegraphHeight = "CS_NodegraphHeight";
            public readonly string SplitWidthMainRight = "CS_SplitWidthMainRight";
            public readonly string SplitWidthMainBottom = "CS_SplitWidthMainBottom";
            public readonly string PreviouslySelectedFunctionType = "CS_PreviouslySelectedFunctionType";
            public readonly string PreviouslySearchedFunctionType = "CS_PreviouslySearchedFunctionType";
            public readonly string PreviouslySearchedParamPopulation = "CS_PreviouslySearchedParamPopulation";
            public readonly string PreviouslySelectedCompInstType = "CS_PreviouslySelectedCompInstType";
            public readonly string PreviouslySearchedCompInstType = "CS_PreviouslySearchedCompInstType";
            public readonly string PreviouslySearchedParamPopulationComp = "CS_PreviouslySearchedParamPopulationComp";
            public readonly string CompileInstances = "CS_CompileInstances";
            public readonly string PrevFuncUsesSearch = "CS_PrevFuncUsesSearch";
            public readonly string PrevVariableType = "CS_PrevVariableTypeNew";
            public readonly string PrevVariableType_Enum = "CS_PrevVariableTypeEnum";
            public readonly string PrevVariableType_EnumString = "CS_PrevVariableTypeEnumString";
            public readonly string CustomColours = "CS_CustomColours";
            public readonly string EntityListState = "CS_EntityListState";
            public readonly string EntityListWidth = "CS_EntityListWidth";
            public readonly string EntityInspectorState = "CS_EntityInspectorState";
            public readonly string EntityInspectorWidth = "CS_EntityInspectorWidth";
            public readonly string PreviouslySearchedParamPopulationProxyOrAlias = "CS_PreviouslySearchedParamPopulationProxyOrAlias";
            public readonly string UNITY_FocusEntity = "CS_UNITY_FocusEntity";
            public readonly string UNITY_ShowCameraPosition = "CS_UNITY_ShowCameraPosition";
            public readonly string UNITY_RenderWireframe = "CS_UNITY_RenderWireframe";
            public readonly string UNITY_HideNestedScriptEntities = "CS_UNITY_HideNestedScriptEntities";
            public readonly string UNITY_BoxRenderFilters = "CS_UNITY_BoxRenderFilters";
            public readonly string RuntimeUtilsOpt = "CE_ConnectToRuntimeUtils";
            public readonly string NumericStep = "CS_NumericStep";
            public readonly string NumericStepRot = "CS_NumericStepRot";
            public readonly string SavePakAndBin = "CS_SavePakAndBin";
            public readonly string PrevEntNameSearch = "CS_PrevEntNameSearch";
            public readonly string PopulateAllPinsOnCreateNode = "CS_PopulateAllPinsOnCreateNode";
            public readonly string OptionToDeleteEntityWithNode = "CS_OptionToDeleteEntityWithNode";
            public readonly string LaunchGameWhenSaved = "CS_LaunchGameWhenSaved";
            public readonly string NodeColour_FunctionNode = "CS_NodeColour_FunctionNode";
            public readonly string NodeColour_FunctionNodeBottom = "CS_NodeColour_FunctionNodeBottom";
            public readonly string NodeColour_FunctionText = "CS_NodeColour_FunctionText";
            public readonly string NodeColour_InstanceNode = "CS_NodeColour_InstanceNode";
            public readonly string NodeColour_InstanceNodeBottom = "CS_NodeColour_InstanceNodeBottom";
            public readonly string NodeColour_InstanceText = "CS_NodeColour_InstanceText";
            public readonly string NodeColour_AliasNode = "CS_NodeColour_AliasNode";
            public readonly string NodeColour_AliasNodeBottom = "CS_NodeColour_AliasNodeBottom";
            public readonly string NodeColour_AliasText = "CS_NodeColour_AliasText";
            public readonly string NodeColour_ProxyNode = "CS_NodeColour_ProxyNode";
            public readonly string NodeColour_ProxyNodeBottom = "CS_NodeColour_ProxyNodeBottom";
            public readonly string NodeColour_ProxyText = "CS_NodeColour_ProxyText";
            public readonly string NodeColour_VariableNode = "CS_NodeColour_VariableNode";
            public readonly string NodeColour_VariableText = "CS_NodeColour_VariableText";
            public readonly string AskBeforeDeletingNode = "CS_AskBeforeDeletingNode";
            public readonly string ShowGamePlatform = "CONFIG_ShowPlatform";
            public readonly string LastSelectedLevel = "OPT_LoadToMap";
            public readonly string RemoteBranch = "CONFIG_RemoteBranch";
            public readonly string GameRoot = "PATH_GameRoot";
            public readonly string UseStagingBranch = "CONFIG_UseStagingBranch";
            public readonly string SkipUpdate = "CONFIG_SkipUpdateCheck";
            public readonly string SaveCounter = "CS_SaveCounter";
            public readonly string EntityCounter = "CS_EntityCounter";
            public readonly string DidSteamPrompt = "CS_DidSteamPrompt";
            public readonly string ResetRenderFilters = "CS_ResetRenderFilters";
            public readonly string LevelViewerEnabled = "CONFIG_LevelViewerEnabled";
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
