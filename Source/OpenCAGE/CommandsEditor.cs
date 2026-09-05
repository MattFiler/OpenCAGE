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
using OpenCAGE.AnimTrees;
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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
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
        private const double DefaultLeftSearchPortion = 0.28;
        private float _defaultSplitterDistance = 0.25f;
        private int _defaultWidth;
        private int _defaultHeight;
        private FormWindowState _lastWindowState;

        private bool _settingUp = true;


        public CommandsEditor(string level = null)
        {
            //LocalDebug.CheckAnimKFTypes();


            //LocalDebug.CheckWriteInstanced();

            InitializeComponent();
            Theming.ThemeManager.ApplyToForm(this);

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

            dockPanel.ShowDocumentIcon = true;
            dockPanel.DocumentStyle = DocumentStyle.DockingWindow;
            Theming.ThemeManager.ApplyToDockPanel(dockPanel);

            _defaultWidth = Width;
            _defaultHeight = Height;

#if !DEBUG
            //Dev options
            DEBUG_ReloadLevel.Visible = false;
            connectToRuntimeUtils.Visible = false;
            optionsToolStripSeparatorRuntimeUtils.Visible = false;
            
            //WIP forms
            scriptReadableVariablesToolStripMenuItem.Visible = false;
#endif

            WindowState = SettingsManager.GetString(Settings.WindowState, "Normal") == "Maximized" ? FormWindowState.Maximized : FormWindowState.Normal;
            _lastWindowState = WindowState;
            Width = SettingsManager.GetInteger(Settings.WindowWidth, _defaultWidth);
            Height = SettingsManager.GetInteger(Settings.WindowHeight, _defaultHeight);
            ApplyMainDockPortionsFromSettings();
            Resize += CommandsEditor_Resize;
            Shown += CommandsEditor_Shown;
            FormClosing += CommandsEditor_FormClosing;

            Singleton.OnEntityAdded += OnEntityAdded;
            Singleton.OnResourceModified += OnResourceModified;

            // Options category menus: open on hover, and stay open when toggling checkable items
            compositeViewerToolStripMenuItem.MouseHover += (sender, e) => { ((ToolStripMenuItem)sender).PerformClick(); };
            compositeViewerToolStripMenuItem.DropDown.Closing += OptionsDropDown_Closing;
            entityDisplayToolStripMenuItem.MouseHover += (sender, e) => { ((ToolStripMenuItem)sender).PerformClick(); };
            entityDisplayToolStripMenuItem.DropDown.Closing += OptionsDropDown_Closing;
            miscToolStripMenuItem.MouseHover += (sender, e) => { ((ToolStripMenuItem)sender).PerformClick(); };
            miscToolStripMenuItem.DropDown.Closing += OptionsDropDown_Closing;
            viewportOptionsToolStripMenuItem.DropDown.Closing += OptionsDropDown_Closing;
            viewportOptionsToolStripMenuItem.DropDownOpening += ViewportOptionsDropdownOpening;
            SetupOptions();

            SettingsManager.SettingsChanged += OnSettingsChanged;

            //Populate level list
            List<string> levels = EditorUtils.GetEditableLevels();
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

            //Apply every setting's effect on startup through the same single path used for local/external changes
            ApplySettingEffects(null);

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

            //Game directory management should not be visible in child processes - until the primary goes away
            manageGameDirectoriesToolStripMenuItem.Visible = Singleton.IsPrimaryInstance;
            if (!Singleton.IsPrimaryInstance)
                WatchForPrimaryInstanceHandover();

#if ENABLE_MOD_PACKAGES
            //Mod packaging lives on the toolbar next to backups
            ToolStripButton modManagerBtn = new ToolStripButton("Mod Manager") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            modManagerBtn.Click += modManagerBtn_Click;
            toolStrip.Items.Insert(toolStrip.Items.IndexOf(manageBackupsBtn) + 1, modManagerBtn);
#endif
            Modding.ModServices.CaptureSmallFilesInBackground();

            versionToolStripMenuItem.Text = "Version " + ProductVersion;
            _settingUp = false;
        }

#if ENABLE_MOD_PACKAGES
        ModManagerForm _modManager = null;
        private void modManagerBtn_Click(object sender, EventArgs e)
        {
            if (_modManager != null)
            {
                _modManager.FormClosed -= _modManager_FormClosed;
                _modManager.Close();
            }

            _modManager = new ModManagerForm();
            _modManager.Show();
            _modManager.FormClosed += _modManager_FormClosed;
        }
        private void _modManager_FormClosed(object sender, FormClosedEventArgs e)
        {
            _modManager = null;
        }
#endif

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

        // Keep option toggle menus open after clicking a checkable item so ticks stay visible.
        // Still close on outside click, focus loss, Escape, etc.
        private void OptionsDropDown_Closing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
                e.Cancel = true;
        }

        private void CommandsEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!TryConfirmCloseWithOptionalSave())
            {
                e.Cancel = true;
                return;
            }

            _primaryInstanceTimer?.Stop();
            SettingsManager.SettingsChanged -= OnSettingsChanged;

            // Cancel in-flight loads so a completing background thread cannot touch this form after dispose
            _levelLoadGeneration++;
            if (_levelLoadedHandler != null)
            {
                Singleton.OnLevelLoaded -= _levelLoadedHandler;
                _levelLoadedHandler = null;
            }

            KillBehaviourTreeEditor();
            HideLoadProgressUI();
            KillLevelViewer();
            SaveSplitterDistances();
        }

        private bool TryConfirmCloseWithOptionalSave()
        {
            if (!SettingsManager.GetBool(Settings.PromptSaveOnClose))
                return true;

            if (_compositeBrowser?.Content?.Level == null)
                return true;

#if USE_DIRTY_TRACKER
            if (!DirtyTracker.IsDirty)
                return true;
#endif

            string levelName = _compositeBrowser.Content.Level.Name;
            DialogResult result = MessageBox.Show(
                "Save \"" + levelName + "\" before closing OpenCAGE?",
                "Save level?",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Cancel)
                return false;

            if (result == DialogResult.Yes)
                SaveLevel(false, successMsg: false, allowLaunchGame: false);

            return true;
        }

        private void CommandsEditor_Shown(object sender, EventArgs e)
        {
            dockPanel?.PerformLayout();
            _compositeDisplay?.RefreshInnerDockLayoutAfterResize();

            /* Harvest the shader permutation database if this install has no database yet. It only
             * widens what the material editor can offer, so it runs itself once in the background
             * rather than waiting to be asked - nothing here blocks, and a failure just leaves the
             * editor with the permutations each level already carries. */
            Modding.ShaderDatabase.EnsureBuiltInBackground(Singleton.PathToAI,
                msg => SetIdleStatus("Shader database: " + msg),
                () => SetIdleStatus(null));

#if ENABLE_MOD_PACKAGES
            //Launched by double-clicking a mod package: straight into the Mod Manager with it
            if (!string.IsNullOrEmpty(Modding.ModServices.PendingPackageImport))
            {
                string package = Modding.ModServices.PendingPackageImport;
                Modding.ModServices.PendingPackageImport = null;
                modManagerBtn_Click(this, EventArgs.Empty);
                _modManager?.ImportPackageFile(package);
            }
#endif
        }

        //UI: remember width/height of editor
        private void CommandsEditor_Resize(object sender, EventArgs e)
        {
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

            // Maximize/restore often skip a full layout pass - nudge the dock panel once.
            if (WindowState != _lastWindowState && WindowState != FormWindowState.Minimized)
            {
                _lastWindowState = WindowState;
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed || Disposing || dockPanel == null || dockPanel.IsDisposed)
                        return;
                    dockPanel.PerformLayout();
                    _compositeDisplay?.RefreshInnerDockLayoutAfterResize();
                }));
            }
            else
            {
                _lastWindowState = WindowState;
            }
        }

        private static double ClampDockPortion(double portion)
        {
            return Math.Max(0.05, Math.Min(0.95, portion));
        }

        private static double LoadDockPortionSetting(float savedPortion, double defaultPortion)
        {
            if (savedPortion <= 0f)
                return defaultPortion;
            return ClampDockPortion(savedPortion);
        }

        private void ApplyMainDockPortionsFromSettings()
        {
            if (dockPanel == null)
                return;

            dockPanel.DockLeftPortion = LoadDockPortionSetting(
                SettingsManager.GetFloat(Settings.DockSplitterLeft, DefaultSideDockPortion),
                DefaultSideDockPortion);
            dockPanel.DockRightPortion = LoadDockPortionSetting(
                SettingsManager.GetFloat(Settings.DockSplitterRight, DefaultEntityInspectorPortion),
                DefaultEntityInspectorPortion);
            dockPanel.DockBottomPortion = LoadDockPortionSetting(
                SettingsManager.GetFloat(Settings.DockSplitterBottom, _defaultSplitterDistance),
                _defaultSplitterDistance);
        }

        private void SaveSplitterDistances()
        {
            if (dockPanel == null)
                return;

            try
            {
                SettingsManager.SetFloat(
                    Settings.DockSplitterLeft,
                    (float)ClampDockPortion(dockPanel.DockLeftPortion));
                SettingsManager.SetFloat(
                    Settings.DockSplitterRight,
                    (float)ClampDockPortion(dockPanel.DockRightPortion));
                SettingsManager.SetFloat(
                    Settings.DockSplitterBottom,
                    (float)ClampDockPortion(dockPanel.DockBottomPortion));

                _compositeDisplay?.SaveInnerDockLayout();
            }
            catch
            {
            }
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
        private void createLevelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new CreateLevel().Show();
        }
        private void importCompositesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!LevelIsOpenForPorting()) return;
            new ImportComposites(false, _compositeBrowser.Content.Level.Name).Show();
        }
        private void portCompositesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!LevelIsOpenForPorting()) return;
            new ExportComposite(null).Show();
        }
        private bool LevelIsOpenForPorting()
        {
            if (_compositeBrowser?.Content?.IsLevelDataLoaded == true)
                return true;
            MessageBox.Show("Load a level first.", "No level loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        //The level menu is built at startup, so a level created during this session has no entry yet
        private ToolStripMenuItem EnsureLevelMenuItem(string level)
        {
            if (_levelMenuItems.TryGetValue(level, out ToolStripMenuItem item))
                return item;

            item = new ToolStripMenuItem(level);
            item.Click += OnLevelSelected;
            loadLevel.DropDownItems.Add(item);
            _levelMenuItems.Add(level, item);
            return item;
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

            //The menu level is the base every new level is built from, not something to edit
            if (EditorUtils.IsFrontend(level))
            {
                MessageBox.Show("FRONTEND is the game's menu level and cannot be edited.\nUse File -> Create Level to start a level of your own from it.", "Not an editable level", MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (_compositeBrowser == null)
                    loadLevel_Click(null, null);
                return;
            }

            SettingsManager.SetString(Settings.LastSelectedLevel, level);

            bool hadPreviousLevel = _compositeBrowser != null;
            if (hadPreviousLevel)
            {
                Singleton.Editor.DockPanel.ActiveAutoHideContent = null;
                string oldLevelName = _compositeBrowser.Content?.Level?.Name;
                if (oldLevelName != null && _levelMenuItems.TryGetValue(oldLevelName, out ToolStripMenuItem oldLevelItem))
                    oldLevelItem.Checked = false;

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

            EnsureLevelMenuItem(_compositeBrowser.Content.Level.Name).Checked = true;
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
                try
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        if (IsDisposed || Disposing || loadGeneration != _levelLoadGeneration)
                            return;

                        EndViewerPopulateProgress(0, forceClose: true);
                        EnableButtons(true, "");
                        //TODO: warn!
                    }));
                }
                catch (ObjectDisposedException) { }
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
            if (IsDisposed || Disposing)
                return;

            if (_progressUI == null || _progressUI.IsDisposed)
                _progressUI = new ProgressUI();
        }

        private void ShowLoadProgressLoading(Level level)
        {
            if (level == null || IsDisposed || Disposing)
                return;

            CloseProgressUI();
            EnsureProgressUI();
            if (_progressUI == null)
                return;

            _progressUI.ShowLevelLoading(level);
            StartProgressKeepOnTop();
            _levelLoadInProgress = true;
        }

        private void ShowLoadProgressPopulating(string displayLabel)
        {
            if (IsDisposed || Disposing)
                return;

            EnsureProgressUI();
            if (_progressUI == null)
                return;

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
            if (IsDisposed || Disposing)
                return;

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

            if (IsDisposed || Disposing)
                return;

            string levelName = content.Level?.Name;
            try
            {
                if (InvokeRequired)
                    Invoke(new Action(() => OnCathodeLoadComplete(levelName, loadGeneration)));
                else
                    OnCathodeLoadComplete(levelName, loadGeneration);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            QueueBuildLevelPanelsWhenReady(content, loadGeneration, 0);
        }

        private void QueueBuildLevelPanelsWhenReady(LevelContent content, int loadGeneration, int attempt)
        {
            if (IsDisposed || Disposing)
                return;

            try
            {
                BeginInvoke(new Action(() => TryBuildLevelPanelsWhenReady(content, loadGeneration, attempt)));
            }
            catch (ObjectDisposedException) { }
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
            if (readyContent.EditorUtils == null)
            {
                if (attempt < MaxLevelPanelBuildAttempts)
                    QueueBuildLevelPanelsWhenReady(content, loadGeneration, attempt + 1);
                return;
            }

            if (_compositeDisplay == null || _compositeDisplay.IsDisposed)
            {
                if (attempt < MaxLevelPanelBuildAttempts)
                    QueueBuildLevelPanelsWhenReady(content, loadGeneration, attempt + 1);
                return;
            }

            _compositeDisplay.AttachCompositeBrowser(_compositeBrowser);

            EnableButtons(true, "");
            _compositeBrowser.Resize += _compositeBrowser_Resize;
            _compositeBrowser.FormClosed += _compositeBrowser_FormClosed;

            EnsureRequiredDockLayout();

            UpdateCompositeBrowserDockState();

            _entityBrowser.InitializeFromLevel();
            _entityList.UpdateTitle();
            _entitySearch.InitializeFromLevel();
            //State list depends on the level's ExclusiveMaster resources
            EntityClipboard.Clear(); //static ctor may not have run yet on the first load
            UnityConnection.ViewerStateInfoMode.Clear();
            _levelViewerPanel?.RefreshStateInfoMenu(_compositeBrowser?.Content);
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

            // Always rebuild the fixed dock structure, then restore saved splitter ratios.
            ApplyDefaultDockLayout();

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

            //A viewer that lost its host window (the panel's handle was recreated by a dock/layout change) is
            //still running but invisible. It is not re-embedded mid-session, since its state may have diverged
            //from what is about to be loaded - a level load is the point at which it is safe to start again.
            if (_levelViewerPanel.IsRunning && !_levelViewerPanel.IsEmbedded)
                _levelViewerPanel.Stop();

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
            }

            if (_entityList == null)
            {
                _entityList = new EntityList();
                _entityList.FormClosing += EntityList_FormClosing;
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

                if (Singleton.ViewportEnabled)
                    _compositeDisplay.EnsureInnerDockLayoutRestored();
            }
            else if (_compositeBrowser != null)
            {
                _compositeDisplay.AttachCompositeBrowser(_compositeBrowser);
            }

            if (_entityBrowser == null)
            {
                _entityBrowser = new EntityBrowser();
            }

            if (_entitySearch == null)
            {
                _entitySearch = new EntitySearch();
                _entitySearch.FormClosing += EntitySearch_FormClosing;
            }

            if (_renderFiltersPanel == null)
            {
                _renderFiltersPanel = new RenderFiltersPanel();
                _renderFiltersPanel.FormClosing += RenderFiltersPanel_FormClosing;
            }

            _entityInspector.AttachCompositeDisplay(_compositeDisplay);
        }

        private void ApplyDefaultDockLayout(bool resetInnerDock = true)
        {
            _compositeDisplay.Show(dockPanel, DockState.Document);
            ApplyLeftDockLayout();
            ApplyRightDockLayout();

            if (resetInnerDock)
            {
                if (SettingsManager.IsSet(Settings.DockSplitterLevelViewer))
                    _compositeDisplay?.EnsureInnerDockLayoutRestored();
                else
                    _compositeDisplay?.ApplyDefaultInnerDockLayout();
            }

            ApplyMainDockPortionsFromSettings();
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

            ApplyMainDockPortionsFromSettings();
            _compositeDisplay?.EnsureInnerDockLayoutRestored();
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

        public CompositeDisplay LoadComposite(Composite composite, bool newDisplay = false)
        {
            if (composite == null || _compositeDisplay == null || _compositeDisplay.IsDisposed)
                return null;

            if (_compositeBrowser != null)
                _compositeDisplay.AttachCompositeBrowser(_compositeBrowser);

            _compositeBrowser?.Content?.EnsureEditorUtils();
            if (_compositeBrowser?.Content?.EditorUtils == null)
                return null;

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

            SaveSplitterDistances();
            _compositeBrowser?.CloseAllChildTabs();
            CloseDockPanelContents(preserveLevelViewer: preserveViewer);

            if (_compositeBrowser != null)
            {
                _compositeBrowser.Resize -= _compositeBrowser_Resize;
                _compositeBrowser.FormClosed -= _compositeBrowser_FormClosed;
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

            if (panel is CompositeDisplay compositeDisplay)
                compositeDisplay.DepopulateUI();

            panel.Hide();
            if (panel.DockHandler.DockPanel != null)
                panel.DockHandler.Close();
            panel.Dispose();
        }

        private void _compositeBrowser_Resize(object sender, EventArgs e)
        {
            SaveSplitterDistances();
        }

        private void _entityInspector_Resize(object sender, EventArgs e)
        {
            SaveSplitterDistances();
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
            SaveLevel(false);
        }

        private void saveAndBuildLevelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveLevel(true);
        }

        public void SaveLevel(bool doInstancing, bool successMsg = true, bool allowLaunchGame = true)
        {
            if (_compositeBrowser == null) return;

            //If backup manager is working on this level, don't allow saving
            switch (Singleton.CurrentBackupState)
            {
                case Singleton.BackupState.ALL_LEVELS:
                    MessageBox.Show("Cannot save level - backup is in progress!", "Backup in progress...", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                case Singleton.BackupState.SINGLE_LEVEL:
                    if (Singleton.BackupLevel.ToLower() == _compositeBrowser.Content.Level.Name.ToLower())
                    {
                        MessageBox.Show("Cannot save level - backup is in progress!", "Backup in progress...", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    break;
            }

            //Close alien down if it's open, it conflicts with our write locks!
            EditorUtils.CloseAI();

            Cursor.Current = Cursors.WaitCursor;
            statusText.Text = "Saving...";
            statusStrip.Update();

            CloseProgressUI();
            EnsureProgressUI();
            _progressUI.ShowLevelSaving(_compositeBrowser.Content.Level, doInstancing);
            StartProgressKeepOnTop();

            if (_compositeDisplay != null)
                _compositeDisplay.SaveAllFlowgraphs();

            if (doInstancing)
            {
                Task saveTask = Task.Run(() => _compositeBrowser.Content.Save(true));
                while (!saveTask.IsCompleted)
                {
                    Application.DoEvents();
                    Thread.Sleep(16);
                }
                saveTask.GetAwaiter().GetResult();
            }
            else
            {
                _compositeBrowser.Content.Save(false);
            }

            //A baker that threw was caught so one bad system could not cost the whole save, which
            //means the level kept whatever that system already had on disk. Say so: a console line
            //nobody sees is not enough when the AI then paths through geometry that has moved.
            IReadOnlyList<string> bakeWarnings = _compositeBrowser.Content.LastBakeWarnings;
            IReadOnlyList<string> warnings = _compositeBrowser.Content.LastWarnings;
            bool anyBake = bakeWarnings != null && bakeWarnings.Count != 0;
            bool anyWarn = warnings != null && warnings.Count != 0;
            if (anyBake || anyWarn)
            {
                string text = "The level saved, but not everything made it in.";
                if (anyBake)
                    text += "\n\n" + bakeWarnings.Count + " system" + (bakeWarnings.Count == 1 ? "" : "s") +
                            " could not be regenerated and kept the data already on disk:\n  " + string.Join("\n  ", bakeWarnings);
                if (anyWarn)
                    text += "\n\n" + warnings.Count + " thing" + (warnings.Count == 1 ? "" : "s") +
                            " the build could not do as asked:\n  " + string.Join("\n  ", warnings);
                MessageBox.Show(text, "Some data was not regenerated", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

#if !DEBUG
            PatchManager.PatchFileIntegrityCheck(Singleton.Platform, Singleton.PathToAI);
            PatchManager.PatchPopupMessage(Singleton.Platform, Singleton.PathToAI);
            PatchManager.UpdateLevelListInPackages(Singleton.Platform, Singleton.PathToAI);

            PatchManager.PatchSkipFrontendFlag(Singleton.Platform, Singleton.PathToAI, SettingsManager.GetBool(Settings.SkipFrontend));
            PatchManager.PatchNoUIFlag(Singleton.Platform, Singleton.PathToAI, SettingsManager.GetBool(Settings.HudDisabled));
            PatchManager.PatchMemReplayLogFlag(Singleton.Platform, Singleton.PathToAI, SettingsManager.GetBool(Settings.MemReplayLogs));
            PatchManager.PatchUIPerfFlag(Singleton.Platform, Singleton.PathToAI, SettingsManager.GetBool(Settings.UiEnabledUiPerf));

            if (allowLaunchGame && SettingsManager.GetBool(Settings.LaunchGameWhenSaved))
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

        /* Status text for background work nobody asked for: it may only write when the bar is idle,
         * and may only clear its own message. Otherwise a long harvest would stamp over "Saving..."
         * and then blank it, which would read as the save having finished. Null clears. */
        private string _idleStatus = null;
        private void SetIdleStatus(string text)
        {
            try
            {
                if (statusStrip == null || statusStrip.IsDisposed) return;
                Action apply = () =>
                {
                    if (statusText == null) return;
                    if (text == null)
                    {
                        if (_idleStatus != null && statusText.Text == _idleStatus)
                            statusText.Text = "";
                        _idleStatus = null;
                    }
                    else if (string.IsNullOrEmpty(statusText.Text) || statusText.Text == _idleStatus)
                    {
                        _idleStatus = text;
                        statusText.Text = text;
                    }
                    statusStrip.Update();
                };
                if (statusStrip.InvokeRequired) statusStrip.BeginInvoke(apply);
                else apply();
            }
            catch { }
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

        private void ConfigureLevelViewerAvailability()
        {
            //The Viewport menu stays, since its first item is what turns the viewport back on; the rest of it
            //and the viewer's own controls only mean something while it is running
            enableViewportToolStripMenuItem.Checked = Singleton.ViewportEnabled;
            foreach (ToolStripItem item in viewportOptionsToolStripMenuItem.DropDownItems)
            {
                if (item != enableViewportToolStripMenuItem && item != viewportOptionsToolStripSeparator)
                    item.Enabled = Singleton.ViewportEnabled;
            }
            resetRenderFiltersOnLoadToolStripMenuItem.Visible = Singleton.ViewportEnabled;

            if (!Singleton.ViewportEnabled)
            {
                _renderFiltersPanel?.Hide();
                _compositeDisplay?.HideLevelViewerPanel();
                if (_entityInspector != null && dockPanel != null && dockPanel.Contents.Count > 0)
                    EnsureRequiredDockLayout();
            }
            else
            {
                EnsureLevelViewerConnection();
            }
        }

        /* Options > Viewport > Enable Viewport. Off is what -disable_viewport does (the Steam launch option
           and child instances pass it), only remembered and switchable without a restart: the viewer is
           closed on the spot, or launched for the level that is open. */
        private void enableViewportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool enable = !Singleton.ViewportEnabled;
            if (enable && !File.Exists(Singleton.ViewportExecutablePath))
            {
                MessageBox.Show(
                    "Could not find CathodeEditorGodot.exe.\nExpected path:\n" + Singleton.ViewportExecutablePath,
                    "Viewport",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            /* The viewer reads the level from disk. Anything edited while it was off (or before it was
             * ever on) would not be in what it loads, and every edit after that would be applied on top
             * of a level that is already behind - so it either saves first or waits for the next level
             * load, which is the point the two are in step again. Either way the setting is on. */
            bool openNow = enable && ConfirmSaveBeforeOpeningViewport();

            SettingsManager.SetBool(Settings.ViewportEnabled, enable);
            Singleton.ViewportEnabled = enable;

            if (enable)
            {
                ConfigureLevelViewerAvailability();
                EnsureDockPanelsCreated();
                if (_renderFiltersPanel != null && dockPanel != null && dockPanel.Contents.Count > 0)
                    EnsureRequiredDockLayout();
                if (openNow && _compositeBrowser?.Content?.Level != null)
                    BeginParallelLevelViewerLoad(_compositeBrowser.Content.Level.Name);
                else if (!openNow)
                    SetIdleStatus("The viewport will open when a level is next loaded.");
            }
            else
            {
                KillLevelViewer();
                ConfigureLevelViewerAvailability();
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
            ToggleBoolSetting(Settings.HighlightAliases);
        }

        private void highlightProxiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleBoolSetting(Settings.HighlightProxies);
        }

        private void showCameraPositionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleBoolSetting(Settings.ShowCameraPosition);
        }

        private void focusOnSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool enabled = !SettingsManager.GetBool(Settings.FocusOnSelected);
            SettingsManager.SetBool(Settings.FocusOnSelected, enabled);
            if (!enabled)
                SettingsManager.SetBool(Settings.FixCameraToSelected, false);
            ApplySettingEffects(new[] { Settings.FocusOnSelected, Settings.FixCameraToSelected });
        }

        private void fixCameraToSelectedEntityToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool enabled = !SettingsManager.GetBool(Settings.FixCameraToSelected);
            SettingsManager.SetBool(Settings.FixCameraToSelected, enabled);
            if (enabled)
                SettingsManager.SetBool(Settings.FocusOnSelected, true);
            ApplySettingEffects(new[] { Settings.FixCameraToSelected, Settings.FocusOnSelected });
        }

        private void renderWireframeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleBoolSetting(Settings.RenderWireframe);
        }

        private void hideNestedScriptEntitiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleBoolSetting(Settings.HideNestedScriptEntities);
        }

        private void resetRenderFiltersOnLoadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleBoolSetting(Settings.ResetRenderFilters);
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
            _levelViewerPanel.CreateModeChanged -= LevelViewerPanel_CreateModeChanged;
            _levelViewerPanel.CreateModeChanged += LevelViewerPanel_CreateModeChanged;
            _levelViewerPanel.StateInfoChanged -= LevelViewerPanel_StateInfoChanged;
            _levelViewerPanel.StateInfoChanged += LevelViewerPanel_StateInfoChanged;
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
            _levelViewerPanel.ApplyCreateMode(UnityConnection.ViewerCreateMode.ActiveFunctionType);
            _levelViewerPanel.ApplyStateInfo();
        }

        private void LevelViewerPanel_StateInfoChanged(object sender, EventArgs e)
        {
            UnityConnection.Send.SendSettingsPacket();
        }

        private void LevelViewerPanel_SelectionModeChanged(object sender, LevelViewerDeepSelectMode mode)
        {
            SettingsManager.SetInteger(Settings.LevelViewerDeepSelectMode, (int)mode);
            UnityConnection.Send.SendSettingsPacket();
        }

        private void LevelViewerPanel_GizmoModeChanged(object sender, LevelViewerGizmoMode mode)
        {
            //Choosing a gizmo mode exits entity creation mode
            if (UnityConnection.ViewerCreateMode.IsActive)
            {
                UnityConnection.ViewerCreateMode.ActiveFunctionType = 0;
                _levelViewerPanel?.ApplyCreateMode(0);
            }

            SettingsManager.SetInteger(Settings.LevelViewerGizmoMode, (int)mode);
            UnityConnection.Send.SendSettingsPacket();
        }

        private void LevelViewerPanel_CreateModeChanged(object sender, uint functionType)
        {
            UnityConnection.ViewerCreateMode.ActiveFunctionType = functionType;

            if (functionType != 0)
            {
                //Entering creation mode disables the transform gizmo
                SettingsManager.SetInteger(Settings.LevelViewerGizmoMode, (int)LevelViewerGizmoMode.None);
                _levelViewerPanel?.ApplyGizmoMode(LevelViewerGizmoMode.None);

                //Make sure the created entities will actually be visible in the viewer
                if (!RenderFilters.IsEnabled(functionType))
                {
                    RenderFilters.SetEnabled(functionType, true);
                    UnityConnection.Send.SendRenderFilterPacket();
                    _renderFiltersPanel?.RefreshFilters();
                }
            }

            UnityConnection.Send.SendSettingsPacket();
        }

        private void ApplyTransformSnapSelectionsFromSettings()
        {
            ApplyTransformGridSnapSelection(TransformSnapDefinitions.NormalizeGridSnap(
                SettingsManager.GetFloat(Settings.TransformGridSnap)));
            ApplyRotationSnapSelection(TransformSnapDefinitions.NormalizeRotationSnap(
                SettingsManager.GetFloat(Settings.RotationSnapDegrees)));
        }

        //Flip a boolean setting and route its effect through the single ApplySettingEffects path
        private void ToggleBoolSetting(string key)
        {
            SettingsManager.SetBool(key, !SettingsManager.GetBool(key));
            ApplySettingEffects(new[] { key });
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

        private void OnSettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            if (!e.ExternalChange || e.ChangedKeys.Count == 0)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ApplySettingEffects(e.ChangedKeys)));
                return;
            }

            ApplySettingEffects(e.ChangedKeys);
        }

        private static readonly HashSet<string> ViewerSettingsKeys = new HashSet<string>
        {
            Settings.HighlightAliases,
            Settings.HighlightProxies,
            Settings.FocusOnSelected,
            Settings.FixCameraToSelected,
            Settings.ShowCameraPosition,
            Settings.RenderWireframe,
            Settings.HideNestedScriptEntities,
            Settings.LevelViewerDeepSelectMode,
            Settings.LevelViewerGizmoMode,
            Settings.TransformGridSnap,
            Settings.RotationSnapDegrees,
        };

        private static bool ShouldApplySetting(string key, IReadOnlyList<string> changedKeys)
        {
            return changedKeys == null || changedKeys.Count == 0 || SettingsChangedEventArgs.ContainsKey(changedKeys, key);
        }

        private void ApplyAllOptionCheckboxesFromSettings(IReadOnlyList<string> changedKeys)
        {
            if (ShouldApplySetting(Settings.RuntimeUtilsOpt, changedKeys))
                connectToRuntimeUtils.Checked = SettingsManager.GetBool(Settings.RuntimeUtilsOpt);

            if (ShouldApplySetting(Settings.ShowShortGuids, changedKeys))
                showEntityIDs.Checked = SettingsManager.GetBool(Settings.ShowShortGuids);
            if (ShouldApplySetting(Settings.CompNameOnlyOpt, changedKeys))
                searchOnlyCompositeNames.Checked = SettingsManager.GetBool(Settings.CompNameOnlyOpt);
            if (ShouldApplySetting(Settings.ShowSavedMsgOpt, changedKeys))
                showConfirmationWhenSavingToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.ShowSavedMsgOpt);
            if (ShouldApplySetting(Settings.PromptSaveOnClose, changedKeys))
                promptToSaveOnCloseToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.PromptSaveOnClose);
            if (ShouldApplySetting(Settings.ShowTexOpt, changedKeys))
                useTexturedModelViewExperimentalToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.ShowTexOpt);
            if (ShouldApplySetting(Settings.EnableFileBrowser, changedKeys))
                showExplorerViewToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.EnableFileBrowser);
            if (ShouldApplySetting(Settings.KeepUsesWindowOpen, changedKeys))
                keepFunctionUsesWindowOpenToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.KeepUsesWindowOpen);
            if (ShouldApplySetting(Settings.LaunchGameWhenSaved, changedKeys))
                openGameOnSaveToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.LaunchGameWhenSaved);
            if (ShouldApplySetting(Settings.ShowGamePlatform, changedKeys))
                showGamePlatformToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.ShowGamePlatform);
            if (ShouldApplySetting(Settings.PopulateAllPinsOnCreateNode, changedKeys))
                populateAllNodePinsWhenCreatedToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.PopulateAllPinsOnCreateNode);
            if (ShouldApplySetting(Settings.FocusCanvasOnNewNode, changedKeys))
                focusCanvasOnNewNodeToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.FocusCanvasOnNewNode);
            if (ShouldApplySetting(Settings.DarkMode, changedKeys))
                darkModeToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.DarkMode);
            if (ShouldApplySetting(Settings.OptionToDeleteEntityWithNode, changedKeys))
                giveOptionToDeleteEntityWhenNoNodesToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.OptionToDeleteEntityWithNode);
            if (ShouldApplySetting(Settings.AskBeforeDeletingNode, changedKeys))
                showConfirmationWhenDeletingNodeToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.AskBeforeDeletingNode);

            if (ShouldApplySetting(Settings.HighlightAliases, changedKeys))
                highlightAliasesToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.HighlightAliases);
            if (ShouldApplySetting(Settings.HighlightProxies, changedKeys))
                highlightProxiesToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.HighlightProxies);
            if (ShouldApplySetting(Settings.FocusOnSelected, changedKeys))
                focusOnSelectedToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.FocusOnSelected);
            if (ShouldApplySetting(Settings.FixCameraToSelected, changedKeys))
                fixCameraToSelectedEntityToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.FixCameraToSelected);
            if (ShouldApplySetting(Settings.ShowCameraPosition, changedKeys))
                showCameraPositionToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.ShowCameraPosition);
            if (ShouldApplySetting(Settings.RenderWireframe, changedKeys))
                renderWireframeToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.RenderWireframe);
            if (ShouldApplySetting(Settings.HideNestedScriptEntities, changedKeys))
                hideNestedScriptEntitiesToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.HideNestedScriptEntities);
            if (ShouldApplySetting(Settings.ResetRenderFilters, changedKeys))
                resetRenderFiltersOnLoadToolStripMenuItem.Checked = SettingsManager.GetBool(Settings.ResetRenderFilters);
        }

        private void ApplySettingEffects(IReadOnlyList<string> changedKeys)
        {
            ApplyAllOptionCheckboxesFromSettings(changedKeys);

            bool pushViewerSettings = false;

            if (ShouldApplySetting(Settings.ShowShortGuids, changedKeys))
                ApplyShowShortGuidsDisplayRefresh();

            //Covers the settings file being edited outside the app, where nothing has switched yet
            if (ShouldApplySetting(Settings.DarkMode, changedKeys))
                Theming.ThemeManager.SetDark(SettingsManager.GetBool(Settings.DarkMode));

            if (ShouldApplySetting(Settings.EnableFileBrowser, changedKeys))
                UpdateCompositeBrowserDockState();

            if (ShouldApplySetting(Settings.RuntimeUtilsOpt, changedKeys))
                ApplyRuntimeUtilsOptFromSettings();

            if (ShouldApplySetting(Settings.LevelViewerDeepSelectMode, changedKeys)
                || ShouldApplySetting(Settings.LevelViewerGizmoMode, changedKeys))
                ApplyLevelViewerViewportModesFromSettings();

            if (ShouldApplySetting(Settings.TransformGridSnap, changedKeys)
                || ShouldApplySetting(Settings.RotationSnapDegrees, changedKeys))
                ApplyTransformSnapSelectionsFromSettings();

            if (ShouldApplySetting(Settings.BoxRenderFilters, changedKeys))
            {
                _renderFiltersPanel?.RefreshFilters();
                if (Singleton.ViewportEnabled)
                    UnityConnection.Send.SendRenderFilterPacket();
            }

            if (ShouldApplySetting(Settings.ShowGamePlatform, changedKeys))
                UpdateTitle();

            foreach (string key in ViewerSettingsKeys)
            {
                if (ShouldApplySetting(key, changedKeys))
                {
                    pushViewerSettings = true;
                    break;
                }
            }

            if (ShouldApplyAnyNodeColour(changedKeys))
                Singleton.OnNodeStyleChanged?.Invoke();

            if (ShouldApplySetting(Settings.NumericStep, changedKeys)
                || ShouldApplySetting(Settings.NumericStepRot, changedKeys))
                NumericStepSettings.NotifyChanged();

            if (pushViewerSettings && Singleton.ViewportEnabled)
                UnityConnection.Send.SendSettingsPacket();
        }

        private static bool ShouldApplyAnyNodeColour(IReadOnlyList<string> changedKeys)
        {
            if (changedKeys == null || changedKeys.Count == 0)
                return true;

            foreach (string key in changedKeys)
            {
                if (Settings.IsNodeColourKey(key))
                    return true;
            }
            return false;
        }

        private void ApplyRuntimeUtilsOptFromSettings()
        {
            bool enabled = SettingsManager.GetBool(Settings.RuntimeUtilsOpt);

            if (enabled)
            {
                if (!RuntimeUtilsConnection.Send.Start())
                    enabled = false;
            }
            else
            {
                RuntimeUtilsConnection.Send.Stop();
            }

            connectToRuntimeUtils.Checked = enabled;
        }

        private void connectToRuntimeUtils_Click(object sender, EventArgs e)
        {
            SettingsManager.SetBool(Settings.RuntimeUtilsOpt, !SettingsManager.GetBool(Settings.RuntimeUtilsOpt));
            ApplySettingEffects(new[] { Settings.RuntimeUtilsOpt });

            //If we asked to connect but the effect couldn't establish a connection, revert and warn
            if (SettingsManager.GetBool(Settings.RuntimeUtilsOpt) && !connectToRuntimeUtils.Checked)
            {
                SettingsManager.SetBool(Settings.RuntimeUtilsOpt, false);
                MessageBox.Show("Failed to connect to RuntimeUtils server.\nIs the game running with the RuntimeUtils DLL loaded?", "Connection failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            ToggleBoolSetting(Settings.ShowShortGuids);
        }

        private void ApplyShowShortGuidsDisplayRefresh()
        {
            LevelContent content = _compositeBrowser?.Content;
            content?.ClearEntityListViewCache();
            content?.EditorUtils?.ClearEntityNameCache();

            _compositeDisplay?.ReloadEntityListFromComposite();
            _compositeDisplay?.RefreshPathBreadcrumb();
            _compositeDisplay?.ReloadAllEntities();
            _entitySearch?.InitializeFromLevel();
        }

        private void searchOnlyCompositeNames_Click(object sender, EventArgs e)
        {
            ToggleBoolSetting(Settings.CompNameOnlyOpt);
        }

        private void showConfirmationWhenSavingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleBoolSetting(Settings.ShowSavedMsgOpt);
        }

        private void promptToSaveOnCloseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleBoolSetting(Settings.PromptSaveOnClose);
        }

        private void useTexturedModelViewExperimentalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleBoolSetting(Settings.ShowTexOpt);
        }

        private void showExplorerViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleBoolSetting(Settings.EnableFileBrowser);
        }

        private void keepFunctionUsesWindowOpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleBoolSetting(Settings.KeepUsesWindowOpen);
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

        private void populateAllNodePinsWhenCreatedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleBoolSetting(Settings.PopulateAllPinsOnCreateNode);
        }

        private void focusCanvasOnNewNodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleBoolSetting(Settings.FocusCanvasOnNewNode);
        }

        private void darkModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //ThemeManager writes the setting itself, so this goes through it rather than ToggleBoolSetting
            Theming.ThemeManager.SetDark(!Theming.ThemeManager.IsDark);
            ApplySettingEffects(new[] { Settings.DarkMode });

            //DockPanelSuite builds its panes from the theme, so the docking chrome can only change while
            //nothing is docked. Rather than making that the user's problem, rebuild the panels in place -
            //the level, the open composite and the 3D viewer all carry across.
            if (!Theming.ThemeManager.DockChromeNeedsRestart)
                return;

            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;
            bool rebuilt;
            try
            {
                rebuilt = RebuildDockChromeForTheme();
            }
            finally
            {
                Cursor = previous;
            }

            if (rebuilt)
                return;

            //Only if the rebuild couldn't be done does a restart come into it
            DialogResult result = MessageBox.Show(
                "Dark mode has been applied.\n\nThe docked panel tabs and borders will finish switching when OpenCAGE restarts.\n\nRestart now?",
                "Dark Mode",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
                Application.Restart();
        }

        private void giveOptionToDeleteEntityWhenNoNodesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleBoolSetting(Settings.OptionToDeleteEntityWithNode);
        }

        private void resetUILayoutsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Normal;
            Width = _defaultWidth;
            Height = _defaultHeight;

            bool preserveLevelViewer = _levelViewerPanel?.IsRunning == true;

            SettingsManager.SetFloat(Settings.DockSplitterLeft, DefaultSideDockPortion);
            SettingsManager.SetFloat(Settings.DockSplitterRight, DefaultEntityInspectorPortion);
            SettingsManager.SetFloat(Settings.DockSplitterBottom, _defaultSplitterDistance);
            SettingsManager.SetFloat(Settings.DockSplitterLevelViewer, 0.35f);
            SettingsManager.SetInteger(Settings.CompositeBrowserSplitter, 0);

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

        /// <summary>
        /// Rebuild the docked panels so the docking chrome can change theme without restarting.
        ///
        /// DockPanelSuite builds every pane, caption and splitter from the theme's factories, so it
        /// refuses to swap a theme while anything is docked. Everything docked here can be closed and
        /// recreated in place, though - which is what Reset UI Layouts already does - so the only real
        /// obstacle is the composite browser, which owns the loaded level. It hands the level over to
        /// its replacement rather than being reloaded, so nothing is read off disk and the level, the
        /// open composite and the 3D viewer all survive.
        /// </summary>
        /// <returns>False if the panels couldn't be rebuilt, in which case a restart is still needed.</returns>
        public bool RebuildDockChromeForTheme()
        {
            if (_compositeBrowser == null)
            {
                //Nothing is docked yet, so the theme applies directly
                return Theming.ThemeManager.ApplyToDockPanel(dockPanel);
            }

            //A value still being typed into the parameter grid lives in the editing control, not in the
            //parameter, until the control loses focus. Clicking the menu does that already, but this
            //makes it certain before anything is torn down.
            try
            {
                Validate();
            }
            catch
            {
            }

            bool preserveLevelViewer = _levelViewerPanel?.IsRunning == true;
            Composite loadedComposite = _compositeDisplay?.Populated == true ? _compositeDisplay.Composite : null;
            LevelContent content = _compositeBrowser.Content;
            bool levelDataLoaded = content?.IsLevelDataLoaded == true;

            try
            {
                //The browser keeps the level alive across the rebuild
                LevelContent retained = _compositeBrowser.DetachContent();

                CloseDockPanelContents(preserveLevelViewer);
                ForceCloseDockContent(ref _compositeBrowser, null);

                //Only now, with the panel genuinely empty, will the theme take
                if (!Theming.ThemeManager.ApplyToDockPanel(dockPanel))
                {
                    _compositeBrowser = new CompositeBrowser(retained);
                    RestoreDockLayoutAfterRebuild(loadedComposite, levelDataLoaded, preserveLevelViewer);
                    return false;
                }

                _compositeBrowser = new CompositeBrowser(retained);
                RestoreDockLayoutAfterRebuild(loadedComposite, levelDataLoaded, preserveLevelViewer);

                //The inner panel inside the composite display is new, and picked the theme up when it
                //was built; this settles whether anything is still outstanding
                Theming.ThemeManager.RecheckDockChrome();
                return true;
            }
            catch (Exception ex)
            {
                Debug.Log("Theme", "Rebuilding the dock layout for a theme change failed: " + ex);
                return false;
            }
        }

        private void RestoreDockLayoutAfterRebuild(Composite loadedComposite, bool levelDataLoaded, bool preserveLevelViewer)
        {
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
                _compositeDisplay.RepositionLevelViewerForLayoutReset();
                BeginInvoke(new Action(() =>
                {
                    _levelViewerPanel?.RefreshEmbeddedBounds();
                    _levelViewerPanel?.RestoreInputFocus();
                }));
            }

            _compositeBrowser?.ResetSplitter();
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
            ToggleBoolSetting(Settings.LaunchGameWhenSaved);
        }

        private void showGamePlatformToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleBoolSetting(Settings.ShowGamePlatform);
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
            ToggleBoolSetting(Settings.AskBeforeDeletingNode);
        }

        private void miscToolStripMenuItem_Click(object sender, EventArgs e)
        {
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

        EditAnimations _editAnimations = null;
        private void animationsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_editAnimations != null)
            {
                _editAnimations.FormClosed -= _editAnimations_FormClosed ;
                _editAnimations.Close();
            }

            _editAnimations = new EditAnimations();
            _editAnimations.Show();
            _editAnimations.FormClosed += _editAnimations_FormClosed;
        }
        private void _editAnimations_FormClosed(object sender, FormClosedEventArgs e)
        {
            _editAnimations = null;
        }

        EditBlendSets _editBlendSets = null;
        private void blendSetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_editBlendSets != null && !_editBlendSets.IsDisposed)
            {
                _editBlendSets.BringToFront();
                return;
            }

            _editBlendSets = new EditBlendSets();
            _editBlendSets.Show();
            _editBlendSets.FormClosed += (s, args) => _editBlendSets = null;
        }

        Process _behaviourEditor = null;
        private void behaviourTreesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            KillBehaviourTreeEditor();

            string editorPath = "legendplugin/";
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

        AnimTreeEditor animTreeEditor = null;
        private void animationTreesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (animTreeEditor != null)
            {
                animTreeEditor.FormClosed -= animTreeEditor_FormClosed;
                animTreeEditor.Close();
            }

            animTreeEditor = new AnimTreeEditor();
            animTreeEditor.Show();
        }
        private void animTreeEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            animTreeEditor = null;
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

        private void changelogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("https://opencage.co.uk/docs/changelog");
        }

        private void logABugToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/MattFiler/OpenCAGE/issues/new");
        }

        /* Unsaved changes when the viewport is about to load the level from disk: save now and open it,
           or leave it closed until the next level load. True when it can open now. */
        private bool ConfirmSaveBeforeOpeningViewport()
        {
            if (_compositeBrowser?.Content?.Level == null)
                return true;

#if USE_DIRTY_TRACKER
            if (!DirtyTracker.IsDirty)
                return true;
#endif

            DialogResult result = MessageBox.Show(
                "\"" + _compositeBrowser.Content.Level.Name + "\" has unsaved changes.\n\n" +
                "The viewport loads the level from disk, so it can't show them until the level is saved. Save now?\n\n" +
                "Yes: save, then open the viewport.\nNo: keep the viewport closed until a level is next loaded.",
                "Save before opening the viewport?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return false;

            SaveLevel(false, successMsg: false, allowLaunchGame: false);
#if USE_DIRTY_TRACKER
            if (DirtyTracker.IsDirty)
                return false; //the save was refused (a backup in progress): same as No
#endif
            return true;
        }

        /* A child instance outlives the primary that launched it often enough - closed first, or crashed -
           for the user to be left with no way to manage directories short of relaunching. Poll for the
           primary's lock, and take its menu over when it frees up. */
        private System.Windows.Forms.Timer _primaryInstanceTimer;
        private void WatchForPrimaryInstanceHandover()
        {
            _primaryInstanceTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _primaryInstanceTimer.Tick += (sender, e) =>
            {
                if (!PrimaryInstanceLock.TryAcquire())
                    return;

                _primaryInstanceTimer.Stop();
                manageGameDirectoriesToolStripMenuItem.Visible = true;
            };
            _primaryInstanceTimer.Start();
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
