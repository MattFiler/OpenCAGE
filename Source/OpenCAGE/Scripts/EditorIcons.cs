using OpenCAGE.Theming;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// The editor's icons for entities, composites, folders and the composite commands - in the order their art is
    /// stored, so never reorder them, only add to the end.
    /// </summary>
    public enum EditorIcon
    {
        Parameter,
        Function,
        CompositeInstance,
        Proxy,
        Alias,
        PinReference,
        PinMethod,
        PinTarget,
        PinInput,
        PinOutput,
        Folder,
        FolderOpen,
        File,
        Composite,
        RootComposite,
        SystemComposite,
        DisplayModel,
        CreateCompositeFromSelected,
        DeinstanceComposite,
        CompositeVariant,
        DuplicateComposite,
        ArrangePage,
        ArrangeSelected,

        //The entity categories (ENTITY_CATEGORIES in flowgraphs.dat), in the generator's order. A category's icon is found
        //by its name: "Characters/NPC_Specific" is CategoryCharactersNPCSpecific (see EditorIcons.ForCategory)
        CategoryAnimation,
        CategoryAudio,
        CategoryCamera,
        CategoryCameraBehavior,
        CategoryCameraUtils,
        CategoryCharactersCommands,
        CategoryCharactersLocomotion,
        CategoryCharactersMarkup,
        CategoryCharactersMonitors,
        CategoryCharactersNPCSpecific,
        CategoryCharactersProperties,
        CategoryCharactersSenses,
        CategoryCombatAlliances,
        CategoryCombatFiring,
        CategoryCombatTarget,
        CategoryCombatWeapons,
        CategoryCover,
        CategoryDebug,
        CategoryFilters,
        CategoryGameEvents,
        CategoryInput,
        CategoryInternal,
        CategoryInterrogation,
        CategoryInventory,
        CategoryLighting,
        CategoryLogic,
        CategoryMathsBoolean,
        CategoryMathsFloat,
        CategoryMathsInteger,
        CategoryMathsPosition,
        CategoryMathsVector,
        CategoryNetwork,
        CategoryPathfinding,
        CategoryPhysics,
        CategoryPlatform,
        CategoryPostProcess,
        CategoryResources,
        CategorySpace,
        CategorySplines,
        CategoryTraversals,
        CategoryTriggers,
        CategoryUI,
        CategoryVariables,
        CategoryVariablesSet,
        CategoryZoning,

        //Bespoke icons for the most used and most important function types, in the generator's order (Art.Entities): an
        //entity of one of those types shows its own icon rather than its category's (see EditorIcons.TypeIcons)
        EntityModel,
        EntitySoundNetwork,
        EntityParticles,
        EntityRadiosity,
        EntityDelay,
        EntitySequence,
        EntityMarker,
        EntityPlayerTrigger,
        EntityGate,
        EntityZoneLink,
        EntityZoneExclusion,
        EntityMusic,
        EntitySpeech,
        EntityFog,
        EntityBarrier,
        EntityPlayer,
        EntityReflection,
        EntityInteract,
        EntityTerminal,
        EntityMaster,
        EntityCharacter,
        EntityNot,
        EntityOnce,
        EntitySwitch,
        EntityAll,
        EntityCounter,
        EntityPressurePad,
    }

    /// <summary>
    /// Where every list, tree and menu gets its entity and composite icons. The art is drawn for each theme at a few sizes
    /// (Resources\Icons: one strip of every icon per size, a light and a dark one), and a size in between is resampled
    /// down from the next size up, so an icon is never stretched. The image lists are shared, each in the order its kind
    /// of list has always indexed, and are redrawn in place when the theme changes: nothing that shows them needs to know
    /// which theme is on.
    /// </summary>
    public static class EditorIcons
    {
        private static readonly int[] _artSizes = { 16, 32, 48, 64, 128, 256 };
        private static readonly Dictionary<string, Bitmap> _strips = new Dictionary<string, Bitmap>();

        /// <summary>The entity lists' order: what <see cref="EditorUtils.GetIndexesForListViewItem"/> and the pin type icons index, with the palette's category folders after.</summary>
        public static readonly EditorIcon[] EntityOrder = new EditorIcon[]
        {
            EditorIcon.Parameter, EditorIcon.Function, EditorIcon.CompositeInstance, EditorIcon.Proxy, EditorIcon.Alias,
            EditorIcon.PinReference, EditorIcon.PinMethod, EditorIcon.PinTarget, EditorIcon.PinInput, EditorIcon.PinOutput,
            EditorIcon.Folder, EditorIcon.FolderOpen,
        }.Concat(CategoryIcons).Concat(FunctionTypeIcons).ToArray();

        /// <summary>Every entity category's icon, in their order (after the folders in <see cref="EntityOrder"/>).</summary>
        public static EditorIcon[] CategoryIcons => Enum.GetValues(typeof(EditorIcon)).Cast<EditorIcon>().Where(o => o.ToString().StartsWith("Category")).ToArray();

        /// <summary>Every bespoke function type icon, in their order (after the categories in <see cref="EntityOrder"/>).</summary>
        public static EditorIcon[] FunctionTypeIcons => Enum.GetValues(typeof(EditorIcon)).Cast<EditorIcon>().Where(o => o.ToString().StartsWith("Entity")).ToArray();

        /// <summary>
        /// Function types that show an icon of their own rather than their category's: the most used in the shipped scripts
        /// (ModelReference alone is a fifth of all their entities) and the most important, where the category's icon says
        /// little about them - Resources' database for models, lights, particles and markers alike - or where the type has no
        /// category at all and would show the plain braces. Some take another category's icon that fits them better.
        /// </summary>
        public static readonly IReadOnlyDictionary<CATHODE.Scripting.FunctionType, EditorIcon> TypeIcons = BuildTypeIcons();

        private static Dictionary<CATHODE.Scripting.FunctionType, EditorIcon> BuildTypeIcons()
        {
            Dictionary<CATHODE.Scripting.FunctionType, EditorIcon> icons = new Dictionary<CATHODE.Scripting.FunctionType, EditorIcon>();
            void Map(EditorIcon icon, params CATHODE.Scripting.FunctionType[] types)
            {
                foreach (CATHODE.Scripting.FunctionType type in types)
                    icons[type] = icon;
            }

            //Their own icons, most used first
            Map(EditorIcon.EntityModel, CATHODE.Scripting.FunctionType.ModelReference, CATHODE.Scripting.FunctionType.EnvironmentModelReference);
            Map(EditorIcon.EntitySoundNetwork, CATHODE.Scripting.FunctionType.SoundNetworkNode);
            Map(EditorIcon.EntityParticles, CATHODE.Scripting.FunctionType.ParticleEmitterReference, CATHODE.Scripting.FunctionType.GPU_PFXEmitterReference, CATHODE.Scripting.FunctionType.RibbonEmitterReference);
            Map(EditorIcon.EntityRadiosity, CATHODE.Scripting.FunctionType.RadiosityProxy);
            Map(EditorIcon.EntityDelay, CATHODE.Scripting.FunctionType.LogicDelay, CATHODE.Scripting.FunctionType.TriggerDelay);
            Map(EditorIcon.EntitySequence, CATHODE.Scripting.FunctionType.TriggerSequence, CATHODE.Scripting.FunctionType.TriggerRandomSequence);
            Map(EditorIcon.EntityMarker, CATHODE.Scripting.FunctionType.PositionMarker);
            Map(EditorIcon.EntityPlayerTrigger, CATHODE.Scripting.FunctionType.PlayerTriggerBox, CATHODE.Scripting.FunctionType.PlayerUseTriggerBox);
            Map(EditorIcon.EntityGate, CATHODE.Scripting.FunctionType.LogicGate, CATHODE.Scripting.FunctionType.LogicGateAnd, CATHODE.Scripting.FunctionType.LogicGateOr,
                CATHODE.Scripting.FunctionType.LogicGateEquals, CATHODE.Scripting.FunctionType.LogicGateNotEqual, CATHODE.Scripting.FunctionType.Logic_MultiGate);
            Map(EditorIcon.EntityZoneLink, CATHODE.Scripting.FunctionType.ZoneLink);
            Map(EditorIcon.EntityZoneExclusion, CATHODE.Scripting.FunctionType.ZoneExclusionLink);
            Map(EditorIcon.EntityMusic, CATHODE.Scripting.FunctionType.MusicTrigger, CATHODE.Scripting.FunctionType.MusicController);
            Map(EditorIcon.EntitySpeech, CATHODE.Scripting.FunctionType.Speech, CATHODE.Scripting.FunctionType.SpeechScript, CATHODE.Scripting.FunctionType.Convo,
                CATHODE.Scripting.FunctionType.CHR_PlayNPCBark, CATHODE.Scripting.FunctionType.NPC_DynamicDialogue);
            Map(EditorIcon.EntityFog, CATHODE.Scripting.FunctionType.FogSphere, CATHODE.Scripting.FunctionType.FogPlane, CATHODE.Scripting.FunctionType.FogBox);
            Map(EditorIcon.EntityBarrier, CATHODE.Scripting.FunctionType.CollisionBarrier);
            Map(EditorIcon.EntityPlayer, CATHODE.Scripting.FunctionType.VariableThePlayer);
            Map(EditorIcon.EntityReflection, CATHODE.Scripting.FunctionType.EnvironmentMap);
            Map(EditorIcon.EntityInteract, CATHODE.Scripting.FunctionType.UiSelectionBox, CATHODE.Scripting.FunctionType.Interaction);
            Map(EditorIcon.EntityTerminal, CATHODE.Scripting.FunctionType.TerminalFolder, CATHODE.Scripting.FunctionType.TerminalContent);
            Map(EditorIcon.EntityMaster, CATHODE.Scripting.FunctionType.Master);
            Map(EditorIcon.EntityCharacter, CATHODE.Scripting.FunctionType.Character);
            Map(EditorIcon.EntityNot, CATHODE.Scripting.FunctionType.LogicNot);
            Map(EditorIcon.EntityOnce, CATHODE.Scripting.FunctionType.LogicOnce);
            Map(EditorIcon.EntitySwitch, CATHODE.Scripting.FunctionType.LogicSwitch);
            Map(EditorIcon.EntityAll, CATHODE.Scripting.FunctionType.LogicAll);
            Map(EditorIcon.EntityCounter, CATHODE.Scripting.FunctionType.LogicCounter, CATHODE.Scripting.FunctionType.Counter);
            Map(EditorIcon.EntityPressurePad, CATHODE.Scripting.FunctionType.LogicPressurePad);

            //Another category's icon, where it fits better than their own category's (or they have none)
            Map(EditorIcon.CategoryLighting, CATHODE.Scripting.FunctionType.LightReference, CATHODE.Scripting.FunctionType.Torch_Control, CATHODE.Scripting.FunctionType.PlayerTorch);
            Map(EditorIcon.CategoryInventory, CATHODE.Scripting.FunctionType.GCIP_WorldPickup, CATHODE.Scripting.FunctionType.CollectSevastopolLog,
                CATHODE.Scripting.FunctionType.SetBlueprintInfo, CATHODE.Scripting.FunctionType.GetBlueprintAvailable, CATHODE.Scripting.FunctionType.GetBlueprintLevel);
            Map(EditorIcon.CategoryPhysics, CATHODE.Scripting.FunctionType.PhysicsSystem);
            Map(EditorIcon.CategorySplines, CATHODE.Scripting.FunctionType.SplinePath, CATHODE.Scripting.FunctionType.GetClosestPointOnSpline);
            Map(EditorIcon.CategoryCharactersNPCSpecific, CATHODE.Scripting.FunctionType.NPC_AreaBox, CATHODE.Scripting.FunctionType.NPC_SetSafePoint, CATHODE.Scripting.FunctionType.CHR_SetFacehuggerAggroRadius);
            Map(EditorIcon.CategoryCharactersMonitors, CATHODE.Scripting.FunctionType.CHR_IsWithinRange);
            Map(EditorIcon.CategoryCharactersLocomotion, CATHODE.Scripting.FunctionType.CHR_DeepCrouch);
            Map(EditorIcon.CategoryCharactersCommands, CATHODE.Scripting.FunctionType.DespawnCharacter);
            Map(EditorIcon.CategoryTriggers, CATHODE.Scripting.FunctionType.TriggerSimple, CATHODE.Scripting.FunctionType.TriggerBindAllCharactersOfType, CATHODE.Scripting.FunctionType.TriggerSelect_Direct,
                CATHODE.Scripting.FunctionType.ProximityDetector, CATHODE.Scripting.FunctionType.Player_ExploitableArea, CATHODE.Scripting.FunctionType.Player_Sensor, CATHODE.Scripting.FunctionType.AreaHitMonitor);
            Map(EditorIcon.CategoryGameEvents, CATHODE.Scripting.FunctionType.GlobalEvent, CATHODE.Scripting.FunctionType.GlobalEventMonitor, CATHODE.Scripting.FunctionType.CheckpointRestoredNotify,
                CATHODE.Scripting.FunctionType.LevelInfo, CATHODE.Scripting.FunctionType.SetAsActiveMissionLevel, CATHODE.Scripting.FunctionType.MissionNumber);
            Map(EditorIcon.CategoryUI, CATHODE.Scripting.FunctionType.GetFlashIntValue, CATHODE.Scripting.FunctionType.GetFlashFloatValue, CATHODE.Scripting.FunctionType.ButtonMashPrompt,
                CATHODE.Scripting.FunctionType.AddExitObjective, CATHODE.Scripting.FunctionType.MotionTrackerPing, CATHODE.Scripting.FunctionType.RTT_MoviePlayer);
            Map(EditorIcon.CategoryCombatWeapons, CATHODE.Scripting.FunctionType.WEAPON_GiveToPlayer, CATHODE.Scripting.FunctionType.WEAPON_MultiFilter);
            Map(EditorIcon.CategoryZoning, CATHODE.Scripting.FunctionType.ZoneLoaded, CATHODE.Scripting.FunctionType.FlushZoneCache);
            Map(EditorIcon.CategoryAnimation, CATHODE.Scripting.FunctionType.AnimationMask, CATHODE.Scripting.FunctionType.MultipleCharacterAttachmentNode,
                CATHODE.Scripting.FunctionType.SmoothMove, CATHODE.Scripting.FunctionType.ApplyRelativeTransform);
            Map(EditorIcon.CategoryMathsPosition, CATHODE.Scripting.FunctionType.Raycast, CATHODE.Scripting.FunctionType.GetClosestPoint, CATHODE.Scripting.FunctionType.PointTracker);
            Map(EditorIcon.CategoryLogic, CATHODE.Scripting.FunctionType.RandomSelect, CATHODE.Scripting.FunctionType.ToggleFunctionality);
            Map(EditorIcon.CategoryPostProcess, CATHODE.Scripting.FunctionType.Refraction);
            Map(EditorIcon.CategoryDebug, CATHODE.Scripting.FunctionType.DEBUG_SenseLevels);
            Map(EditorIcon.CategoryPlatform, CATHODE.Scripting.FunctionType.SetRichPresence, CATHODE.Scripting.FunctionType.GameDVR, CATHODE.Scripting.FunctionType.IsPlaylistTypeMarathon,
                CATHODE.Scripting.FunctionType.IsPlaylistTypeAll, CATHODE.Scripting.FunctionType.GetCurrentPlaylistLevelIndex);
            return icons;
        }

        /// <summary>The composite trees' order (<see cref="TreeUtility"/> and the pickers): folder, composite, open folder, root, GLOBAL/PAUSEMENU, DisplayModel.</summary>
        public static readonly EditorIcon[] CompositeTreeOrder =
        {
            EditorIcon.Folder, EditorIcon.Composite, EditorIcon.FolderOpen, EditorIcon.RootComposite, EditorIcon.SystemComposite, EditorIcon.DisplayModel,
        };

        /// <summary>The composite browser's flat list: composite, folder, root, GLOBAL/PAUSEMENU, DisplayModel.</summary>
        public static readonly EditorIcon[] CompositeTileOrder =
        {
            EditorIcon.Composite, EditorIcon.Folder, EditorIcon.RootComposite, EditorIcon.SystemComposite, EditorIcon.DisplayModel,
        };

        /// <summary>The asset trees' order (<see cref="TreeItemIcon"/>): folder, file, open folder.</summary>
        public static readonly EditorIcon[] FileTreeOrder = { EditorIcon.Folder, EditorIcon.File, EditorIcon.FolderOpen };

        /// <summary>The pin pickers' order: reference, method, target, input, output.</summary>
        public static readonly EditorIcon[] PinOrder = { EditorIcon.PinReference, EditorIcon.PinMethod, EditorIcon.PinTarget, EditorIcon.PinInput, EditorIcon.PinOutput };

        private sealed class SharedList
        {
            public ImageList List;
            public EditorIcon[] Order;
            public int Size;
        }
        private static readonly List<SharedList> _lists = new List<SharedList>();
        private static readonly Dictionary<Component, Action> _bound = new Dictionary<Component, Action>();

        /// <summary>The theme changed and every shared list and bound menu item has its new icons. For anything that copied icons out.</summary>
        public static event Action Changed;

        static EditorIcons()
        {
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public static ImageList EntityList => Shared(EntityOrder, 16);
        public static ImageList CompositeTree => Shared(CompositeTreeOrder, 16);
        public static ImageList FileTree => Shared(FileTreeOrder, 16);
        public static ImageList Pins => Shared(PinOrder, 16);

        /// <summary>The composite browser's flat list icons at a size of its own (its large and small views).</summary>
        public static ImageList CompositeTiles(int size) => Shared(CompositeTileOrder, size);

        /// <summary>
        /// Show one of the shared lists above in a ListView (null leaves that one as it is). Use this rather than setting
        /// SmallImageList/LargeImageList: .NET Framework's ListView.Dispose lets go of an image list's Disposed event but
        /// not its RecreateHandle one (nor the large list's ChangeHandle), so a list that outlives the view keeps it alive
        /// for good - with its items, their tags and the window it was in (each level's browser panels, every search
        /// window). The view is unhooked here once it is disposed. A TreeView unhooks itself.
        /// </summary>
        public static void ShowIn(ListView view, ImageList small, ImageList large = null)
        {
            if (view == null)
                return;
            if (small != null)
                view.SmallImageList = small;
            if (large != null)
                view.LargeImageList = large;

            if (!_shownIn.TryGetValue(view, out List<ImageList> lists))
            {
                lists = new List<ImageList>();
                _shownIn.Add(view, lists);
                view.Disposed += UnhookDisposedView;
            }
            if (small != null && !lists.Contains(small)) lists.Add(small);
            if (large != null && !lists.Contains(large)) lists.Add(large);
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ListView, List<ImageList>> _shownIn = new System.Runtime.CompilerServices.ConditionalWeakTable<ListView, List<ImageList>>();

        //The handlers a ListView's image list setters hook (private to ListView, so made from their names): RecreateHandle
        //for all three lists, and the large list's internal ChangeHandle too
        private static readonly MethodInfo[] _listViewRecreateHandlers = ListViewHandlers("SmallImageListRecreateHandle", "LargeImageListRecreateHandle", "StateImageListRecreateHandle");
        private static readonly MethodInfo[] _listViewChangeHandlers = ListViewHandlers("LargeImageListChangedHandle");
        private static readonly MethodInfo _removeChangeHandle = typeof(ImageList).GetEvent("ChangeHandle", BindingFlags.Instance | BindingFlags.NonPublic)?.GetRemoveMethod(true);

        private static MethodInfo[] ListViewHandlers(params string[] names)
        {
            return names.Select(name => typeof(ListView).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(object), typeof(EventArgs) }, null))
                .Where(method => method != null).ToArray();
        }

        private static void UnhookDisposedView(object sender, EventArgs e)
        {
            ListView view = sender as ListView;
            if (view == null)
                return;
            view.Disposed -= UnhookDisposedView;
            if (!_shownIn.TryGetValue(view, out List<ImageList> lists))
                return;
            _shownIn.Remove(view);
            foreach (ImageList list in lists)
            {
                foreach (MethodInfo handler in _listViewRecreateHandlers)
                    list.RecreateHandle -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), view, handler);
                if (_removeChangeHandle != null)
                    foreach (MethodInfo handler in _listViewChangeHandlers)
                        _removeChangeHandle.Invoke(list, new object[] { Delegate.CreateDelegate(typeof(EventHandler), view, handler) });
            }
        }

        /// <summary>The icons a shared list holds, in order; null for any other list.</summary>
        public static EditorIcon[] OrderOf(ImageList list)
        {
            foreach (SharedList shared in _lists)
                if (shared.List == list)
                    return shared.Order;
            return null;
        }

        /// <summary>
        /// The icon of an entity category ("Characters/Senses"), or of the nearest category above it that has one; null for
        /// none ("Misc", or a category the art doesn't have yet) - those keep the plain function braces.
        /// </summary>
        public static EditorIcon? ForCategory(string category)
        {
            if (string.IsNullOrEmpty(category))
                return null;
            lock (_categoryIcons)
            {
                if (_categoryIcons.TryGetValue(category, out EditorIcon? known))
                    return known;
                EditorIcon? found = null;
                for (string at = category; found == null && !string.IsNullOrEmpty(at); at = at.Contains("/") ? at.Substring(0, at.LastIndexOf('/')) : null)
                {
                    string key = "Category" + new string(at.Where(char.IsLetterOrDigit).ToArray());
                    foreach (EditorIcon icon in CategoryIcons)
                    {
                        if (string.Equals(icon.ToString(), key, StringComparison.OrdinalIgnoreCase))
                        {
                            found = icon;
                            break;
                        }
                    }
                }
                _categoryIcons[category] = found;
                return found;
            }
        }
        private static readonly Dictionary<string, EditorIcon?> _categoryIcons = new Dictionary<string, EditorIcon?>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// A function type's icon: its own where it has one (<see cref="TypeIcons"/>), else its category's (see
        /// <see cref="ForCategory"/>), else the function braces. Cached: the categories are read once, from the shipped tables.
        /// </summary>
        public static EditorIcon ForFunctionType(CATHODE.Scripting.FunctionType function)
        {
            if (TypeIcons.TryGetValue(function, out EditorIcon own))
                return own;
            lock (_functionIcons)
            {
                if (_functionIcons.TryGetValue(function, out EditorIcon icon))
                    return icon;
                icon = ForCategory(FlowgraphLayoutManager.TryGetCategoryForFunctionType(function)) ?? EditorIcon.Function;
                _functionIcons[function] = icon;
                return icon;
            }
        }
        private static readonly Dictionary<CATHODE.Scripting.FunctionType, EditorIcon> _functionIcons = new Dictionary<CATHODE.Scripting.FunctionType, EditorIcon>();

        /// <summary>Where an icon sits in <see cref="EntityList"/> (-1 if it isn't one of its icons).</summary>
        public static int EntityIndex(EditorIcon icon)
        {
            return Array.IndexOf(EntityOrder, icon);
        }

        /// <summary>A function type's icon's place in <see cref="EntityList"/>: its category's, or the function braces'.</summary>
        public static int EntityIndex(CATHODE.Scripting.FunctionType function)
        {
            int index = EntityIndex(ForFunctionType(function));
            return index < 0 ? EntityIndex(EditorIcon.Function) : index;
        }

        /// <summary>
        /// An icon in one colour (every opaque pixel of it), for drawing on a coloured ground - a zoomed-out flowgraph node's.
        /// Made once per icon, size and colour and kept, so it can be drawn every paint: never dispose it.
        /// </summary>
        public static Bitmap GetSilhouette(EditorIcon icon, int size, Color colour)
        {
            long key = ((long)icon << 40) | ((long)size << 32) | (uint)colour.ToArgb();
            lock (_silhouettes)
            {
                if (_silhouettes.TryGetValue(key, out Bitmap made))
                    return made;
                //The light art: its shapes are the same in both themes, and only its alpha is kept
                Bitmap result = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
                using (Bitmap art = Get(icon, size, false))
                using (Graphics g = Graphics.FromImage(result))
                using (ImageAttributes tint = new ImageAttributes())
                {
                    tint.SetColorMatrix(new ColorMatrix(new float[][]
                    {
                        new float[] { 0, 0, 0, 0, 0 },
                        new float[] { 0, 0, 0, 0, 0 },
                        new float[] { 0, 0, 0, 0, 0 },
                        new float[] { 0, 0, 0, colour.A / 255f, 0 },
                        new float[] { colour.R / 255f, colour.G / 255f, colour.B / 255f, 0, 1 },
                    }));
                    g.Clear(Color.Transparent);
                    g.DrawImage(art, new Rectangle(0, 0, size, size), 0, 0, size, size, GraphicsUnit.Pixel, tint);
                }
                _silhouettes[key] = result;
                return result;
            }
        }
        private static readonly Dictionary<long, Bitmap> _silhouettes = new Dictionary<long, Bitmap>();

        /// <summary>
        /// An icon in its own colours, in one theme's art, for drawing every paint (a zoomed-out flowgraph node's on a light
        /// ground). Made once per icon, size and theme and kept: never dispose it.
        /// </summary>
        public static Bitmap GetKept(EditorIcon icon, int size, bool dark)
        {
            long key = ((long)icon << 40) | ((long)size << 32) | (dark ? 1L : 0L);
            lock (_kept)
            {
                if (_kept.TryGetValue(key, out Bitmap made))
                    return made;
                //Premultiplied: GDI+ draws those much faster, and these are drawn for every node on screen
                Bitmap result = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
                using (Bitmap art = Get(icon, size, dark))
                using (Graphics g = Graphics.FromImage(result))
                {
                    g.Clear(Color.Transparent);
                    g.DrawImageUnscaled(art, 0, 0);
                }
                _kept[key] = result;
                return result;
            }
        }
        private static readonly Dictionary<long, Bitmap> _kept = new Dictionary<long, Bitmap>();

        /// <summary>
        /// The colour to draw on a ground of the given colour so it stands out, but stays of a piece with it: the ground's own
        /// colour deepened (on a light ground) or paled (on a dark one).
        /// </summary>
        public static Color InkFor(Color ground)
        {
            lock (_inks)
            {
                if (_inks.TryGetValue(ground.ToArgb(), out Color known))
                    return known;
                Color ink = MakeInk(ground);
                _inks[ground.ToArgb()] = ink;
                return ink;
            }
        }
        private static readonly Dictionary<int, Color> _inks = new Dictionary<int, Color>();

        private static Color MakeInk(Color ground)
        {
            bool light = Luminance(ground) > 0.36;
            Color toward = light ? Color.Black : Color.White;
            for (int step = 15; step <= 50; step++)
            {
                double t = step / 50.0;
                Color c = Color.FromArgb(255, (int)Math.Round(ground.R + (toward.R - ground.R) * t), (int)Math.Round(ground.G + (toward.G - ground.G) * t), (int)Math.Round(ground.B + (toward.B - ground.B) * t));
                if (Contrast(c, ground) >= 4.5)
                    return c;
            }
            return toward;
        }

        private static double Luminance(Color c)
        {
            Func<int, double> lin = v => { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); };
            return 0.2126 * lin(c.R) + 0.7152 * lin(c.G) + 0.0722 * lin(c.B);
        }

        private static double Contrast(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        /// <summary>An icon at a size, for the current theme. The bitmap is the caller's.</summary>
        public static Bitmap Get(EditorIcon icon, int size) => Get(icon, size, ThemeManager.IsDark);

        /// <summary>An icon at a size, in a theme's art whatever the theme is. The bitmap is the caller's.</summary>
        public static Bitmap Get(EditorIcon icon, int size, bool dark)
        {
            size = Math.Max(1, size);
            int art = _artSizes[_artSizes.Length - 1];
            foreach (int candidate in _artSizes)
            {
                if (candidate >= size)
                {
                    art = candidate;
                    break;
                }
            }

            Bitmap result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            Bitmap strip = Strip(art, dark);
            if (strip == null || ((int)icon + 1) * art > strip.Width)
                return result;

            Rectangle cell = new Rectangle((int)icon * art, 0, art, art);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.Clear(Color.Transparent);
                if (art == size)
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.DrawImage(strip, new Rectangle(0, 0, size, size), cell, GraphicsUnit.Pixel);
                }
                else
                {
                    //Out of the strip first, so the filter can't reach into the icons either side
                    using (Bitmap source = strip.Clone(cell, PixelFormat.Format32bppArgb))
                    using (ImageAttributes edges = new ImageAttributes())
                    {
                        edges.SetWrapMode(WrapMode.TileFlipXY);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.DrawImage(source, new Rectangle(0, 0, size, size), 0, 0, art, art, GraphicsUnit.Pixel, edges);
                    }
                }
            }
            return result;
        }

        /// <summary>An icon in the middle of a larger square (a preview's), at the size that sits well among previews.</summary>
        public static Bitmap GetInSquare(EditorIcon icon, int square)
        {
            int size = square <= 32 ? square : (int)Math.Round(square * 0.75);
            Bitmap result = new Bitmap(square, square, PixelFormat.Format32bppArgb);
            using (Bitmap art = Get(icon, size))
            using (Graphics g = Graphics.FromImage(result))
            {
                g.Clear(Color.Transparent);
                g.DrawImageUnscaled(art, (square - size) / 2, (square - size) / 2);
            }
            return result;
        }

        /// <summary>Give a menu item an icon that follows the theme.</summary>
        public static void Bind(ToolStripItem item, EditorIcon icon)
        {
            Bind(item, () =>
            {
                Image old = item.Image;
                item.Image = Get(icon, 16);
                old?.Dispose();
            });
        }

        /// <summary>Give a button an icon that follows the theme, sized to sit inside it.</summary>
        public static void Bind(ButtonBase button, EditorIcon icon)
        {
            Bind(button, () =>
            {
                int width = button.ClientSize.Width > 0 ? button.ClientSize.Width : button.Width;
                int height = button.ClientSize.Height > 0 ? button.ClientSize.Height : button.Height;
                Image old = button.Image;
                button.Image = Get(icon, Math.Max(12, Math.Min(width, height) - 4));
                old?.Dispose();
            });
        }

        /// <summary>Give a window (a dock panel's tab) an icon that follows the theme, in place of the one it had.</summary>
        public static void BindIcon(Form form, EditorIcon icon)
        {
            Bind(form, () => form.Icon = GetIcon(icon));
        }

        /// <summary>An icon as a window icon, for the current theme. Made once per theme and kept: never dispose it.</summary>
        public static Icon GetIcon(EditorIcon icon)
        {
            string key = icon + (ThemeManager.IsDark ? ":dark" : "");
            if (_windowIcons.TryGetValue(key, out Icon made))
                return made;

            using (Bitmap art = Get(icon, 16))
            {
                IntPtr handle = art.GetHicon();
                try
                {
                    //FromHandle doesn't own the handle; its clone owns a copy, so the original can go
                    using (Icon borrowed = Icon.FromHandle(handle))
                        made = (Icon)borrowed.Clone();
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
            _windowIcons[key] = made;
            return made;
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        private static readonly Dictionary<string, Icon> _windowIcons = new Dictionary<string, Icon>();

        //Everything given an icon that follows the theme, and how to give it the current theme's
        private static void Bind(Component owner, Action apply)
        {
            if (owner == null)
                return;
            if (!_bound.ContainsKey(owner))
                owner.Disposed += BoundOwnerDisposed;
            _bound[owner] = apply;
            apply();
        }

        private static void BoundOwnerDisposed(object sender, EventArgs e)
        {
            if (sender is Component owner)
                _bound.Remove(owner);
        }

        private static ImageList Shared(EditorIcon[] order, int size)
        {
            foreach (SharedList shared in _lists)
                if (shared.Order == order && shared.Size == size)
                    return shared.List;

            //An ImageList makes light grey see-through unless told otherwise
            ImageList list = new ImageList() { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(size, size), TransparentColor = Color.Transparent };
            foreach (EditorIcon icon in order)
                list.Images.Add(Get(icon, size));
            _lists.Add(new SharedList() { List = list, Order = order, Size = size });
            return list;
        }

        private static Bitmap Strip(int size, bool dark)
        {
            string name = "icons_" + size + (dark ? "_dark" : "");
            if (_strips.TryGetValue(name, out Bitmap strip))
                return strip;

            strip = null;
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenCAGE.Resources.Icons." + name + ".png"))
                {
                    if (stream != null)
                    {
                        //A Bitmap made from a stream needs the stream for its whole life: this copy doesn't
                        using (Bitmap loaded = new Bitmap(stream))
                            strip = new Bitmap(loaded);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("Editor Icons", "Couldn't load " + name + ": " + ex.Message);
            }
            _strips[name] = strip;
            return strip;
        }

        /* Every shared list gets the other theme's art where its old art was - the controls drawing from them keep their
           lists and indices - then the menus, then anything that copied icons out, and the windows repaint. */
        private static void OnThemeChanged()
        {
            foreach (SharedList shared in _lists)
                for (int i = 0; i < shared.Order.Length && i < shared.List.Images.Count; i++)
                    shared.List.Images[i] = Get(shared.Order[i], shared.Size);

            foreach (Action apply in new List<Action>(_bound.Values))
                apply();

            Changed?.Invoke();

            for (int i = 0; i < Application.OpenForms.Count; i++)
            {
                Form form = Application.OpenForms[i];
                if (form != null && !form.IsDisposed)
                    form.Invalidate(true);
            }
        }
    }
}
