using CATHODE;
using CATHODE.EXPERIMENTAL;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CathodeLib.ObjectExtensions;
using OpenCAGE.ConfigEditors;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups;
using OpenCAGE.Popups.Configuration_Editors;
using OpenCAGE.Scripts;
using OpenCAGE.UserControls;
using OpenCAGE.UnityConnection;
using DarkModeForms;
using DiscordRPC;
using Newtonsoft.Json;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Xml;
using WebSocketSharp;
using WebSocketSharp.Server;
using WeifenLuo.WinFormsUI.Docking;
using Task = System.Threading.Tasks.Task;

namespace OpenCAGE
{
    public partial class CommandsEditor : Form
    {
        public DockPanel DockPanel => dockPanel;

        private CompositeBrowser _compositeBrowser = null;
        public CompositeBrowser CompositeBrowser => _compositeBrowser;

        private CompositeDisplay _compositeDisplay = null;
        public CompositeDisplay CompositeDisplay => _compositeDisplay;

        private EntityInspector _entityInspector = null;
        public EntityInspector EntityInspector => _entityInspector;

        private EntityBrowser _entityBrowser = null;
        public EntityBrowser EntityBrowser => _entityBrowser;

        private EntityList _entityList = null;
        public EntityList EntityList => _entityList;

        private LevelViewerPanel _levelViewerPanel = null;
        public LevelViewerPanel LevelViewerPanel => _levelViewerPanel;

        private EntitySearch _entitySearch = null;
        public EntitySearch EntitySearch => _entitySearch;

        private RenderFiltersPanel _renderFiltersPanel = null;
        public RenderFiltersPanel RenderFiltersPanel => _renderFiltersPanel;

        private SelectLevel _levelSelect = null;

        private DiscordRpcClient _discord;

        private Dictionary<string, ToolStripMenuItem> _levelMenuItems = new Dictionary<string, ToolStripMenuItem>();
        private readonly Dictionary<float, ToolStripMenuItem> _transformGridSnapMenuItems = new Dictionary<float, ToolStripMenuItem>();
        private readonly Dictionary<float, ToolStripMenuItem> _rotationSnapMenuItems = new Dictionary<float, ToolStripMenuItem>();

        private Thread _loadThread = null;
        private ProgressUI _progressUI = null;
        private bool _levelLoadInProgress;
        private System.Windows.Forms.Timer _progressKeepOnTopTimer;
        private bool _cathodeLoadComplete;
        private bool _viewerPopulateFinished;
        private uint _viewerActivePopulateToken;
        private uint _viewerPopulateFinishedToken;
        private int _levelLoadGeneration;
        private uint _populateTokenAtLoadStart;
        private Action<LevelContent> _levelLoadedHandler;
        private const int MaxLevelPanelBuildAttempts = 100;

        private const float DefaultSideDockPortion = 0.22f;
        private const float DefaultEntityInspectorPortion = 0.18f;
        private const int CurrentMainDockLayoutVersion = 8;
        private const double DefaultLeftSearchPortion = 0.28;
        private float _defaultSplitterDistance = 0.25f;
        private int _defaultWidth;
        private int _defaultHeight;
        private int _lastMainDockAreaWidth;
        private int _lastMainDockAreaHeight;
        private int _lastFormClientWidth;
        private int _lastFormClientHeight;

        private bool _settingUp = true;

        private DarkModeCS _dm;

        public CommandsEditor(string level = null)
        {
            //LocalDebug.CheckWriteInstanced();

            InitializeComponent();
#if USE_DARK_MODE
            _dm = new DarkModeCS(this);
#endif

            Singleton.Editor = this;
            Singleton.LoadGlobals();

            //LocalDebug.GetExclusiveMasters("production/tech_comms");
            //LocalDebug.GetExclusiveMasters("production/hab_airport");

            _discord = new DiscordRpcClient("1152999067207606392");
            _discord.Initialize();
            _discord.SetPresence(new RichPresence() { Assets = new Assets() { LargeImageKey = "icon" } });

            Singleton.OnCompositeSelected += OnCompositeSelectedForDiscord;

#if USE_DIRTY_TRACKER
            DirtyTracker.OnChanged += OnDirtyChanged;
#endif

            if (SettingsManager.GetFloat(Settings.NumericStep, -1.0f) == -1.0f)
                SettingsManager.SetFloat(Settings.NumericStep, 0.1f);
            if (SettingsManager.GetFloat(Settings.NumericStepRot, -1.0f) == -1.0f)
                SettingsManager.SetFloat(Settings.NumericStepRot, 1.0f);

            dockPanel.ShowDocumentIcon = true;

            _defaultWidth = Width;
            _defaultHeight = Height;

#if !DEBUG
            //Dev options
            DEBUG_ReloadLevel.Visible = false;
            connectToRuntimeUtils.Visible = false;
            optionsToolStripSeparatorRuntimeUtils.Visible = false;
            
            //WIP forms
            inputsToolStripMenuItem.Visible = false;
            scriptReadableVariablesToolStripMenuItem.Visible = false;
            voiceMappingsToolStripMenuItem.Visible = false;
            localisationToolStripMenuItem.Visible = false;
            levelTextDBsToolStripMenuItem.Visible = false;
            fontConfigToolStripMenuItem.Visible = false;
#endif

            WindowState = SettingsManager.GetString(Settings.WindowState, "Normal") == "Maximized" ? FormWindowState.Maximized : FormWindowState.Normal;
            Width = SettingsManager.GetInteger(Settings.WindowWidth, _defaultWidth);
            Height = SettingsManager.GetInteger(Settings.WindowHeight, _defaultHeight);
            ApplyMainDockPortionsFromSettings();
            UpdateMainDockAreaCache();
            Resize += CommandsEditor_Resize;
            Shown += CommandsEditor_Shown;
            FormClosing += CommandsEditor_FormClosing;
            Load += CommandsEditor_Load;

            Singleton.OnEntityAdded += OnEntityAdded;
            Singleton.OnResourceModified += OnResourceModified;

            //Fixes for dodgy top dropdowns
            compositeViewerToolStripMenuItem.MouseHover += (sender, e) => { ((ToolStripMenuItem)sender).PerformClick(); };
            compositeViewerToolStripMenuItem.DropDown.Closing += DropDown_Closing;
            entityDisplayToolStripMenuItem.MouseHover += (sender, e) => { ((ToolStripMenuItem)sender).PerformClick(); };
            entityDisplayToolStripMenuItem.DropDown.Closing += DropDown_Closing;
            miscToolStripMenuItem.MouseHover += (sender, e) => { ((ToolStripMenuItem)sender).PerformClick(); };
            miscToolStripMenuItem.DropDown.Closing += DropDown_Closing;
            toolStripButton2.DropDown.Closing += DropDown_Closing;
            viewportOptionsToolStripMenuItem.DropDown.Closing += DropDown_Closing;
            viewportOptionsToolStripMenuItem.DropDownOpening += ViewportOptionsDropdownOpening;
            SetupOptions();

            //Populate level list
            List<string> levels = Level.GetLevels(Singleton.PathToAI);
            for (int i = 0; i < levels.Count; i++)
            {
                ToolStripMenuItem levelItem = new ToolStripMenuItem(levels[i]);
                levelItem.Click += OnLevelSelected;
                loadLevel.DropDownItems.Add(levelItem);
                _levelMenuItems.Add(levels[i], levelItem);
            }

            //If we have been launched to a level, load that
            if (level != null)
                OnLevelSelected(level);
            else
                loadLevel_Click(null, null);
        }

        private void SetupOptions()
        {
            ConfigureLevelViewerAvailability();

            if (!SettingsManager.IsSet(Settings.RuntimeUtilsOpt)) SettingsManager.SetBool(Settings.RuntimeUtilsOpt, false);
            connectToRuntimeUtils.Checked = SettingsManager.GetBool(Settings.RuntimeUtilsOpt);
            if (connectToRuntimeUtils.Checked)
            {
                if (!RuntimeUtilsConnection.Send.Start())
                {
                    connectToRuntimeUtils.Checked = false;
                    SettingsManager.SetBool(Settings.RuntimeUtilsOpt, false);
                }
            }
            if (!SettingsManager.IsSet(Settings.HighlightAliases)) SettingsManager.SetBool(Settings.HighlightAliases, true);
            highlightAliasesToolStripMenuItem.Checked = !SettingsManager.GetBool(Settings.HighlightAliases); highlightAliasesToolStripMenuItem.PerformClick();
            if (!SettingsManager.IsSet(Settings.HighlightProxies)) SettingsManager.SetBool(Settings.HighlightProxies, true);
            highlightProxiesToolStripMenuItem.Checked = !SettingsManager.GetBool(Settings.HighlightProxies); highlightProxiesToolStripMenuItem.PerformClick();
            showCameraPositionToolStripMenuItem.Checked = !SettingsManager.GetBool(Settings.ShowCameraPosition); showCameraPositionToolStripMenuItem.PerformClick();
            renderWireframeToolStripMenuItem.Checked = !SettingsManager.GetBool(Settings.RenderWireframe); renderWireframeToolStripMenuItem.PerformClick();
            hideNestedScriptEntitiesToolStripMenuItem.Checked = !SettingsManager.GetBool(Settings.HideNestedScriptEntities); hideNestedScriptEntitiesToolStripMenuItem.PerformClick();
            resetRenderFiltersOnLoadToolStripMenuItem.Checked = !SettingsManager.GetBool(Settings.ResetRenderFilters); resetRenderFiltersOnLoadToolStripMenuItem.PerformClick();

            if (!SettingsManager.IsSet(Settings.LevelViewerDeepSelectMode))
                SettingsManager.SetInteger(Settings.LevelViewerDeepSelectMode, 0);
            if (!SettingsManager.IsSet(Settings.LevelViewerGizmoMode))
                SettingsManager.SetInteger(Settings.LevelViewerGizmoMode, 0);
            ApplyLevelViewerViewportModesFromSettings();

            showEntityIDs.Checked = !SettingsManager.GetBool(Settings.ShowShortGuids); showEntityIDs.PerformClick();
            searchOnlyCompositeNames.Checked = !SettingsManager.GetBool(Settings.CompNameOnlyOpt); searchOnlyCompositeNames.PerformClick();
            keepFunctionUsesWindowOpenToolStripMenuItem.Checked = !SettingsManager.GetBool(Settings.KeepUsesWindowOpen); keepFunctionUsesWindowOpenToolStripMenuItem.PerformClick();
            writeInstancedResourcesExperimentalToolStripMenuItem.Checked = !SettingsManager.GetBool(Settings.CompileInstances); writeInstancedResourcesExperimentalToolStripMenuItem.PerformClick();
            openGameOnSaveToolStripMenuItem.Checked = !SettingsManager.GetBool(Settings.LaunchGameWhenSaved); openGameOnSaveToolStripMenuItem.PerformClick();
            showGamePlatformToolStripMenuItem.Checked = !SettingsManager.GetBool(Settings.ShowGamePlatform); showGamePlatformToolStripMenuItem.PerformClick();

            if (!SettingsManager.IsSet(Settings.ShowTexOpt)) SettingsManager.SetBool(Settings.ShowTexOpt, true);
            useTexturedModelViewExperimentalToolStripMenuItem.Checked = !SettingsManager.GetBool(Settings.ShowTexOpt); useTexturedModelViewExperimentalToolStripMenuItem.PerformClick();

            if (!SettingsManager.IsSet(Settings.ShowSavedMsgOpt)) SettingsManager.SetBool(Settings.ShowSavedMsgOpt, true);
            showConfirmationWhenSavingToolStripMenuItem.Checked = !SettingsManager.GetBool(Settings.ShowSavedMsgOpt); showConfirmationWhenSavingToolStripMenuItem.PerformClick();

            showExplorerViewToolStripMenuItem.Checked = !SettingsManager.GetBool(Settings.EnableFileBrowser); showExplorerViewToolStripMenuItem.PerformClick();

            if (!SettingsManager.IsSet(Settings.SavePakAndBin)) SettingsManager.SetBool(Settings.SavePakAndBin, true);
            savePAKAndBINToolStripMenuItem.Checked = !SettingsManager.GetBool(Settings.SavePakAndBin); savePAKAndBINToolStripMenuItem.PerformClick();

            if (!SettingsManager.IsSet(Settings.PopulateAllPinsOnCreateNode)) SettingsManager.SetBool(Settings.PopulateAllPinsOnCreateNode, true);
            populateAllNodePinsWhenCreatedToolStripMenuItem.Checked = !SettingsManager.GetBool(Settings.PopulateAllPinsOnCreateNode); populateAllNodePinsWhenCreatedToolStripMenuItem.PerformClick();

            if (!SettingsManager.IsSet(Settings.OptionToDeleteEntityWithNode)) SettingsManager.SetBool(Settings.OptionToDeleteEntityWithNode, true);
            giveOptionToDeleteEntityWhenNoNodesToolStripMenuItem.Checked = !SettingsManager.GetBool(Settings.OptionToDeleteEntityWithNode); giveOptionToDeleteEntityWhenNoNodesToolStripMenuItem.PerformClick();

            if (!SettingsManager.IsSet(Settings.AskBeforeDeletingNode)) SettingsManager.SetBool(Settings.AskBeforeDeletingNode, true);
            showConfirmationWhenDeletingNodeToolStripMenuItem.Checked = !SettingsManager.GetBool(Settings.AskBeforeDeletingNode); showConfirmationWhenDeletingNodeToolStripMenuItem.PerformClick();

            if (!SettingsManager.IsSet(Settings.NodeColour_FunctionNode))
                SettingsManager.SetInteger(Settings.NodeColour_FunctionNode, Color.FromArgb(30, 144, 255).ToArgb());
            if (!SettingsManager.IsSet(Settings.NodeColour_FunctionNodeBottom))
                SettingsManager.SetInteger(Settings.NodeColour_FunctionNodeBottom, Color.FromArgb(10, 109, 157).ToArgb());
            if (!SettingsManager.IsSet(Settings.NodeColour_FunctionText))
                SettingsManager.SetInteger(Settings.NodeColour_FunctionText, Color.White.ToArgb());

            if (!SettingsManager.IsSet(Settings.NodeColour_AliasNode))
                SettingsManager.SetInteger(Settings.NodeColour_AliasNode, Color.FromArgb(255, 114, 30).ToArgb());
            if (!SettingsManager.IsSet(Settings.NodeColour_AliasNodeBottom))
                SettingsManager.SetInteger(Settings.NodeColour_AliasNodeBottom, Color.FromArgb(196, 76, 29).ToArgb());
            if (!SettingsManager.IsSet(Settings.NodeColour_AliasText))
                SettingsManager.SetInteger(Settings.NodeColour_AliasText, Color.White.ToArgb());

            if (!SettingsManager.IsSet(Settings.NodeColour_ProxyNode))
                SettingsManager.SetInteger(Settings.NodeColour_ProxyNode, Color.FromArgb(35, 196, 22).ToArgb());
            if (!SettingsManager.IsSet(Settings.NodeColour_ProxyNodeBottom))
                SettingsManager.SetInteger(Settings.NodeColour_ProxyNodeBottom, Color.FromArgb(9, 153, 72).ToArgb());
            if (!SettingsManager.IsSet(Settings.NodeColour_ProxyText))
                SettingsManager.SetInteger(Settings.NodeColour_ProxyText, Color.White.ToArgb());

            if (!SettingsManager.IsSet(Settings.NodeColour_InstanceNode))
                SettingsManager.SetInteger(Settings.NodeColour_InstanceNode, Color.FromArgb(195, 30, 255).ToArgb());
            if (!SettingsManager.IsSet(Settings.NodeColour_InstanceNodeBottom))
                SettingsManager.SetInteger(Settings.NodeColour_InstanceNodeBottom, Color.FromArgb(118, 10, 157).ToArgb());
            if (!SettingsManager.IsSet(Settings.NodeColour_InstanceText))
                SettingsManager.SetInteger(Settings.NodeColour_InstanceText, Color.White.ToArgb());

            if (!SettingsManager.IsSet(Settings.NodeColour_VariableNode))
                SettingsManager.SetInteger(Settings.NodeColour_VariableNode, Color.Red.ToArgb());
            if (!SettingsManager.IsSet(Settings.NodeColour_VariableText))
                SettingsManager.SetInteger(Settings.NodeColour_VariableText, Color.White.ToArgb());

            //Launch game is only supported by certain platforms due to having to patch the binary
            switch (Singleton.Platform)
            {
                case PatchManager.Platform.STEAM:
                case PatchManager.Platform.EPIC_GAMES_STORE:
                case PatchManager.Platform.GOG:
                    launchGameBtn.Enabled = true;
                    break;
                default:
                    launchGameBtn.Enabled = false;
                    break;
            }

#if SHIP_BUILD
            //This option is dependent on external tools, so disable if they don't exist
            if (!Directory.Exists("legendplugin"))
                behaviourTreesToolStripMenuItem.Enabled = false;
#endif

            versionToolStripMenuItem.Text = "Version " + ProductVersion;
            _settingUp = false;
        }

        private void OnEntityAdded(Entity e)
        {
            Steam.UnlockAchievement(Steam.Achievements.CREATE_A_NEW_ENTITY);

            int entCount = SettingsManager.GetInteger(Settings.EntityCounter) + 1;
            SettingsManager.SetInteger(Settings.EntityCounter, entCount);
            if (entCount >= 100)
                Steam.UnlockAchievement(Steam.Achievements.ONE_HUNDRED_ENTITIES);
        }

        private void OnResourceModified()
        {
            Steam.UnlockAchievement(Steam.Achievements.ASSETS_MODIFIED);
        }

        //keep dropdown open if cursor is inside it 
        private void DropDown_Closing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            var dropdown = sender as ToolStripDropDown;
            if (dropdown != null)
            {
                Point cursorPosition = dropdown.PointToClient(Cursor.Position);
                if (dropdown.DisplayRectangle.Contains(cursorPosition))
                {
                    e.Cancel = true;
                }
            }
        }

        private void CommandsEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            KillBehaviourTreeEditor();
            HideLoadProgressUI();
            KillLevelViewer();
            SaveDockLayout();
        }

        private void CommandsEditor_Load(object sender, EventArgs e)
        {
            UpdateMainDockAreaCache();
            UpdateFormClientSizeCache();
        }

        private void CommandsEditor_Shown(object sender, EventArgs e)
        {
            NormalizeMainDockPortionsToRatios();
            UpdateMainDockAreaCache();
            UpdateFormClientSizeCache();
        }

        //UI: remember width/height of editor
        private void CommandsEditor_Resize(object sender, EventArgs e)
        {
            bool formSizeChanged =
                ClientSize.Width != _lastFormClientWidth
                || ClientSize.Height != _lastFormClientHeight;
            if (formSizeChanged)
                ConvertMainDockPixelPortionsBeforeResize();

            switch (WindowState)
            {
                case FormWindowState.Normal:
                    SettingsManager.SetInteger(Settings.WindowWidth, Width);
                    SettingsManager.SetInteger(Settings.WindowHeight, Height);
                    break;
                case FormWindowState.Maximized:

                    break;
            }
            SettingsManager.SetString(Settings.WindowState, WindowState.ToString());
            UpdateMainDockAreaCache();
            UpdateFormClientSizeCache();
        }

        private static double ClampDockPortion(double portion)
        {
            return Math.Max(0.05, Math.Min(0.95, portion));
        }

        private static double ToStoredDockPortion(double portion, double axisSize)
        {
            if (portion <= 1.0 || axisSize <= 0)
                return portion;
            return ClampDockPortion(portion / axisSize);
        }

        private static double LoadDockPortionSetting(float savedPortion, int savedAxisSize, double currentAxisSize, double defaultPortion)
        {
            if (savedPortion <= 0f)
                return defaultPortion;
            if (savedPortion <= 1f)
                return savedPortion;
            if (savedAxisSize > 0)
                return ClampDockPortion(savedPortion / savedAxisSize);
            if (currentAxisSize > 0)
                return ClampDockPortion(savedPortion / currentAxisSize);
            return defaultPortion;
        }

        private void ApplyMainDockPortionsFromSettings()
        {
            if (dockPanel == null)
                return;

            Rectangle area = dockPanel.DockArea;
            double areaWidth = area.Width > 0 ? area.Width : Width;
            double areaHeight = area.Height > 0 ? area.Height : Height;
            int savedWidth = SettingsManager.GetInteger(Settings.WindowWidth, Width);
            int savedHeight = SettingsManager.GetInteger(Settings.WindowHeight, Height);

            dockPanel.DockLeftPortion = LoadDockPortionSetting(
                SettingsManager.GetFloat(Settings.SplitWidthMainRight, DefaultSideDockPortion),
                savedWidth,
                areaWidth,
                DefaultSideDockPortion);
            dockPanel.DockRightPortion = LoadDockPortionSetting(
                SettingsManager.GetFloat(Settings.EntityInspectorWidth, DefaultEntityInspectorPortion),
                savedWidth,
                areaWidth,
                DefaultEntityInspectorPortion);
            dockPanel.DockBottomPortion = LoadDockPortionSetting(
                SettingsManager.GetFloat(Settings.SplitWidthMainBottom, _defaultSplitterDistance),
                savedHeight,
                areaHeight,
                _defaultSplitterDistance);
        }

        private void SaveMainDockPortionsToSettings()
        {
            if (dockPanel == null)
                return;

            Rectangle area = dockPanel.DockArea;
            double width = area.Width > 0 ? area.Width : Width;
            double height = area.Height > 0 ? area.Height : Height;

            SettingsManager.SetFloat(
                Settings.SplitWidthMainRight,
                (float)ToStoredDockPortion(dockPanel.DockLeftPortion, width));
            SettingsManager.SetFloat(
                Settings.EntityInspectorWidth,
                (float)ToStoredDockPortion(dockPanel.DockRightPortion, width));
            SettingsManager.SetFloat(
                Settings.SplitWidthMainBottom,
                (float)ToStoredDockPortion(dockPanel.DockBottomPortion, height));
        }

        private void ConvertMainDockPixelPortionsBeforeResize()
        {
            if (dockPanel == null)
                return;

            double widthBasis = _lastMainDockAreaWidth > 0
                ? _lastMainDockAreaWidth
                : dockPanel.DockArea.Width > 0
                    ? dockPanel.DockArea.Width
                    : ClientSize.Width;
            double heightBasis = _lastMainDockAreaHeight > 0
                ? _lastMainDockAreaHeight
                : dockPanel.DockArea.Height > 0
                    ? dockPanel.DockArea.Height
                    : ClientSize.Height;

            NormalizeMainDockPortionsToRatios(widthBasis, heightBasis);
        }

        private void NormalizeMainDockPortionsAfterXmlLoad()
        {
            NormalizeMainDockPortionsToRatios();
        }

        private void NormalizeMainDockPortionsToRatios(double? widthBasis = null, double? heightBasis = null)
        {
            if (dockPanel == null)
                return;

            Rectangle area = dockPanel.DockArea;
            double width = widthBasis
                ?? (area.Width > 0 ? area.Width : ClientSize.Width);
            double height = heightBasis
                ?? (area.Height > 0 ? area.Height : ClientSize.Height);

            if (dockPanel.DockLeftPortion > 1.0 && width > 0)
                dockPanel.DockLeftPortion = ClampDockPortion(dockPanel.DockLeftPortion / width);
            if (dockPanel.DockRightPortion > 1.0 && width > 0)
                dockPanel.DockRightPortion = ClampDockPortion(dockPanel.DockRightPortion / width);
            if (dockPanel.DockBottomPortion > 1.0 && height > 0)
                dockPanel.DockBottomPortion = ClampDockPortion(dockPanel.DockBottomPortion / height);
        }

        private void UpdateMainDockAreaCache()
        {
            if (dockPanel == null)
                return;

            Rectangle area = dockPanel.DockArea;
            if (area.Width > 0)
                _lastMainDockAreaWidth = area.Width;
            if (area.Height > 0)
                _lastMainDockAreaHeight = area.Height;
        }

        private void UpdateFormClientSizeCache()
        {
            _lastFormClientWidth = ClientSize.Width;
            _lastFormClientHeight = ClientSize.Height;
        }

        private void OnCompositeSelectedForDiscord(Composite composite)
        {
            RichPresence newPresence = _discord.CurrentPresence.Copy();
            newPresence.Details = "Level: " + (_compositeBrowser?.Content?.Level?.Name ?? "No Level");
            newPresence.State = "Composite: " + EditorUtils.GetCompositeName(composite);
            _discord.SetPresence(newPresence);
            _discord.UpdateStartTime();

            if (_compositeBrowser?.Content?.Level == null)
                Steam.UpdatePresence(Steam.RichPresences.NO_PRESENCE);
            else
                Steam.UpdatePresence(Steam.RichPresences.EditingLevel, _compositeBrowser.Content.Level.Name);
        }

        private void OnDirtyChanged(bool dirty) => UpdateTitle();
        private void UpdateTitle()
        {
            string title = "OpenCAGE";

            if (SettingsManager.GetBool(Settings.ShowGamePlatform))
            {
                switch (Singleton.Platform)
                {
                    case PatchManager.Platform.STEAM:
                        title += " - Steam";
                        break;
                    case PatchManager.Platform.EPIC_GAMES_STORE:
                        title += " - Epic Games Store";
                        break;
                    case PatchManager.Platform.GOG:
                        title += " - GoG";
                        break;
                    case PatchManager.Platform.WINDOWS_STORE:
                        title += " - Windows Store";
                        break;
                    case PatchManager.Platform.SWITCH:
                        title += " - Nintendo Switch";
                        break;
                    case PatchManager.Platform.IOS_ANDROID:
                        title += " - Mobile";
                        break;
                    case PatchManager.Platform.MAC_LINUX:
                        title += " - Mac/Linux";
                        break;
                    default:
                        title += " - Unknown Platform";
                        break;
                }
            }

            if (_compositeBrowser == null)
            {
                this.Text = title;
            }
            else
            {
                string[] levelBits = _compositeBrowser.Content.Level.Name.Split('/');
                this.Text = title + " - " + levelBits[levelBits.Length - 1] + " (" + _compositeBrowser.Content.Level.Name.Substring(0, _compositeBrowser.Content.Level.Name.Length - levelBits[levelBits.Length - 1].Length).TrimEnd('/') + ")";
            }

#if USE_DIRTY_TRACKER
            if (DirtyTracker.IsDirty)
                this.Text += " [UNSAVED CHANGES]";
#endif
        }

        public void LoadLevel(string level)
        {
            OnLevelSelected(level);
        }

        private void loadLevel_Click(object sender, EventArgs e)
        {
            if (_levelSelect == null)
            {
                _levelSelect = new SelectLevel();
                _levelSelect.Show();
                _levelSelect.FormClosed += OnLevelSelectClosed;
                _levelSelect.OnLevelSelected += OnLevelSelected;
            }
        }
        private void OnLevelSelectClosed(object sender, FormClosedEventArgs e)
        {
            _levelSelect = null;
        }
        private void OnLevelSelected(object sender, EventArgs e)
        {
            OnLevelSelected(((ToolStripMenuItem)sender).Text);
        }
        private void OnLevelSelected(string level)
        {
            if (level == null)
                return;
            level = level.ToUpper();

            SettingsManager.SetString(Settings.LastSelectedLevel, level);

            bool hadPreviousLevel = _compositeBrowser != null;
            if (hadPreviousLevel)
            {
                Singleton.Editor.DockPanel.ActiveAutoHideContent = null;
                string oldLevelName = _compositeBrowser.Content?.Level?.Name;
                if (oldLevelName != null)
                    _levelMenuItems[oldLevelName].Checked = false;

                CloseLevelPanels();
            }

#if DEBUG
            //if (Directory.Exists(Singleton.PathToAI + "\\LatestBuiltData\\ENV"))
            //{
            //    Directory.Delete(Singleton.PathToAI + "\\DATA\\ENV\\" + level, true);
            //    CopyFilesRecursively(Singleton.PathToAI + "\\LatestBuiltData\\ENV\\" + level, Singleton.PathToAI + "\\DATA\\ENV\\" + level);
            //}
#endif

            _compositeBrowser = new CompositeBrowser(level);
            BeginLevelLoadTracking();

            _viewerActivePopulateToken = 0;
            _cathodeLoadComplete = false;
            _viewerPopulateFinished = !Singleton.ViewportEnabled;
            _populateTokenAtLoadStart = _viewerPopulateFinishedToken;

            HideLoadProgressUI();
            ShowLoadProgressLoading(_compositeBrowser.Content.Level);
            EnableButtons(false, "Loading " + _compositeBrowser.Content.Level.Name + "...");

            if (hadPreviousLevel)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
            }

            PrepareLevelLoadWorkspace();
            BeginParallelLevelViewerLoad(_compositeBrowser.Content.Level.Name);

            _loadThread = new Thread(ThreadedLevelLoader);
            _loadThread.Start();

            _levelMenuItems[_compositeBrowser.Content.Level.Name].Checked = true;
            UpdateTitle();

            if (Singleton.ViewportEnabled && SettingsManager.GetBool(Settings.ResetRenderFilters))
            {
                foreach (RenderFilterDefinitions.Definition definition in RenderFilterDefinitions.All)
                {
                    RenderFilters.SetEnabled(definition.FunctionType, false);
                }
                UnityConnection.Send.SendRenderFilterPacket();
                _renderFiltersPanel?.RefreshFilters();
            }

            Steam.UnlockAchievement(Steam.Achievements.FIRST_LOAD);
        }

        private void ThreadedLevelLoader()
        {
            int loadGeneration = _levelLoadGeneration;
#if !CATHODE_FAIL_HARD
            try
            {
#endif
                _compositeBrowser.Content.Load();
                if (loadGeneration != _levelLoadGeneration)
                    return;

                if (_compositeBrowser.Content.Level?.Commands == null || !_compositeBrowser.Content.Level.Commands.Loaded)
                    UnityConnection.Send.NotifyLevelLoadAborted();
#if !CATHODE_FAIL_HARD
            }
            catch
            {
                if (loadGeneration != _levelLoadGeneration)
                    return;

                UnityConnection.Send.NotifyLevelLoadAborted();
                this.BeginInvoke(new Action(() =>
                {
                    if (loadGeneration != _levelLoadGeneration)
                        return;

                    EndViewerPopulateProgress(0, forceClose: true);
                    EnableButtons(true, "");
                    //TODO: warn!
                }));
            }
#endif
        }

        private void BeginLevelLoadTracking()
        {
            if (_levelLoadedHandler != null)
            {
                Singleton.OnLevelLoaded -= _levelLoadedHandler;
                _levelLoadedHandler = null;
            }

            _levelLoadGeneration++;
            int loadGeneration = _levelLoadGeneration;

            _levelLoadedHandler = content =>
            {
                Singleton.OnLevelLoaded -= _levelLoadedHandler;
                _levelLoadedHandler = null;

                if (loadGeneration != _levelLoadGeneration)
                    return;

                ShowLevelPanelsWhenLoaded(content, loadGeneration);
            };
            Singleton.OnLevelLoaded += _levelLoadedHandler;
        }

        private void StartProgressKeepOnTop()
        {
            StopProgressKeepOnTop();

            _progressKeepOnTopTimer = new System.Windows.Forms.Timer();
            _progressKeepOnTopTimer.Interval = 200;
            _progressKeepOnTopTimer.Tick += (s, e) =>
            {
                if (_progressUI == null || _progressUI.IsDisposed)
                {
                    StopProgressKeepOnTop();
                    return;
                }

                if (!_progressUI.TopMost)
                    _progressUI.TopMost = true;

                _progressUI.BringToFront();
            };
            _progressKeepOnTopTimer.Start();
        }

        private void StopProgressKeepOnTop()
        {
            if (_progressKeepOnTopTimer == null)
                return;

            _progressKeepOnTopTimer.Stop();
            _progressKeepOnTopTimer.Dispose();
            _progressKeepOnTopTimer = null;
        }

        private void CloseProgressUI()
        {
            StopProgressKeepOnTop();

            if (_progressUI != null && !_progressUI.IsDisposed)
            {
                _progressUI.Close();
                _progressUI.Dispose();
                _progressUI = null;
            }
        }

        private void EnsureProgressUI()
        {
            if (_progressUI == null || _progressUI.IsDisposed)
                _progressUI = new ProgressUI();
        }

        private void ShowLoadProgressLoading(Level level)
        {
            if (level == null)
                return;

            CloseProgressUI();
            EnsureProgressUI();
            _progressUI.ShowLevelLoading(level);
            StartProgressKeepOnTop();
            _levelLoadInProgress = true;
        }

        private void ShowLoadProgressPopulating(string displayLabel)
        {
            EnsureProgressUI();
            _progressUI.ShowViewerPopulating(displayLabel);
            StartProgressKeepOnTop();
        }

        private void HideLoadProgressUI()
        {
            _levelLoadInProgress = false;
            CloseProgressUI();
        }

        private void ShowViewerPopulateMarquee(string displayLabel)
        {
            ShowLoadProgressPopulating(displayLabel);
        }

        private void FinishLevelLoadProgress()
        {
            ResetLevelLoadProgressState();
            HideLoadProgressUI();

            if (Singleton.ViewportEnabled)
                _compositeDisplay?.ShowLevelViewerPanel(activate: false);
        }

        internal void ShowViewerPopulateProgress(string levelName, uint populateToken)
        {
            if (populateToken == 0 || populateToken <= _viewerPopulateFinishedToken)
                return;

            _viewerActivePopulateToken = populateToken;

            if (_levelLoadInProgress)
                _viewerPopulateFinished = false;

            ShowViewerPopulateMarquee(levelName);
        }

        internal void EndViewerPopulateProgress(uint populateToken = 0, bool forceClose = false)
        {
            if (forceClose)
            {
                FinishLevelLoadProgress();
                return;
            }

            if (!_levelLoadInProgress)
            {
                if (!TryAcknowledgeViewerPopulateFinished(populateToken))
                    return;

                HideLoadProgressUI();
                return;
            }

            if (!TryAcknowledgeViewerPopulateFinished(populateToken))
                return;

            TryCloseLevelLoadProgress();
        }

        private bool TryAcknowledgeViewerPopulateFinished(uint populateToken)
        {
            if (populateToken != 0)
            {
                if (populateToken <= _viewerPopulateFinishedToken)
                    return false;

                if (_viewerActivePopulateToken != 0 && populateToken != _viewerActivePopulateToken)
                    return false;

                _viewerPopulateFinishedToken = populateToken;
                _viewerActivePopulateToken = 0;
                _viewerPopulateFinished = true;
            }
            else
            {
                if (_viewerActivePopulateToken != 0)
                    return false;

                _viewerPopulateFinished = true;
            }

            return true;
        }

        private void ResetLevelLoadProgressState()
        {
            _cathodeLoadComplete = false;
            _viewerPopulateFinished = false;
            _viewerActivePopulateToken = 0;
        }

        private void TryCloseLevelLoadProgress()
        {
            if (!_cathodeLoadComplete)
                return;

            if (Singleton.ViewportEnabled && !_viewerPopulateFinished)
                return;

            FinishLevelLoadProgress();
        }

        private void OnCathodeLoadComplete(string levelName, int loadGeneration)
        {
            if (loadGeneration != _levelLoadGeneration)
                return;

            _cathodeLoadComplete = true;

            if (Singleton.ViewportEnabled && !_viewerPopulateFinished)
            {
                if (_viewerPopulateFinishedToken > _populateTokenAtLoadStart)
                    _viewerPopulateFinished = true;
                else
                    ShowViewerPopulateMarquee(levelName);
            }

            TryCloseLevelLoadProgress();
        }

        private void ShowLevelPanelsWhenLoaded(LevelContent content, int loadGeneration)
        {
            if (loadGeneration != _levelLoadGeneration)
                return;

            string levelName = content.Level?.Name;
            if (InvokeRequired)
                Invoke(new Action(() => OnCathodeLoadComplete(levelName, loadGeneration)));
            else
                OnCathodeLoadComplete(levelName, loadGeneration);

            QueueBuildLevelPanelsWhenReady(content, loadGeneration, 0);
        }

        private void QueueBuildLevelPanelsWhenReady(LevelContent content, int loadGeneration, int attempt)
        {
            BeginInvoke(new Action(() => TryBuildLevelPanelsWhenReady(content, loadGeneration, attempt)));
        }

        private void TryBuildLevelPanelsWhenReady(LevelContent content, int loadGeneration, int attempt)
        {
            if (loadGeneration != _levelLoadGeneration)
                return;

            //Another level load replaced this browser - don't retry, the new load owns the UI now.
            if (_compositeBrowser == null || _compositeBrowser.IsDisposed)
            {
                if (attempt < MaxLevelPanelBuildAttempts)
                    QueueBuildLevelPanelsWhenReady(content, loadGeneration, attempt + 1);
                return;
            }

            LevelContent readyContent = _compositeBrowser.Content;
            if (readyContent == null || readyContent != content)
                return;

            EnsureDockPanelsCreated();
            readyContent.EnsureEditorUtils();

            if (_compositeDisplay == null || _compositeDisplay.IsDisposed)
            {
                if (attempt < MaxLevelPanelBuildAttempts)
                    QueueBuildLevelPanelsWhenReady(content, loadGeneration, attempt + 1);
                return;
            }

            EnableButtons(true, "");
            _compositeBrowser.Resize += _compositeBrowser_Resize;
            _compositeBrowser.FormClosed += _compositeBrowser_FormClosed;
            _compositeBrowser.DockStateChanged += DockPanelContent_DockStateChanged;

            EnsureRequiredDockLayout();

            UpdateCompositeBrowserDockState();

            _entityBrowser.InitializeFromLevel();
            _entityList.UpdateTitle();
            _entitySearch.InitializeFromLevel();
            _compositeBrowser.OnLevelDataReady();
            _compositeBrowser.LoadInitialComposite();
            _compositeDisplay.Show(dockPanel, DockState.Document);
            _entityList.FocusPanel();

            BeginInvoke(new Action(() =>
            {
                if (loadGeneration != _levelLoadGeneration)
                    return;

                if (_compositeBrowser == null || _compositeBrowser.IsDisposed)
                    return;

                _compositeBrowser.UpdateDockState();
                _compositeBrowser.EnsureCompositeTreePopulated();
            }));
        }

        private void PrepareLevelLoadWorkspace()
        {
            EnsureDockPanelsCreated();

            bool layoutLoaded = TryLoadDockLayout();
            if (!layoutLoaded || !IsMainDockLayoutValid())
                ApplyDefaultDockLayout();
            else
                EnsureRequiredDockLayout();

            _compositeDisplay.Show(dockPanel, DockState.Document);
            _compositeDisplay.EnsureInnerDockLayoutRestored();

            if (Singleton.ViewportEnabled)
                _compositeDisplay.EnsureLevelViewerDocked();

            if (_levelViewerPanel?.IsRunning == true)
                _compositeDisplay.HideLevelViewerPanelForLoad();
        }

        private void BeginParallelLevelViewerLoad(string levelName)
        {
            if (!Singleton.ViewportEnabled || string.IsNullOrEmpty(levelName))
                return;

            if (_compositeDisplay == null || _levelViewerPanel == null)
                return;

            if (!EnsureLevelViewerConnection())
                return;

            UnityConnection.Send.NotifyLevelLoadStarting(levelName);
            _compositeDisplay.EnsureLevelViewerDocked();

            if (_levelViewerPanel.IsRunning)
            {
                _compositeDisplay.HideLevelViewerPanelForLoad();
                return;
            }

            _levelViewerPanel.Launch(focusAfterEmbed: false);

            if (!_levelViewerPanel.IsRunning)
                return;

            Steam.UnlockAchievement(Steam.Achievements.LEVEL_VIEWER_LAUNCHED); //todo - deprecate this post v18?
            _compositeDisplay.HideLevelViewerPanelForLoad();
        }

        private void EnsureDockPanelsCreated()
        {
            if (_entityInspector == null)
            {
                _entityInspector = new EntityInspector();
                _entityInspector.FormClosing += EntityInspector_FormClosing;
                _entityInspector.Resize += _entityInspector_Resize;
                _entityInspector.DockStateChanged += DockPanelContent_DockStateChanged;
            }

            if (_entityList == null)
            {
                _entityList = new EntityList();
                _entityList.FormClosing += EntityList_FormClosing;
                _entityList.DockStateChanged += DockPanelContent_DockStateChanged;
            }

            if (_levelViewerPanel == null)
            {
                _levelViewerPanel = new LevelViewerPanel();
                _levelViewerPanel.ProcessExited += LevelViewerPanel_ProcessExited;
                EnsureLevelViewerToolbarConfigured();
            }

            if (_compositeDisplay == null)
            {
                _compositeDisplay = new CompositeDisplay(_compositeBrowser, _entityInspector, _entityList, _levelViewerPanel);
                _compositeDisplay.FormClosing += CompositeDisplay_FormClosing;
                _compositeDisplay.DockStateChanged += DockPanelContent_DockStateChanged;

                if (Singleton.ViewportEnabled)
                    _compositeDisplay.EnsureInnerDockLayoutRestored();
            }

            if (_entityBrowser == null)
            {
                _entityBrowser = new EntityBrowser();
                _entityBrowser.DockStateChanged += DockPanelContent_DockStateChanged;
            }

            if (_entitySearch == null)
            {
                _entitySearch = new EntitySearch();
                _entitySearch.FormClosing += EntitySearch_FormClosing;
                _entitySearch.DockStateChanged += DockPanelContent_DockStateChanged;
            }

            if (_renderFiltersPanel == null)
            {
                _renderFiltersPanel = new RenderFiltersPanel();
                _renderFiltersPanel.FormClosing += RenderFiltersPanel_FormClosing;
                _renderFiltersPanel.DockStateChanged += DockPanelContent_DockStateChanged;
            }

            _entityInspector.AttachCompositeDisplay(_compositeDisplay);
        }

        private void ApplyDefaultDockLayout(bool resetInnerDock = true)
        {
            dockPanel.DockLeftPortion = DefaultSideDockPortion;
            dockPanel.DockRightPortion = DefaultEntityInspectorPortion;
            dockPanel.DockBottomPortion = _defaultSplitterDistance;

            _compositeDisplay.Show(dockPanel, DockState.Document);
            ApplyLeftDockLayout();
            ApplyRightDockLayout();

            if (resetInnerDock)
            {
                if (SettingsManager.IsSet(Settings.CompositeDisplayDockTopPortion))
                    _compositeDisplay?.EnsureInnerDockLayoutRestored();
                else
                    _compositeDisplay?.ApplyDefaultInnerDockLayout();
            }

            SettingsManager.SetInteger(Settings.MainDockLayoutVersion, CurrentMainDockLayoutVersion);
            SaveDockLayout();
        }

        private void ApplyLeftDockLayout()
        {
            EnsureDockPanelsCreated();

            HideLeftDockPanelsForRelayout();

            _entitySearch.Show(dockPanel, DockState.DockLeft);

            if (Singleton.ViewportEnabled)
                _renderFiltersPanel.Show(_entitySearch.Pane, (IDockContent)null);

            _compositeBrowser.Show(_entitySearch.Pane, DockAlignment.Bottom, 1.0 - DefaultLeftSearchPortion);
            _entityBrowser.Show(_compositeBrowser.Pane, (IDockContent)null);
            _entityList.Show(_compositeBrowser.Pane, (IDockContent)null);
        }

        private void HideLeftDockPanelsForRelayout()
        {
            DockContent[] leftPanels =
            {
                _entityList,
                _entityBrowser,
                _compositeBrowser,
                _entitySearch,
                _renderFiltersPanel,
            };

            foreach (DockContent panel in leftPanels)
            {
                if (panel != null && panel.DockState != DockState.Hidden)
                    panel.Hide();
            }
        }

        private void ApplyRightDockLayout()
        {
            EnsureDockPanelsCreated();

            HideRightDockPanelsForRelayout();

            _entityInspector.Show(dockPanel, DockState.DockRight);
        }

        private void HideRightDockPanelsForRelayout()
        {
            DockContent[] rightPanels =
            {
                _entityInspector,
            };

            foreach (DockContent panel in rightPanels)
            {
                if (panel != null && panel.DockState != DockState.Hidden)
                    panel.Hide();
            }
        }

        private void EnsureRequiredDockLayout()
        {
            EnsureDockPanelsCreated();

            if (_compositeDisplay.DockState != DockState.Document)
                _compositeDisplay.Show(dockPanel, DockState.Document);

            if (!IsRightDockLayoutValid())
                ApplyRightDockLayout();

            if (!IsLeftDockLayoutValid())
                ApplyLeftDockLayout();

            _compositeDisplay?.EnsureInnerDockLayoutRestored();
        }

        private bool TryLoadDockLayout()
        {
            if (SettingsManager.GetInteger(Settings.MainDockLayoutVersion, 0) < CurrentMainDockLayoutVersion)
                return false;

            string layout = SettingsManager.GetString(Settings.MainDockLayout);
            if (string.IsNullOrWhiteSpace(layout))
                return false;

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(layout);
                using (MemoryStream stream = new MemoryStream(bytes))
                    dockPanel.LoadFromXml(stream, DeserializeDockContent);
                NormalizeMainDockPortionsAfterXmlLoad();
                UpdateMainDockAreaCache();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool IsMainDockLayoutValid()
        {
            EnsureDockPanelsCreated();

            return IsPanelDocked(_compositeDisplay, DockState.Document)
                && IsRightDockLayoutValid()
                && IsLeftDockLayoutValid();
        }

        private bool IsRightDockLayoutValid()
        {
            return IsPanelDocked(_entityInspector, DockState.DockRight);
        }

        private bool IsLeftDockLayoutValid()
        {
            if (!IsPanelDocked(_entitySearch, DockState.DockLeft)
                || !IsPanelDocked(_compositeBrowser, DockState.DockLeft)
                || !IsPanelDocked(_entityBrowser, DockState.DockLeft)
                || !IsPanelDocked(_entityList, DockState.DockLeft))
            {
                return false;
            }

            if (Singleton.ViewportEnabled)
            {
                if (!IsPanelDocked(_renderFiltersPanel, DockState.DockLeft))
                    return false;

                if (_renderFiltersPanel.Pane != _entitySearch.Pane)
                    return false;
            }
            else if (_renderFiltersPanel != null && _renderFiltersPanel.DockState != DockState.Hidden)
            {
                return false;
            }

            if (_entitySearch.Pane == null || _compositeBrowser.Pane == null)
                return false;

            if (_entityBrowser.Pane != _compositeBrowser.Pane || _entityList.Pane != _compositeBrowser.Pane)
                return false;

            if (_entitySearch.Pane == _compositeBrowser.Pane)
                return false;

            NestedDockingStatus browserStatus = _compositeBrowser.Pane.NestedDockingStatus;
            return browserStatus.PreviousPane == _entitySearch.Pane
                && browserStatus.Alignment == DockAlignment.Bottom;
        }

        private static bool IsPanelDocked(DockContent panel, DockState expectedState)
        {
            return panel != null && panel.DockState == expectedState;
        }

        private IDockContent DeserializeDockContent(string persistString)
        {
            EnsureDockPanelsCreated();

            switch (persistString)
            {
                case "CommandsDisplay":
                    return _compositeBrowser;
                case "EntityInspector":
                    return _entityInspector;
                case "EntityList":
                    return _entityList;
                case "CompositeDisplay":
                    return _compositeDisplay;
                case "CompositeBrowser":
                    return _compositeBrowser;
                case "EntityBrowser":
                    return _entityBrowser;
                case "EntitySearch":
                    return _entitySearch;
                case "RenderFiltersPanel":
                    return Singleton.ViewportEnabled ? _renderFiltersPanel : null;
                case "LevelViewerPanel":
                    return Singleton.ViewportEnabled ? _levelViewerPanel : null;
            }

            if (persistString == typeof(EntityInspector).ToString())
                return _entityInspector;
            if (persistString == typeof(EntityList).ToString())
                return _entityList;
            if (persistString == typeof(CompositeDisplay).ToString())
                return _compositeDisplay;
            if (persistString == typeof(CompositeBrowser).ToString())
                return _compositeBrowser;
            if (persistString == typeof(EntityBrowser).ToString())
                return _entityBrowser;
            if (persistString == typeof(EntitySearch).ToString())
                return _entitySearch;
            if (persistString == typeof(RenderFiltersPanel).ToString())
                return Singleton.ViewportEnabled ? _renderFiltersPanel : null;
            if (persistString == typeof(LevelViewerPanel).ToString())
                return Singleton.ViewportEnabled ? _levelViewerPanel : null;

            return null;
        }

        private void SaveDockLayout()
        {
            if (_compositeBrowser == null || dockPanel.Contents.Count == 0)
                return;

            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    dockPanel.SaveAsXml(stream, Encoding.UTF8);
                    SettingsManager.SetString(Settings.MainDockLayout, Encoding.UTF8.GetString(stream.ToArray()));
                    SettingsManager.SetInteger(Settings.MainDockLayoutVersion, CurrentMainDockLayoutVersion);
                }

                SaveMainDockPortionsToSettings();
                _compositeDisplay?.SaveInnerDockLayout();
            }
            catch { }
        }

        private void DockPanelContent_DockStateChanged(object sender, EventArgs e)
        {
            SaveDockLayout();
        }

        public CompositeDisplay LoadComposite(Composite composite, bool newDisplay = false)
        {
            if (composite == null || _compositeDisplay == null || _compositeDisplay.IsDisposed)
                return null;

            _compositeBrowser?.Content?.EnsureEditorUtils();

            if (!newDisplay && _compositeDisplay.Populated && _compositeDisplay.Composite == composite)
                return _compositeDisplay;

            if (newDisplay)
                _compositeDisplay.DepopulateUI();

            _compositeDisplay.PopulateUI(composite);
            _compositeDisplay.Show(dockPanel, DockState.Document);
            if (!ViewerSelectionSync.IsApplyingViewerSelection)
                _compositeDisplay.Activate();
            return _compositeDisplay;
        }

        private void CloseLevelPanels()
        {
            bool preserveViewer = _levelViewerPanel?.IsRunning == true;
            if (preserveViewer)
                _compositeDisplay?.HideLevelViewerPanelForLoad();
            else
                KillLevelViewer();

            SaveDockLayout();
            _compositeBrowser?.CloseAllChildTabs();
            CloseDockPanelContents(preserveLevelViewer: preserveViewer);

            if (_compositeBrowser != null)
            {
                _compositeBrowser.Resize -= _compositeBrowser_Resize;
                _compositeBrowser.FormClosed -= _compositeBrowser_FormClosed;
                _compositeBrowser.DockStateChanged -= DockPanelContent_DockStateChanged;
                _compositeBrowser.Close();
                _compositeBrowser.Dispose();
                _compositeBrowser = null;
            }
        }

        private void CloseDockPanelContents(bool preserveLevelViewer = false)
        {
            if (preserveLevelViewer)
                PreserveLevelViewerForLayoutReset();
            else
                DestroyLevelViewerPanel();

            ForceCloseDockContent(ref _compositeDisplay, CompositeDisplay_FormClosing);
            ForceCloseDockContent(ref _entityInspector, EntityInspector_FormClosing, _entityInspector_Resize);
            ForceCloseDockContent(ref _entityList, EntityList_FormClosing);
            ForceCloseDockContent(ref _entityBrowser, null);
            ForceCloseDockContent(ref _entitySearch, EntitySearch_FormClosing);
            ForceCloseDockContent(ref _renderFiltersPanel, RenderFiltersPanel_FormClosing);
        }

        private void PreserveLevelViewerForLayoutReset()
        {
            if (_levelViewerPanel == null || !_levelViewerPanel.IsRunning)
                return;

            _compositeDisplay?.ReleaseLevelViewerForLayoutReset();
        }

        private void DestroyLevelViewerPanel()
        {
            if (_levelViewerPanel == null)
                return;

            _compositeDisplay?.DetachLevelViewerPanel();
            _levelViewerPanel.ProcessExited -= LevelViewerPanel_ProcessExited;

            try
            {
                _levelViewerPanel.Stop();
                _levelViewerPanel.Hide();
                if (_levelViewerPanel.DockHandler.DockPanel != null)
                    _levelViewerPanel.DockHandler.Close();
            }
            catch
            {
            }

            try
            {
                _levelViewerPanel.Dispose();
            }
            catch
            {
            }

            _levelViewerPanel = null;
        }

        private void ForceCloseDockContent<T>(ref T content, FormClosingEventHandler formClosingHandler, EventHandler resizeHandler = null) where T : DockContent
        {
            if (content == null)
                return;

            T panel = content;
            content = null;

            if (formClosingHandler != null)
                panel.FormClosing -= formClosingHandler;
            if (resizeHandler != null)
                panel.Resize -= resizeHandler;
            panel.DockStateChanged -= DockPanelContent_DockStateChanged;

            if (panel is CompositeDisplay compositeDisplay)
                compositeDisplay.DepopulateUI();

            panel.Hide();
            if (panel.DockHandler.DockPanel != null)
                panel.DockHandler.Close();
            panel.Dispose();
        }

        private void _compositeBrowser_Resize(object sender, EventArgs e)
        {
            NormalizeMainDockPortionsToRatios();
            UpdateMainDockAreaCache();
            SaveMainDockPortionsToSettings();
            SaveDockLayout();
        }

        private void _entityInspector_Resize(object sender, EventArgs e)
        {
            NormalizeMainDockPortionsToRatios();
            UpdateMainDockAreaCache();
            SaveMainDockPortionsToSettings();
            SaveDockLayout();
        }

        private void _compositeBrowser_FormClosed(object sender, FormClosedEventArgs e)
        {
            CloseLevelPanels();
        }

        private void CompositeDisplay_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            ((CompositeDisplay)sender).DepopulateUI();
        }

        private void EntityInspector_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            ((EntityInspector)sender).DepopulateUI();
            Singleton.OnCompositeDisplayClosing?.Invoke(_compositeDisplay);
        }

        private void EntityList_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            ((EntityList)sender).Hide();
        }

        private void EntitySearch_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            ((EntitySearch)sender).Hide();
        }

        private void RenderFiltersPanel_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            ((RenderFiltersPanel)sender).Hide();
        }

        private void saveLevel_Click(object sender, EventArgs e)
        {
            SaveLevel();
        }

        public void SaveLevel(bool successMsg = true)
        {
            if (_compositeBrowser == null) return;

            //Close alien down if it's open, it conflicts with our write locks!
            EditorUtils.CloseAI();

            Cursor.Current = Cursors.WaitCursor;
            statusText.Text = "Saving...";
            statusStrip.Update();

            CloseProgressUI();
            EnsureProgressUI();
            _progressUI.ShowLevelSaving(_compositeBrowser.Content.Level);
            StartProgressKeepOnTop();

            if (_compositeDisplay != null)
                _compositeDisplay.SaveAllFlowgraphs();

#if DEBUG
            if (SettingsManager.GetBool(Settings.CompileInstances))
            {
                _compositeBrowser.Content.Level.Resources.Entries.Clear();
                _compositeBrowser.Content.Level.PhysicsMaps.Entries.Clear();
                //todo - clear others when i write them

                Instancing inst = new Instancing(_compositeBrowser.Content.Level);
                inst.GenerateInstances();
                inst.ProcessInstances();
            }
#endif

            //TODO: take a backup first
            _compositeBrowser.Content.Save();

            if (!_compositeBrowser.Content.Level.Commands.Compressed && SettingsManager.GetBool(Settings.SavePakAndBin))
            {
                string ext = "BIN";
                if (Path.GetExtension(_compositeBrowser.Content.Level.Commands.Filepath).ToUpper() == ".BIN")
                    ext = "PAK";
                _compositeBrowser.Content.Level.Commands.Save(_compositeBrowser.Content.Level.Commands.Filepath.Substring(0, _compositeBrowser.Content.Level.Commands.Filepath.Length - 3) + ext, false);
            }

#if !DEBUG
            PatchManager.PatchFileIntegrityCheck(Singleton.Platform, Singleton.PathToAI);
            PatchManager.PatchPopupMessage(Singleton.Platform, Singleton.PathToAI);
            PatchManager.UpdateLevelListInPackages(Singleton.Platform, Singleton.PathToAI);

            PatchManager.PatchSkipFrontendFlag(Singleton.Platform, Singleton.PathToAI, SettingsManager.GetBool(Settings.SkipFrontend));
            PatchManager.PatchNoUIFlag(Singleton.Platform, Singleton.PathToAI, SettingsManager.GetBool(Settings.HudDisabled));
            PatchManager.PatchMemReplayLogFlag(Singleton.Platform, Singleton.PathToAI, SettingsManager.GetBool(Settings.MemReplayLogs));
            PatchManager.PatchUIPerfFlag(Singleton.Platform, Singleton.PathToAI, SettingsManager.GetBool(Settings.UiEnabledUiPerf));

            if (SettingsManager.GetBool(Settings.LaunchGameWhenSaved))
            {
                PatchManager.PatchLaunchMode(Singleton.Platform, Singleton.PathToAI, _compositeBrowser.Content.Level.Name);

                if (Singleton.Platform == PatchManager.Platform.STEAM)
                {
                    Process.Start("steam://rungameid/214490");
                }
                else
                {
                    ProcessStartInfo alienProcess = new ProcessStartInfo();
                    alienProcess.WorkingDirectory = Singleton.PathToAI;
                    alienProcess.FileName = Singleton.PathToAI + "/AI.exe";
                    Process.Start(alienProcess);
                }
                Steam.UnlockAchievement(Steam.Achievements.LAUNCHED_GAME);
            }
#endif

            statusText.Text = "";
            Cursor.Current = Cursors.Default;
            CloseProgressUI();

            Singleton.OnSaved?.Invoke();
            Steam.UnlockAchievement(Steam.Achievements.FIRST_SAVE);

            int saveCount = SettingsManager.GetInteger(Settings.SaveCounter) + 1;
            SettingsManager.SetInteger(Settings.SaveCounter, saveCount);
            if (saveCount >= 100)
                Steam.UnlockAchievement(Steam.Achievements.ONE_HUNDRED_SAVES);

#if SHIP_BUILD
            if (saveCount > 10 && !SettingsManager.GetBool(Settings.DidSteamReviewPrompt))
            {
                SettingsManager.SetBool(Settings.DidSteamReviewPrompt, true);
                if (MessageBox.Show("" +
                    "Thanks for using OpenCAGE - don't forget to share your mods with the community on Discord!\n\n" +
                    "If you haven't already, please consider leaving the tools a review on Steam! It'd mean a lot!", "Thanks for using OpenCAGE!", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    Process.Start("https://store.steampowered.com/app/3367530/OpenCAGE/");
                }
            }
#endif

            //if (saved)
            //{
                if (SettingsManager.GetBool(Settings.ShowSavedMsgOpt) && successMsg)
                    MessageBox.Show("Saved changes!", "Saved.", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //}
            //else
            //    MessageBox.Show("Failed to save changes!", "Failed!", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public void EnableButtons(bool shouldEnable, string text)
        {
            try
            {
                if (toolStrip.InvokeRequired)
                    toolStrip.Invoke(new Action(() => { toolStrip.Enabled = shouldEnable; toolStrip.Refresh(); }));
                else
                    toolStrip.Enabled = shouldEnable; toolStrip.Refresh();

                if (statusStrip.InvokeRequired)
                    statusStrip.Invoke(new Action(() => { statusText.Text = text; statusStrip.Update(); }));
                else
                    statusText.Text = text; statusStrip.Update();
            }
            catch { }
        }

        private void ViewportOptionsDropdownOpening(object sender, EventArgs e)
        {
            EnsureDockPanelsCreated();
        }

        private void openLevelViewerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            KillLevelViewer();
            EnsureDockPanelsCreated();
            BeginParallelLevelViewerLoad(_compositeBrowser?.Content?.Level?.Name);
            toolStripButton2.HideDropDown();
        }

        private void LevelViewerPanel_ProcessExited(object sender, EventArgs e)
        {
            _compositeDisplay?.HideLevelViewerPanel();
        }

        //todo - post V18 i'm going to just support steam which will mean we ALWAYS have the viewer. can remove all this bloat.
        private void ConfigureLevelViewerAvailability()
        {
            if (!Singleton.ViewportEnabled)
            {
                viewportOptionsToolStripMenuItem.Visible = false;
                resetRenderFiltersOnLoadToolStripMenuItem.Visible = false;
                _renderFiltersPanel?.Hide();
                _compositeDisplay?.HideLevelViewerPanel();
                if (_entityInspector != null && dockPanel != null && dockPanel.Contents.Count > 0)
                    EnsureRequiredDockLayout();
            }
            else
            { 
                resetRenderFiltersOnLoadToolStripMenuItem.Visible = true;
                viewportOptionsToolStripMenuItem.Visible = true;
                EnsureLevelViewerConnection();
            }
        }

        private static bool EnsureLevelViewerConnection()
        {
            if (UnityConnection.Send.Started)
                return true;

            return UnityConnection.Send.Start();
        }

        private void highlightAliasesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            highlightAliasesToolStripMenuItem.Checked = !highlightAliasesToolStripMenuItem.Checked;
            SettingsManager.SetBool(Settings.HighlightAliases, highlightAliasesToolStripMenuItem.Checked);
            UnityConnection.Send.SendSettingsPacket();
        }

        private void highlightProxiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            highlightProxiesToolStripMenuItem.Checked = !highlightProxiesToolStripMenuItem.Checked;
            SettingsManager.SetBool(Settings.HighlightProxies, highlightProxiesToolStripMenuItem.Checked);
            UnityConnection.Send.SendSettingsPacket();
        }

        private void showCameraPositionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            showCameraPositionToolStripMenuItem.Checked = !showCameraPositionToolStripMenuItem.Checked;
            SettingsManager.SetBool(Settings.ShowCameraPosition, showCameraPositionToolStripMenuItem.Checked);
            UnityConnection.Send.SendSettingsPacket();
        }

        private void renderWireframeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            renderWireframeToolStripMenuItem.Checked = !renderWireframeToolStripMenuItem.Checked;
            SettingsManager.SetBool(Settings.RenderWireframe, renderWireframeToolStripMenuItem.Checked);
            UnityConnection.Send.SendSettingsPacket();
        }

        private void hideNestedScriptEntitiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            hideNestedScriptEntitiesToolStripMenuItem.Checked = !hideNestedScriptEntitiesToolStripMenuItem.Checked;
            SettingsManager.SetBool(Settings.HideNestedScriptEntities, hideNestedScriptEntitiesToolStripMenuItem.Checked);
            UnityConnection.Send.SendSettingsPacket();
        }

        private void resetRenderFiltersOnLoadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            resetRenderFiltersOnLoadToolStripMenuItem.Checked = !resetRenderFiltersOnLoadToolStripMenuItem.Checked;
            SettingsManager.SetBool(Settings.ResetRenderFilters, resetRenderFiltersOnLoadToolStripMenuItem.Checked);
        }

        private bool _levelViewerToolbarConfigured;

        private void EnsureLevelViewerToolbarConfigured()
        {
            if (_levelViewerToolbarConfigured || _levelViewerPanel == null)
                return;

            SetupLevelViewerPanelToolbar();
            _levelViewerToolbarConfigured = true;
        }

        private void SetupLevelViewerPanelToolbar()
        {
            if (_levelViewerPanel == null)
                return;

            SetupTransformGridSnapMenu(_levelViewerPanel.PanelTransformGridSnapMenu);
            SetupRotationSnapMenu(_levelViewerPanel.PanelRotationSnapMenu);
            ApplyTransformSnapSelectionsFromSettings();

            _levelViewerPanel.SelectionModeChanged -= LevelViewerPanel_SelectionModeChanged;
            _levelViewerPanel.SelectionModeChanged += LevelViewerPanel_SelectionModeChanged;
            _levelViewerPanel.GizmoModeChanged -= LevelViewerPanel_GizmoModeChanged;
            _levelViewerPanel.GizmoModeChanged += LevelViewerPanel_GizmoModeChanged;
            ApplyLevelViewerViewportModesFromSettings();
        }

        private void ApplyLevelViewerViewportModesFromSettings()
        {
            if (_levelViewerPanel == null)
                return;

            _levelViewerPanel.ApplySelectionMode(LevelViewerViewportDefinitions.NormalizeDeepSelectMode(
                SettingsManager.GetInteger(Settings.LevelViewerDeepSelectMode)));
            _levelViewerPanel.ApplyGizmoMode(LevelViewerViewportDefinitions.NormalizeGizmoMode(
                SettingsManager.GetInteger(Settings.LevelViewerGizmoMode)));
        }

        private void LevelViewerPanel_SelectionModeChanged(object sender, LevelViewerDeepSelectMode mode)
        {
            SettingsManager.SetInteger(Settings.LevelViewerDeepSelectMode, (int)mode);
            UnityConnection.Send.SendSettingsPacket();
        }

        private void LevelViewerPanel_GizmoModeChanged(object sender, LevelViewerGizmoMode mode)
        {
            SettingsManager.SetInteger(Settings.LevelViewerGizmoMode, (int)mode);
            UnityConnection.Send.SendSettingsPacket();
        }

        private void ApplyTransformSnapSelectionsFromSettings()
        {
            if (!SettingsManager.IsSet(Settings.TransformGridSnap))
                SettingsManager.SetFloat(Settings.TransformGridSnap, 0f);
            if (!SettingsManager.IsSet(Settings.RotationSnapDegrees))
                SettingsManager.SetFloat(Settings.RotationSnapDegrees, 0f);

            ApplyTransformGridSnapSelection(TransformSnapDefinitions.NormalizeGridSnap(
                SettingsManager.GetFloat(Settings.TransformGridSnap)));
            ApplyRotationSnapSelection(TransformSnapDefinitions.NormalizeRotationSnap(
                SettingsManager.GetFloat(Settings.RotationSnapDegrees)));
        }

        private void SetupTransformGridSnapMenu(ToolStripDropDownButton parent)
        {
            if (parent == null)
                return;

            parent.DropDownItems.Clear();
            _transformGridSnapMenuItems.Clear();

            foreach (float value in TransformSnapDefinitions.GridSnapValues)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(TransformSnapDefinitions.FormatGridSnapLabel(value))
                {
                    CheckOnClick = false,
                    Tag = value,
                };
                item.Click += TransformGridSnapMenuItem_Click;
                _transformGridSnapMenuItems[value] = item;
                parent.DropDownItems.Add(item);
            }
        }

        private void SetupRotationSnapMenu(ToolStripDropDownButton parent)
        {
            if (parent == null)
                return;

            parent.DropDownItems.Clear();
            _rotationSnapMenuItems.Clear();

            foreach (float value in TransformSnapDefinitions.RotationSnapValues)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(TransformSnapDefinitions.FormatRotationSnapLabel(value))
                {
                    CheckOnClick = false,
                    Tag = value,
                };
                item.Click += RotationSnapMenuItem_Click;
                _rotationSnapMenuItems[value] = item;
                parent.DropDownItems.Add(item);
            }
        }

        private void ApplyTransformGridSnapSelection(float value)
        {
            foreach (KeyValuePair<float, ToolStripMenuItem> entry in _transformGridSnapMenuItems)
                entry.Value.Checked = SnapValuesEqual(entry.Key, value);

            if (_levelViewerPanel?.PanelTransformGridSnapMenu != null)
            {
                _levelViewerPanel.PanelTransformGridSnapMenu.Text = "Transform Snap: "
                    + TransformSnapDefinitions.FormatGridSnapLabel(value);
            }
        }

        private void ApplyRotationSnapSelection(float value)
        {
            foreach (KeyValuePair<float, ToolStripMenuItem> entry in _rotationSnapMenuItems)
                entry.Value.Checked = SnapValuesEqual(entry.Key, value);

            if (_levelViewerPanel?.PanelRotationSnapMenu != null)
            {
                _levelViewerPanel.PanelRotationSnapMenu.Text = "Rotation Snap: "
                    + TransformSnapDefinitions.FormatRotationSnapLabel(value);
            }
        }

        private static bool SnapValuesEqual(float left, float right)
        {
            return Math.Abs(left - right) < 0.0001f;
        }

        private void TransformGridSnapMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;
            float value = (float)item.Tag;
            value = TransformSnapDefinitions.NormalizeGridSnap(value);
            ApplyTransformGridSnapSelection(value);
            SettingsManager.SetFloat(Settings.TransformGridSnap, value);
            UnityConnection.Send.SendSettingsPacket();
        }

        private void RotationSnapMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;
            float value = (float)item.Tag;
            value = TransformSnapDefinitions.NormalizeRotationSnap(value);
            ApplyRotationSnapSelection(value);
            SettingsManager.SetFloat(Settings.RotationSnapDegrees, value);
            UnityConnection.Send.SendSettingsPacket();
        }

        private void connectToRuntimeUtils_Click(object sender, EventArgs e)
        {
            connectToRuntimeUtils.Checked = !connectToRuntimeUtils.Checked;
            SettingsManager.SetBool(Settings.RuntimeUtilsOpt, connectToRuntimeUtils.Checked);

            if (connectToRuntimeUtils.Checked)
            {
                if (!RuntimeUtilsConnection.Send.Start())
                {
                    connectToRuntimeUtils.PerformClick();
                    MessageBox.Show("Failed to connect to RuntimeUtils server.\nIs the game running with the RuntimeUtils DLL loaded?", "Connection failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                RuntimeUtilsConnection.Send.Stop();
            }
        }

        private void KillLevelViewer()
        {
            if (_levelViewerPanel == null)
                return;

            _levelViewerPanel.Stop();
            _compositeDisplay?.HideLevelViewerPanel();
        }

        private void showEntityIDs_Click(object sender, EventArgs e)
        {
            showEntityIDs.Checked = !showEntityIDs.Checked;
            SettingsManager.SetBool(Settings.ShowShortGuids, showEntityIDs.Checked);

            _compositeBrowser?.Reload(true);
            _entitySearch?.InitializeFromLevel();
            //TODO: also reload hierarchy cache
        }

        private void searchOnlyCompositeNames_Click(object sender, EventArgs e)
        {
            searchOnlyCompositeNames.Checked = !searchOnlyCompositeNames.Checked;
            SettingsManager.SetBool(Settings.CompNameOnlyOpt, searchOnlyCompositeNames.Checked);
        }

        private void showConfirmationWhenSavingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            showConfirmationWhenSavingToolStripMenuItem.Checked = !showConfirmationWhenSavingToolStripMenuItem.Checked;
            SettingsManager.SetBool(Settings.ShowSavedMsgOpt, showConfirmationWhenSavingToolStripMenuItem.Checked);
        }

        private void useTexturedModelViewExperimentalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            useTexturedModelViewExperimentalToolStripMenuItem.Checked = !useTexturedModelViewExperimentalToolStripMenuItem.Checked;
            SettingsManager.SetBool(Settings.ShowTexOpt, useTexturedModelViewExperimentalToolStripMenuItem.Checked);
        }

        private void showExplorerViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            showExplorerViewToolStripMenuItem.Checked = !showExplorerViewToolStripMenuItem.Checked;
            SettingsManager.SetBool(Settings.EnableFileBrowser, showExplorerViewToolStripMenuItem.Checked);
            UpdateCompositeBrowserDockState();
        }

        private void keepFunctionUsesWindowOpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            keepFunctionUsesWindowOpenToolStripMenuItem.Checked = !keepFunctionUsesWindowOpenToolStripMenuItem.Checked;
            SettingsManager.SetBool(Settings.KeepUsesWindowOpen, keepFunctionUsesWindowOpenToolStripMenuItem.Checked);
        }

        SetNumericStep numericStepConfig = null;
        private void setNumericStepToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (numericStepConfig != null)
            {
                numericStepConfig.Close();
            }
            numericStepConfig = new SetNumericStep();
            numericStepConfig.Show();
        }

        private void savePAKAndBINToolStripMenuItem_Click(object sender, EventArgs e)
        {
            savePAKAndBINToolStripMenuItem.Checked = !savePAKAndBINToolStripMenuItem.Checked;
            SettingsManager.SetBool(Settings.SavePakAndBin, savePAKAndBINToolStripMenuItem.Checked);
        }

        private void populateAllNodePinsWhenCreatedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            populateAllNodePinsWhenCreatedToolStripMenuItem.Checked = !populateAllNodePinsWhenCreatedToolStripMenuItem.Checked;
            SettingsManager.SetBool(Settings.PopulateAllPinsOnCreateNode, populateAllNodePinsWhenCreatedToolStripMenuItem.Checked);
        }

        private void giveOptionToDeleteEntityWhenNoNodesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            giveOptionToDeleteEntityWhenNoNodesToolStripMenuItem.Checked = !giveOptionToDeleteEntityWhenNoNodesToolStripMenuItem.Checked;
            SettingsManager.SetBool(Settings.OptionToDeleteEntityWithNode, giveOptionToDeleteEntityWhenNoNodesToolStripMenuItem.Checked);
        }

        private void resetUILayoutsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Normal;
            Width = _defaultWidth;
            Height = _defaultHeight;

            bool preserveLevelViewer = _levelViewerPanel?.IsRunning == true;

            SettingsManager.SetString(Settings.MainDockLayout, "");
            SettingsManager.SetInteger(Settings.MainDockLayoutVersion, 0);
            SettingsManager.SetInteger(Settings.CompositeDisplayDockLayoutVersion, 0);
            if (!preserveLevelViewer)
                SettingsManager.SetString(Settings.LevelViewerPanelDockState, DockState.Hidden.ToString());
            SettingsManager.SetFloat(Settings.CompositeDisplayDockTopPortion, 0.35f);
            SettingsManager.SetFloat(Settings.SplitWidthMainRight, DefaultSideDockPortion);
            SettingsManager.SetFloat(Settings.EntityInspectorWidth, DefaultEntityInspectorPortion);
            SettingsManager.SetFloat(Settings.SplitWidthMainBottom, _defaultSplitterDistance);
            SettingsManager.SetInteger(Settings.CompositeSplitWidth, 0);

            Composite loadedComposite = _compositeDisplay?.Populated == true ? _compositeDisplay.Composite : null;
            LevelContent loadedContent = _compositeBrowser?.Content;
            bool levelDataLoaded = loadedContent?.IsLevelDataLoaded == true;

            if (_compositeBrowser != null)
            {
                try
                {
                    _compositeBrowser.Hide();
                    CloseDockPanelContents(preserveLevelViewer);
                    EnsureDockPanelsCreated();
                    ApplyDefaultDockLayout(resetInnerDock: !preserveLevelViewer);
                    UpdateCompositeBrowserDockState();

                    if (levelDataLoaded)
                    {
                        _entityBrowser.InitializeFromLevel();
                        _entityList.UpdateTitle();
                        _entitySearch.InitializeFromLevel();
                        _renderFiltersPanel.RefreshFilters();
                    }

                    if (loadedComposite != null)
                        LoadComposite(loadedComposite);
                    else if (levelDataLoaded)
                        _compositeBrowser.LoadInitialComposite();

                    _entityList.FocusPanel();

                    if (preserveLevelViewer)
                    {
                        SettingsManager.SetString(
                            Settings.LevelViewerPanelDockState,
                            DockState.DockTop.ToString());
                        _compositeDisplay.RepositionLevelViewerForLayoutReset();
                        BeginInvoke(new Action(() =>
                        {
                            _levelViewerPanel?.RefreshEmbeddedBounds();
                            _levelViewerPanel?.RestoreInputFocus();
                        }));
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log("UI Layout", "Reset UI layouts failed: " + ex);
                    MessageBox.Show(
                        "Reset UI layouts encountered an error.\n" + ex.Message,
                        "Reset UI Layouts",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            else
            {
                dockPanel.DockLeftPortion = DefaultSideDockPortion;
                dockPanel.DockRightPortion = DefaultEntityInspectorPortion;
                dockPanel.DockBottomPortion = _defaultSplitterDistance;
            }

            _compositeBrowser?.ResetSplitter();
        }

        private void writeInstancedResourcesExperimentalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            writeInstancedResourcesExperimentalToolStripMenuItem.Checked = !writeInstancedResourcesExperimentalToolStripMenuItem.Checked;
            SettingsManager.SetBool(Settings.CompileInstances, writeInstancedResourcesExperimentalToolStripMenuItem.Checked);
        }

        private void UpdateCompositeBrowserDockState()
        {
            if (_compositeBrowser == null)
            {
                Singleton.Editor.DockPanel.ActiveAutoHideContent = null;
                return;
            }
            _compositeBrowser.UpdateDockState();
        }

        private void helpBtn_Click(object sender, EventArgs e)
        {
            Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
            Process.Start("https://opencage.co.uk/docs/");
        }

        private void DEBUG_ReloadLevel_Click(object sender, EventArgs e)
        {
            if (!RuntimeUtilsConnection.Send.Connected)
            {
                MessageBox.Show("Cannot reload level - not connected to RuntimeUtils", "Not connected", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            RuntimeUtilsConnection.Send.SendData(new RuntimeUtilsConnection.Packet() { load_level = "Production/HAB_Airport" });
        }

        private void openGameOnSaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openGameOnSaveToolStripMenuItem.Checked = !openGameOnSaveToolStripMenuItem.Checked;
            SettingsManager.SetBool(Settings.LaunchGameWhenSaved, openGameOnSaveToolStripMenuItem.Checked);
        }

        private void showGamePlatformToolStripMenuItem_Click(object sender, EventArgs e)
        {
            showGamePlatformToolStripMenuItem.Checked = !showGamePlatformToolStripMenuItem.Checked;
            SettingsManager.SetBool(Settings.ShowGamePlatform, showGamePlatformToolStripMenuItem.Checked);
            UpdateTitle();
        }

        private void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
        }

        EditModel _modelEditor = null;
        private void modelsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_modelEditor != null)
            {
                _modelEditor.FormClosed -= _modelEditor_FormClosed;
                _modelEditor.Close();
            }

            _modelEditor = new EditModel(null, false);
            _modelEditor.Show();
            _modelEditor.FormClosed += _modelEditor_FormClosed;
        }
        private void _modelEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _modelEditor = null;
        }

        EditMaterial _materialEditor = null;
        private void materialsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_materialEditor != null)
            {
                _materialEditor.FormClosed -= _materialEditor_FormClosed;
                _materialEditor.Close();
            }

            _materialEditor = new EditMaterial(null, false);
            _materialEditor.Show();
            _materialEditor.FormClosed += _materialEditor_FormClosed;
        }
        private void _materialEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _materialEditor = null;
        }

        EditMaterialMapping _materialMappingEditor = null;
        private void materialMappingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_materialMappingEditor != null)
            {
                _materialMappingEditor.FormClosed -= _materialMappingEditor_FormClosed;
                _materialMappingEditor.Close();
            }

            _materialMappingEditor = new EditMaterialMapping(null, false);
            _materialMappingEditor.Show();
            _materialMappingEditor.FormClosed += _materialMappingEditor_FormClosed;
        }
        private void _materialMappingEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _materialMappingEditor = null;
        }

        EditTexture _textureEditor = null;
        private void texturesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_textureEditor != null)
            {
                _textureEditor.FormClosed -= _textureEditor_FormClosed;
                _textureEditor.Close();
            }

            _textureEditor = new EditTexture(null, false);
            _textureEditor.Show();
            _textureEditor.FormClosed += _textureEditor_FormClosed;
        }
        private void _textureEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _textureEditor = null;
        }

        GalaxyEditor _galaxyEditor = null;
        private void galaxyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_galaxyEditor != null)
            {
                _galaxyEditor.FormClosed -= _galaxyEditor_FormClosed;
                _galaxyEditor.Close();
            }

            _galaxyEditor = new GalaxyEditor();
            _galaxyEditor.Show();
            _galaxyEditor.FormClosed += _galaxyEditor_FormClosed;
        }
        private void _galaxyEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _galaxyEditor = null;
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            modelsToolStripMenuItem.Enabled = _compositeBrowser?.Content?.Level != null;
            materialsToolStripMenuItem.Enabled = _compositeBrowser?.Content?.Level != null;
            materialMappingsToolStripMenuItem.Enabled = _compositeBrowser?.Content?.Level != null;
            texturesToolStripMenuItem.Enabled = _compositeBrowser?.Content?.Level != null;
            galaxyToolStripMenuItem.Enabled = _compositeBrowser?.Content?.Level != null;
        }

        private void charactersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            assetSetsToolStripMenuItem.Enabled = _compositeBrowser?.Content?.Level != null;
        }

        SetNodeColours _setNodeColours;
        private void setNodeColoursToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_setNodeColours != null)
                _setNodeColours.Close();

            _setNodeColours = new SetNodeColours();
            _setNodeColours.Show();
        }

        private void showConfirmationWhenDeletingNodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            showConfirmationWhenDeletingNodeToolStripMenuItem.Checked = !showConfirmationWhenDeletingNodeToolStripMenuItem.Checked;
            SettingsManager.SetBool(Settings.AskBeforeDeletingNode, showConfirmationWhenDeletingNodeToolStripMenuItem.Checked);
        }

        private void miscToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //NOTE: When in compressed mode, we ALWAYS save in the BIN format, so hide this option in that case.
            savePAKAndBINToolStripMenuItem.Visible = _compositeBrowser?.Content?.Level?.Commands != null && _compositeBrowser.Content.Level.Commands.Compressed;

            //NOTE: We don't actually allow this to be changed (even though it could be done) because it's not much use, for now at least. Maybe some sort of conversion between compressed and uncompressed levels in future.
            writeCompressedToolStripMenuItem.Checked = _compositeBrowser?.Content?.Level?.Commands != null && _compositeBrowser.Content.Level.Commands.Compressed;
            writeCompressedToolStripMenuItem.Enabled = false; 
        }

        ControlsWindow _controlsWindow = null;
        private void controlsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_controlsWindow != null)
            {
                _controlsWindow.FormClosed -= _controlsWindow_FormClosed;
                _controlsWindow.Close();
            }

            _controlsWindow = new ControlsWindow();
            _controlsWindow.Show();
            _controlsWindow.FormClosed += _controlsWindow_FormClosed;
        }
        private void _controlsWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            _controlsWindow = null;
        }

        LaunchGame _launchGamePopup = null;
        private void launchGameBtn_Click(object sender, EventArgs e)
        {
            if (_launchGamePopup != null)
            {
                _launchGamePopup.FormClosed -= _launchGamePopup_FormClosed;
                _launchGamePopup.Close();
            }

            _launchGamePopup = new LaunchGame();
            _launchGamePopup.Show();
            _launchGamePopup.FormClosed += _launchGamePopup_FormClosed;
        }
        private void _launchGamePopup_FormClosed(object sender, FormClosedEventArgs e)
        {
            _launchGamePopup = null;
        }

        EditPAK2 _editUiPak = null;
        private void uIToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_editUiPak != null)
            {
                _editUiPak.FormClosed -= _editUiPak_FormClosed;
                _editUiPak.Close();
            }

            _editUiPak = new EditPAK2();
            _editUiPak.Show();
            _editUiPak.LoadPAK2("UI.PAK", "UI");
            _editUiPak.FormClosed += _editUiPak_FormClosed;
        }
        private void _editUiPak_FormClosed(object sender, FormClosedEventArgs e)
        {
            _editUiPak = null;
        }

        //todo - eventually will want to expand this for anim trees and better handling of data (previews?)
        EditPAK2 _editAnimations = null;
        private void animationsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_editAnimations != null)
            {
                _editAnimations.FormClosed -= _editAnimations_FormClosed ;
                _editAnimations.Close();
            }

            _editAnimations = new EditPAK2();
            _editAnimations.Show();
            _editAnimations.LoadPAK2("GLOBAL/ANIMATION.PAK", "Animations");
            _editAnimations.FormClosed += _editAnimations_FormClosed;
        }
        private void _editAnimations_FormClosed(object sender, FormClosedEventArgs e)
        {
            _editAnimations = null;
        }

        Process _behaviourEditor = null;
        private void behaviourTreesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            KillBehaviourTreeEditor();

            string editorPath = Singleton.PathToAI + "/DATA/MODTOOLS/REMOTE_ASSETS/legendplugin/";
            _behaviourEditor = Process.Start(new ProcessStartInfo
                {
                    FileName = editorPath + "BehaviourTreeEditor.exe",
                    Arguments = "-pathToAI=\"" + Singleton.PathToAI + "\"",
                    WorkingDirectory = editorPath,
                }
            );

            Steam.UnlockAchievement(Steam.Achievements.BEHAVIOUR_TREE_TOOL_LAUNCHED);
        }

        private void KillBehaviourTreeEditor()
        {
            if (_behaviourEditor != null)
            {
                try
                {
                    _behaviourEditor?.Kill();
                    _behaviourEditor?.Close();
                }
                catch { }
            }
        }

        LevelBackupManager _levelBackups = null;
        private void manageBackupsBtn_Click(object sender, EventArgs e)
        {
            if (_levelBackups != null)
            {
                _levelBackups.FormClosed -= _levelBackups_FormClosed;
                _levelBackups.Close();
            }

            _levelBackups = new LevelBackupManager();
            _levelBackups.Show();
            _levelBackups.FormClosed += _levelBackups_FormClosed;
        }
        private void _levelBackups_FormClosed(object sender, FormClosedEventArgs e)
        {
            _levelBackups = null;
        }

        #region Config Editors
        HackingEditor _hackToolEditor = null;
        private void hackToolDifficultiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_hackToolEditor != null)
            {
                _hackToolEditor.FormClosed -= _hackToolEditor_FormClosed;
                _hackToolEditor.Close();
            }

            _hackToolEditor = new HackingEditor();
            _hackToolEditor.Show();
            _hackToolEditor.FormClosed += _hackToolEditor_FormClosed;
        }
        private void _hackToolEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _hackToolEditor = null;
        }

        LoadMovieEditor _loadMovieEditor = null;
        private void loadscreenMoviesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_loadMovieEditor != null)
            {
                _loadMovieEditor.FormClosed -= _loadMovieEditor_FormClosed;
                _loadMovieEditor.Close();
            }

            _loadMovieEditor = new LoadMovieEditor();
            _loadMovieEditor.Show();
            _loadMovieEditor.FormClosed += _loadMovieEditor_FormClosed;
        }
        private void _loadMovieEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _loadMovieEditor = null;
        }

        BlueprintEditor _blueprintEditor = null;
        private void blueprintRecipesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_blueprintEditor != null)
            {
                _blueprintEditor.FormClosed -= _blueprintEditor_FormClosed;
                _blueprintEditor.Close();
            }

            _blueprintEditor = new BlueprintEditor();
            _blueprintEditor.Show();
            _blueprintEditor.FormClosed += _blueprintEditor_FormClosed;
        }
        private void _blueprintEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _blueprintEditor = null;
        }

        AmmoEditor _ammoEditor = null;
        private void ammoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_ammoEditor != null)
            {
                _ammoEditor.FormClosed -= _ammoEditor_FormClosed;
                _ammoEditor.Close();
            }

            _ammoEditor = new AmmoEditor();
            _ammoEditor.Show();
            _ammoEditor.FormClosed += _ammoEditor_FormClosed;
        }
        private void _ammoEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _ammoEditor = null;
        }

        RadiosityEditor _radiosityEditor = null;
        private void radiosityToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_radiosityEditor != null)
            {
                _radiosityEditor.FormClosed -= _radiosityEditor_FormClosed;
                _radiosityEditor.Close();
            }

            _radiosityEditor = new RadiosityEditor();
            _radiosityEditor.Show();
            _radiosityEditor.FormClosed += _radiosityEditor_FormClosed;
        }
        private void _radiosityEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _radiosityEditor = null;
        }

        GlobalConstantsEditor _globalConstEditor = null;
        private void globalConstantsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_globalConstEditor != null)
            {
                _globalConstEditor.FormClosed -= _globalConstEditor_FormClosed;
                _globalConstEditor.Close();
            }

            _globalConstEditor = new GlobalConstantsEditor();
            _globalConstEditor.Show();
            _globalConstEditor.FormClosed += _globalConstEditor_FormClosed;
        }
        private void _globalConstEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _globalConstEditor = null;
        }

        LocomotionEditor _locomotionEditor = null;
        private void locomotionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_locomotionEditor != null)
            {
                _locomotionEditor.FormClosed -= _locomotionEditor_FormClosed;
                _locomotionEditor.Close();
            }

            _locomotionEditor = new LocomotionEditor();
            _locomotionEditor.Show();
            _locomotionEditor.FormClosed += _locomotionEditor_FormClosed;
        }
        private void _locomotionEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _locomotionEditor = null;
        }

        AlienConfigEditor _alienConfigEditor = null;
        private void alienConfigsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_alienConfigEditor != null)
            {
                _alienConfigEditor.FormClosed -= _alienConfigEditor_FormClosed;
                _alienConfigEditor.Close();
            }

            _alienConfigEditor = new AlienConfigEditor();
            _alienConfigEditor.Show();
            _alienConfigEditor.FormClosed += _alienConfigEditor_FormClosed;
        }
        private void _alienConfigEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _alienConfigEditor = null;
        }

        ViewconeEditor _viewconeEditor = null;
        private void viewconesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_viewconeEditor != null)
            {
                _viewconeEditor.FormClosed -= _viewconeEditor_FormClosed;
                _viewconeEditor.Close();
            }

            _viewconeEditor = new ViewconeEditor();
            _viewconeEditor.Show();
            _viewconeEditor.FormClosed += _viewconeEditor_FormClosed;
        }
        private void _viewconeEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _viewconeEditor = null;
        }

        SenseEditor _senseEditor = null;
        private void sensesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_senseEditor != null)
            {
                _senseEditor.FormClosed -= _senseEditor_FormClosed;
                _senseEditor.Close();
            }

            _senseEditor = new SenseEditor();
            _senseEditor.Show();
            _senseEditor.FormClosed += _senseEditor_FormClosed;
        }
        private void _senseEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _senseEditor = null;
        }

        AttributesEditor _attributesEditor = null;
        private void attributesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_attributesEditor != null)
            {
                _attributesEditor.FormClosed -= _attributesEditor_FormClosed;
                _attributesEditor.Close();
            }

            _attributesEditor = new AttributesEditor();
            _attributesEditor.Show();
            _attributesEditor.FormClosed += _attributesEditor_FormClosed;
        }
        private void _attributesEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _attributesEditor = null;
        }

        VoiceMappingEditor _voiceMapEditor = null;
        private void voiceMappingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_voiceMapEditor != null)
            {
                _voiceMapEditor.FormClosed -= _voiceMapEditor_FormClosed;
                _voiceMapEditor.Close();
            }

            _voiceMapEditor = new VoiceMappingEditor();
            _voiceMapEditor.Show();
            _voiceMapEditor.FormClosed += _voiceMapEditor_FormClosed;
        }
        private void _voiceMapEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _voiceMapEditor = null;
        }

        CharacterAssetEditor _charAssetEditor = null;
        private void assetSetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_charAssetEditor != null)
            {
                _charAssetEditor.FormClosed -= _charAssetEditor_FormClosed;
                _charAssetEditor.Close();
            }

            _charAssetEditor = new CharacterAssetEditor();
            _charAssetEditor.Show();
            _charAssetEditor.FormClosed += _charAssetEditor_FormClosed;
        }
        private void _charAssetEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _charAssetEditor = null;
        }

        PhysicalMaterialEditor _physicalMatEditor = null;
        private void physicalMaterialsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_physicalMatEditor != null)
            {
                _physicalMatEditor.FormClosed -= _physicalMatEditor_FormClosed;
                _physicalMatEditor.Close();
            }

            _physicalMatEditor = new PhysicalMaterialEditor();
            _physicalMatEditor.Show();
            _physicalMatEditor.FormClosed += _physicalMatEditor_FormClosed;
        }
        private void _physicalMatEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _physicalMatEditor = null;
        }

        ScriptReadableVariableEditor _scriptVariableEditor = null;
        private void scriptReadableVariablesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_scriptVariableEditor != null)
            {
                _scriptVariableEditor.FormClosed -= _scriptVariableEditor_FormClosed;
                _scriptVariableEditor.Close();
            }

            _scriptVariableEditor = new ScriptReadableVariableEditor();
            _scriptVariableEditor.Show();
            _scriptVariableEditor.FormClosed += _scriptVariableEditor_FormClosed;
        }
        private void _scriptVariableEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _scriptVariableEditor = null;
        }

        PermanentSoundbankEditor _permaSoundbankEditor = null;
        private void permanentSoundbanksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_permaSoundbankEditor != null)
            {
                _permaSoundbankEditor.FormClosed -= _permaSoundbankEditor_FormClosed;
                _permaSoundbankEditor.Close();
            }

            _permaSoundbankEditor = new PermanentSoundbankEditor();
            _permaSoundbankEditor.Show();
            _permaSoundbankEditor.FormClosed += _permaSoundbankEditor_FormClosed;
        }
        private void _permaSoundbankEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _permaSoundbankEditor = null;
        }

        HairAndSkinShadingEditor _hairShadingEditor = null;
        private void hairShadingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_hairShadingEditor != null)
            {
                _hairShadingEditor.FormClosed -= _hairShadingEditor_FormClosed;
                _hairShadingEditor.Close();
            }

            _hairShadingEditor = new HairAndSkinShadingEditor();
            _hairShadingEditor.Show();
            _hairShadingEditor.FormClosed += _hairShadingEditor_FormClosed;
        }
        private void _hairShadingEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _hairShadingEditor = null;
        }

        InputsEditor _inputsEditor = null;
        private void inputsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_inputsEditor != null)
            {
                _inputsEditor.FormClosed -= _inputsEditor_FormClosed;
                _inputsEditor.Close();
            }

            _inputsEditor = new InputsEditor();
            _inputsEditor.Show();
            _inputsEditor.FormClosed += _inputsEditor_FormClosed;
        }
        private void _inputsEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _inputsEditor = null;
        }

        LocalisationEditor _localisationEditor = null;
        private void localisationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_localisationEditor != null)
            {
                _localisationEditor.FormClosed -= _localisationEditor_FormClosed;
                _localisationEditor.Close();
            }

            _localisationEditor = new LocalisationEditor();
            _localisationEditor.Show();
            _localisationEditor.FormClosed += _localisationEditor_FormClosed;
        }
        private void _localisationEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _localisationEditor = null;
        }

        LevelTextDBEditor _levelTextDBEditor = null;
        private void levelTextDBsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_levelTextDBEditor != null)
            {
                _levelTextDBEditor.FormClosed -= _levelTextDBEditor_FormClosed;
                _levelTextDBEditor.Close();
            }

            _levelTextDBEditor = new LevelTextDBEditor();
            _levelTextDBEditor.Show();
            _levelTextDBEditor.FormClosed += _levelTextDBEditor_FormClosed;
        }
        private void _levelTextDBEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _levelTextDBEditor = null;
        }

        FontConfigEditor _fontConfigEditor = null;
        private void fontConfigToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_fontConfigEditor != null)
            {
                _fontConfigEditor.FormClosed -= _fontConfigEditor_FormClosed;
                _fontConfigEditor.Close();
            }

            _fontConfigEditor = new FontConfigEditor();
            _fontConfigEditor.Show();
            _fontConfigEditor.FormClosed += _fontConfigEditor_FormClosed;
        }
        private void _fontConfigEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _fontConfigEditor = null;
        }

        InventoryItemEditor _inventoryItemEditor = null;
        private void inventoryItemsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_inventoryItemEditor != null)
            {
                _inventoryItemEditor.FormClosed -= _inventoryItemEditor_FormClosed;
                _inventoryItemEditor.Close();
            }

            _inventoryItemEditor = new InventoryItemEditor();
            _inventoryItemEditor.Show();
            _inventoryItemEditor.FormClosed += _inventoryItemEditor_FormClosed;
        }
        private void _inventoryItemEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _inventoryItemEditor = null;
        }

        DifficultyEditor _difficultyEditor = null;
        private void difficultyModifiersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_difficultyEditor != null)
            {
                _difficultyEditor.FormClosed -= _difficultyEditor_FormClosed;
                _difficultyEditor.Close();
            }

            _difficultyEditor = new DifficultyEditor();
            _difficultyEditor.Show();
            _difficultyEditor.FormClosed += _difficultyEditor_FormClosed;
        }
        private void _difficultyEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            _difficultyEditor = null;
        }
        #endregion

        private About _aboutWindow = null;
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_aboutWindow != null)
            {
                _aboutWindow.FormClosed -= _aboutWindow_FormClosed;
                _aboutWindow.Close();
            }

            _aboutWindow = new About();
            _aboutWindow.Show();
            _aboutWindow.FormClosed += _aboutWindow_FormClosed;
        }
        private void _aboutWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            _aboutWindow = null;
        }

        private void documentationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
            Process.Start("https://opencage.co.uk/docs/");
        }

        GameDirectoryManager _directoryManager = null;
        private void manageGameDirectoriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_directoryManager != null)
            {
                _directoryManager.FormClosed -= manageGameDirectoriesToolStripMenuItem_Click;
                _directoryManager.Close();
            }

            _directoryManager = new GameDirectoryManager();
            _directoryManager.Show();
            _directoryManager.FormClosed += manageGameDirectoriesToolStripMenuItem_Click;
        }
        private void manageGameDirectoriesToolStripMenuItem_Click(object sender, FormClosedEventArgs e)
        {
            _directoryManager = null;
        }
    }
}
