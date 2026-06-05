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

        private CommandsDisplay _commandsDisplay = null;
        public CommandsDisplay CommandsDisplay => _commandsDisplay;

        private SelectLevel _levelSelect = null;

        private DiscordRpcClient _discord;

        private Dictionary<string, ToolStripMenuItem> _levelMenuItems = new Dictionary<string, ToolStripMenuItem>();
        private readonly Dictionary<uint, ToolStripMenuItem> _boxRenderFilterMenuItems = new Dictionary<uint, ToolStripMenuItem>();
        private readonly Dictionary<float, ToolStripMenuItem> _transformGridSnapMenuItems = new Dictionary<float, ToolStripMenuItem>();
        private readonly Dictionary<float, ToolStripMenuItem> _rotationSnapMenuItems = new Dictionary<float, ToolStripMenuItem>();

        private Thread _loadThread = null;
        private ProgressUI _progressUI = null;

        private float _defaultSplitterDistance = 0.25f;
        private int _defaultWidth;
        private int _defaultHeight;

        private bool _settingUp = true;

        private DarkModeCS _dm;

        public CommandsEditor(string level = null)
        {
            /*
            Level lvl = Utilities.LoadLevel("C:\\AlienData\\game\\pc", "production/tech_rnd_hzdlab");
            List<string> dump = new List<string>();
            foreach (Composite comp in lvl.Commands.Entries)
            {
                foreach (FunctionEntity func in comp.GetFunctionEntitiesOfType(FunctionType.Zone))
                {
                    Parameter p = func.GetParameter("force_visible_on_load");
                    if (p != null)
                    {
                        if (p.content.dataType == DataType.BOOL)
                        {
                            if (((cBool)p.content).value == true)
                            {
                                dump.Add("force_visible_on_load=true : " + ((cString)func.GetParameter("name").content).value);
                            }
                        }
                    }
                }
            }
            File.WriteAllLines("dump.txt", dump);
            */

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

            if (SettingsManager.GetFloat(Singleton.Settings.NumericStep, -1.0f) == -1.0f)
                SettingsManager.SetFloat(Singleton.Settings.NumericStep, 0.1f);
            if (SettingsManager.GetFloat(Singleton.Settings.NumericStepRot, -1.0f) == -1.0f)
                SettingsManager.SetFloat(Singleton.Settings.NumericStepRot, 1.0f);

            dockPanel.DockLeftPortion = SettingsManager.GetFloat(Singleton.Settings.CommandsSplitWidth, _defaultSplitterDistance);
            dockPanel.DockBottomPortion = SettingsManager.GetFloat(Singleton.Settings.SplitWidthMainBottom, _defaultSplitterDistance);
            dockPanel.DockRightPortion = SettingsManager.GetFloat(Singleton.Settings.SplitWidthMainRight, _defaultSplitterDistance);
            dockPanel.ShowDocumentIcon = true;

            _defaultWidth = Width;
            _defaultHeight = Height;

#if !DEBUG
            //Dev options
            DEBUG_ReloadLevel.Visible = false;
            connectToRuntimeUtils.Visible = false;
            toolStripSeparatorLv2.Visible = false;
            
            //WIP forms
            inputsToolStripMenuItem.Visible = false;
            scriptReadableVariablesToolStripMenuItem.Visible = false;
            voiceMappingsToolStripMenuItem.Visible = false;
            localisationToolStripMenuItem.Visible = false;
            levelTextDBsToolStripMenuItem.Visible = false;
            fontConfigToolStripMenuItem.Visible = false;
#endif

            WindowState = SettingsManager.GetString(Singleton.Settings.WindowState, "Normal") == "Maximized" ? FormWindowState.Maximized : FormWindowState.Normal;
            Width = SettingsManager.GetInteger(Singleton.Settings.WindowWidth, _defaultWidth);
            Height = SettingsManager.GetInteger(Singleton.Settings.WindowHeight, _defaultHeight);
            Resize += CommandsEditor_Resize;
            FormClosing += CommandsEditor_FormClosing;

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
            levelViewerDropdown.DropDown.Closing += DropDown_Closing;
            levelViewerDropdown.DropDownOpening += LevelViewerDropdown_DropDownOpening;
            renderFiltersToolStripMenuItem.MouseHover += (sender, e) => { ((ToolStripMenuItem)sender).PerformClick(); };
            renderFiltersToolStripMenuItem.DropDown.Closing += DropDown_Closing;
            transformGridSnapToolStripMenuItem.MouseHover += (sender, e) => { ((ToolStripMenuItem)sender).PerformClick(); };
            transformGridSnapToolStripMenuItem.DropDown.Closing += DropDown_Closing;
            rotationSnapToolStripMenuItem.MouseHover += (sender, e) => { ((ToolStripMenuItem)sender).PerformClick(); };
            rotationSnapToolStripMenuItem.DropDown.Closing += DropDown_Closing;
            SetupRenderFiltersMenu();
            SetupTransformSnapMenus();
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

#if SHIP_BUILD
            if (!Singleton.IsSteamworks && !SettingsManager.GetBool(Singleton.Settings.DidSteamPrompt))
            {
                SettingsManager.SetBool(Singleton.Settings.DidSteamPrompt, true);
                if (MessageBox.Show("Welcome to OpenCAGE!\nDid you know you can now download OpenCAGE via Steam?!\nCheck it out for achievements, stats, and more!", "Welcome to OpenCAGE", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    Process.Start("https://store.steampowered.com/app/3367530/OpenCAGE/");
                }
            }
#endif
        }

        private void SetupOptions()
        {
#if SHIP_BUILD
            if (Singleton.IsSteamworks)
            {
                enableLevelViewerToolStripMenuItem.Visible = false;

                if (!SettingsManager.IsSet(Singleton.Settings.ConnectToLevelViewer)) SettingsManager.SetBool(Singleton.Settings.ConnectToLevelViewer, true);
                connectToLevelViewer.Checked = !SettingsManager.GetBool(Singleton.Settings.ConnectToLevelViewer); connectToLevelViewer.PerformClick();
            }
            else
            {
                if (SettingsManager.GetBool(Singleton.Settings.LevelViewerEnabled))
                {
#endif
                    enableLevelViewerToolStripMenuItem.Visible = false;

                    if (!SettingsManager.IsSet(Singleton.Settings.ConnectToLevelViewer)) SettingsManager.SetBool(Singleton.Settings.ConnectToLevelViewer, true);
                    connectToLevelViewer.Checked = !SettingsManager.GetBool(Singleton.Settings.ConnectToLevelViewer); connectToLevelViewer.PerformClick();
#if SHIP_BUILD
                }
                else
                { 
                    openLevelViewerToolStripMenuItem.Visible = false;
                    toolStripSeparator1.Visible = false;
                    connectToLevelViewer.Visible = false;
                    focusOnSelectedToolStripMenuItem.Visible = false;
                    highlightAliasesToolStripMenuItem.Visible = false;
                    showCameraPositionToolStripMenuItem.Visible = false;
                    renderWireframeToolStripMenuItem.Visible = false;
                    hideNestedScriptEntitiesToolStripMenuItem.Visible = false;
                    transformGridSnapToolStripMenuItem.Visible = false;
                    rotationSnapToolStripMenuItem.Visible = false;
                    renderFiltersToolStripMenuItem.Visible = false;

                    // note to self, this is experimental stuff that's hidden in release currently anyway
                    toolStripSeparatorLv2.Visible = false;
                    connectToRuntimeUtils.Visible = false;
                }
            }
#endif

            if (!SettingsManager.IsSet(Singleton.Settings.RuntimeUtilsOpt)) SettingsManager.SetBool(Singleton.Settings.RuntimeUtilsOpt, false);
            connectToRuntimeUtils.Checked = SettingsManager.GetBool(Singleton.Settings.RuntimeUtilsOpt);
            if (connectToRuntimeUtils.Checked)
            {
                if (!RuntimeUtilsConnection.Send.Start())
                {
                    connectToRuntimeUtils.Checked = false;
                    SettingsManager.SetBool(Singleton.Settings.RuntimeUtilsOpt, false);
                }
            }
            if (!SettingsManager.IsSet(Singleton.Settings.UNITY_FocusEntity)) SettingsManager.SetBool(Singleton.Settings.UNITY_FocusEntity, true);
            focusOnSelectedToolStripMenuItem.Checked = !SettingsManager.GetBool(Singleton.Settings.UNITY_FocusEntity); focusOnSelectedToolStripMenuItem.PerformClick();
            if (!SettingsManager.IsSet(Singleton.Settings.UNITY_HighlightAliases)) SettingsManager.SetBool(Singleton.Settings.UNITY_HighlightAliases, true);
            highlightAliasesToolStripMenuItem.Checked = !SettingsManager.GetBool(Singleton.Settings.UNITY_HighlightAliases); highlightAliasesToolStripMenuItem.PerformClick();
            if (!SettingsManager.IsSet(Singleton.Settings.UNITY_ShowCameraPosition)) SettingsManager.SetBool(Singleton.Settings.UNITY_ShowCameraPosition, true);
            showCameraPositionToolStripMenuItem.Checked = !SettingsManager.GetBool(Singleton.Settings.UNITY_ShowCameraPosition); showCameraPositionToolStripMenuItem.PerformClick();
            renderWireframeToolStripMenuItem.Checked = !SettingsManager.GetBool(Singleton.Settings.UNITY_RenderWireframe); renderWireframeToolStripMenuItem.PerformClick();
            hideNestedScriptEntitiesToolStripMenuItem.Checked = !SettingsManager.GetBool(Singleton.Settings.UNITY_HideNestedScriptEntities); hideNestedScriptEntitiesToolStripMenuItem.PerformClick();
            resetRenderFiltersOnLoadToolStripMenuItem.Checked = !SettingsManager.GetBool(Singleton.Settings.ResetRenderFilters); resetRenderFiltersOnLoadToolStripMenuItem.PerformClick();

            showEntityIDs.Checked = !SettingsManager.GetBool(Singleton.Settings.ShowShortGuids); showEntityIDs.PerformClick();
            searchOnlyCompositeNames.Checked = !SettingsManager.GetBool(Singleton.Settings.CompNameOnlyOpt); searchOnlyCompositeNames.PerformClick();
            keepFunctionUsesWindowOpenToolStripMenuItem.Checked = !SettingsManager.GetBool(Singleton.Settings.KeepUsesWindowOpen); keepFunctionUsesWindowOpenToolStripMenuItem.PerformClick();
            writeInstancedResourcesExperimentalToolStripMenuItem.Checked = !SettingsManager.GetBool(Singleton.Settings.CompileInstances); writeInstancedResourcesExperimentalToolStripMenuItem.PerformClick();
            openGameOnSaveToolStripMenuItem.Checked = !SettingsManager.GetBool(Singleton.Settings.LaunchGameWhenSaved); openGameOnSaveToolStripMenuItem.PerformClick();
            showGamePlatformToolStripMenuItem.Checked = !SettingsManager.GetBool(Singleton.Settings.ShowGamePlatform); showGamePlatformToolStripMenuItem.PerformClick();

            if (!SettingsManager.IsSet(Singleton.Settings.ShowTexOpt)) SettingsManager.SetBool(Singleton.Settings.ShowTexOpt, true);
            useTexturedModelViewExperimentalToolStripMenuItem.Checked = !SettingsManager.GetBool(Singleton.Settings.ShowTexOpt); useTexturedModelViewExperimentalToolStripMenuItem.PerformClick();

            if (!SettingsManager.IsSet(Singleton.Settings.ShowSavedMsgOpt)) SettingsManager.SetBool(Singleton.Settings.ShowSavedMsgOpt, true);
            showConfirmationWhenSavingToolStripMenuItem.Checked = !SettingsManager.GetBool(Singleton.Settings.ShowSavedMsgOpt); showConfirmationWhenSavingToolStripMenuItem.PerformClick();

            if (!SettingsManager.IsSet(Singleton.Settings.EnableFileBrowser)) SettingsManager.SetBool(Singleton.Settings.EnableFileBrowser, true);
            showExplorerViewToolStripMenuItem.Checked = !SettingsManager.GetBool(Singleton.Settings.EnableFileBrowser); showExplorerViewToolStripMenuItem.PerformClick();

            if (!SettingsManager.IsSet(Singleton.Settings.AutoHideCompositeDisplay)) SettingsManager.SetBool(Singleton.Settings.AutoHideCompositeDisplay, true);
            autoHideExplorerViewToolStripMenuItem.Checked = !SettingsManager.GetBool(Singleton.Settings.AutoHideCompositeDisplay); autoHideExplorerViewToolStripMenuItem.PerformClick();

            if (!SettingsManager.IsSet(Singleton.Settings.SavePakAndBin)) SettingsManager.SetBool(Singleton.Settings.SavePakAndBin, true);
            savePAKAndBINToolStripMenuItem.Checked = !SettingsManager.GetBool(Singleton.Settings.SavePakAndBin); savePAKAndBINToolStripMenuItem.PerformClick();

            if (!SettingsManager.IsSet(Singleton.Settings.PopulateAllPinsOnCreateNode)) SettingsManager.SetBool(Singleton.Settings.PopulateAllPinsOnCreateNode, true);
            populateAllNodePinsWhenCreatedToolStripMenuItem.Checked = !SettingsManager.GetBool(Singleton.Settings.PopulateAllPinsOnCreateNode); populateAllNodePinsWhenCreatedToolStripMenuItem.PerformClick();

            if (!SettingsManager.IsSet(Singleton.Settings.OptionToDeleteEntityWithNode)) SettingsManager.SetBool(Singleton.Settings.OptionToDeleteEntityWithNode, true);
            giveOptionToDeleteEntityWhenNoNodesToolStripMenuItem.Checked = !SettingsManager.GetBool(Singleton.Settings.OptionToDeleteEntityWithNode); giveOptionToDeleteEntityWhenNoNodesToolStripMenuItem.PerformClick();

            if (!SettingsManager.IsSet(Singleton.Settings.AskBeforeDeletingNode)) SettingsManager.SetBool(Singleton.Settings.AskBeforeDeletingNode, true);
            showConfirmationWhenDeletingNodeToolStripMenuItem.Checked = !SettingsManager.GetBool(Singleton.Settings.AskBeforeDeletingNode); showConfirmationWhenDeletingNodeToolStripMenuItem.PerformClick();

#if SHIP_BUILD
            if (Singleton.IsSteamworks)
            {
                useStagingBranchToolStripMenuItem.Visible = false;
                checkForUpdatesToolStripMenuItem.Visible = false;
            }
            else
            {
                useStagingBranchToolStripMenuItem.Checked = !SettingsManager.GetBool(Singleton.Settings.UseStagingBranch); useStagingBranchToolStripMenuItem.PerformClick();
            }
#else
            useStagingBranchToolStripMenuItem.Visible = false;
            checkForUpdatesToolStripMenuItem.Visible = false;
#endif

            if (!SettingsManager.IsSet(Singleton.Settings.NodeColour_FunctionNode))
                SettingsManager.SetInteger(Singleton.Settings.NodeColour_FunctionNode, Color.FromArgb(30, 144, 255).ToArgb());
            if (!SettingsManager.IsSet(Singleton.Settings.NodeColour_FunctionNodeBottom))
                SettingsManager.SetInteger(Singleton.Settings.NodeColour_FunctionNodeBottom, Color.FromArgb(10, 109, 157).ToArgb());
            if (!SettingsManager.IsSet(Singleton.Settings.NodeColour_FunctionText))
                SettingsManager.SetInteger(Singleton.Settings.NodeColour_FunctionText, Color.White.ToArgb());

            if (!SettingsManager.IsSet(Singleton.Settings.NodeColour_AliasNode))
                SettingsManager.SetInteger(Singleton.Settings.NodeColour_AliasNode, Color.FromArgb(255, 114, 30).ToArgb());
            if (!SettingsManager.IsSet(Singleton.Settings.NodeColour_AliasNodeBottom))
                SettingsManager.SetInteger(Singleton.Settings.NodeColour_AliasNodeBottom, Color.FromArgb(196, 76, 29).ToArgb());
            if (!SettingsManager.IsSet(Singleton.Settings.NodeColour_AliasText))
                SettingsManager.SetInteger(Singleton.Settings.NodeColour_AliasText, Color.White.ToArgb());

            if (!SettingsManager.IsSet(Singleton.Settings.NodeColour_ProxyNode))
                SettingsManager.SetInteger(Singleton.Settings.NodeColour_ProxyNode, Color.FromArgb(35, 196, 22).ToArgb());
            if (!SettingsManager.IsSet(Singleton.Settings.NodeColour_ProxyNodeBottom))
                SettingsManager.SetInteger(Singleton.Settings.NodeColour_ProxyNodeBottom, Color.FromArgb(9, 153, 72).ToArgb());
            if (!SettingsManager.IsSet(Singleton.Settings.NodeColour_ProxyText))
                SettingsManager.SetInteger(Singleton.Settings.NodeColour_ProxyText, Color.White.ToArgb());

            if (!SettingsManager.IsSet(Singleton.Settings.NodeColour_InstanceNode))
                SettingsManager.SetInteger(Singleton.Settings.NodeColour_InstanceNode, Color.FromArgb(195, 30, 255).ToArgb());
            if (!SettingsManager.IsSet(Singleton.Settings.NodeColour_InstanceNodeBottom))
                SettingsManager.SetInteger(Singleton.Settings.NodeColour_InstanceNodeBottom, Color.FromArgb(118, 10, 157).ToArgb());
            if (!SettingsManager.IsSet(Singleton.Settings.NodeColour_InstanceText))
                SettingsManager.SetInteger(Singleton.Settings.NodeColour_InstanceText, Color.White.ToArgb());

            if (!SettingsManager.IsSet(Singleton.Settings.NodeColour_VariableNode))
                SettingsManager.SetInteger(Singleton.Settings.NodeColour_VariableNode, Color.Red.ToArgb());
            if (!SettingsManager.IsSet(Singleton.Settings.NodeColour_VariableText))
                SettingsManager.SetInteger(Singleton.Settings.NodeColour_VariableText, Color.White.ToArgb());

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
            //These options are dependent on external tools, so disable them if they don't exist
            if (!Directory.Exists(Singleton.PathToAI + "/DATA/MODTOOLS/REMOTE_ASSETS/legendplugin"))
                behaviourTreesToolStripMenuItem.Enabled = false;
#endif

            versionToolStripMenuItem.Text = "Version " + ProductVersion;
            _settingUp = false;
        }

        private void OnEntityAdded(Entity e)
        {
            Steam.UnlockAchievement(Steam.Achievements.CREATE_A_NEW_ENTITY);

            int entCount = SettingsManager.GetInteger(Singleton.Settings.EntityCounter) + 1;
            SettingsManager.SetInteger(Singleton.Settings.EntityCounter, entCount);
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
            KillLevelViewer();
            SettingsManager.SetFloat(Singleton.Settings.SplitWidthMainBottom, (float)dockPanel.DockBottomPortion);
            SettingsManager.SetFloat(Singleton.Settings.SplitWidthMainRight, (float)dockPanel.DockRightPortion);
        }

        //UI: remember width/height of editor
        private void CommandsEditor_Resize(object sender, EventArgs e)
        {
            switch (WindowState)
            {
                case FormWindowState.Normal:
                    SettingsManager.SetInteger(Singleton.Settings.WindowWidth, Width);
                    SettingsManager.SetInteger(Singleton.Settings.WindowHeight, Height);
                    break;
                case FormWindowState.Maximized:

                    break;
            }
            SettingsManager.SetString(Singleton.Settings.WindowState, WindowState.ToString());
        }

        private void OnCompositeSelectedForDiscord(Composite composite)
        {
            RichPresence newPresence = _discord.CurrentPresence.Copy();
            newPresence.Details = "Level: " + (_commandsDisplay?.Content?.Level?.Name ?? "No Level");
            newPresence.State = "Composite: " + EditorUtils.GetCompositeName(composite);
            _discord.SetPresence(newPresence);
            _discord.UpdateStartTime();

            if (_commandsDisplay?.Content?.Level == null)
                Steam.UpdatePresence(Steam.RichPresences.NO_PRESENCE);
            else
                Steam.UpdatePresence(Steam.RichPresences.EditingLevel, _commandsDisplay.Content.Level.Name);
        }

        private void OnDirtyChanged(bool dirty) => UpdateTitle();
        private void UpdateTitle()
        {
            string title = "OpenCAGE";
            if (SettingsManager.GetBool(Singleton.Settings.ShowGamePlatform))
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

            if (_commandsDisplay == null)
            {
                this.Text = title;
            }
            else
            {
                string[] levelBits = _commandsDisplay.Content.Level.Name.Split('/');
                this.Text = title + " - " + levelBits[levelBits.Length - 1] + " (" + _commandsDisplay.Content.Level.Name.Substring(0, _commandsDisplay.Content.Level.Name.Length - levelBits[levelBits.Length - 1].Length).TrimEnd('/') + ")";
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

            if (_commandsDisplay != null)
            {
                Singleton.Editor.DockPanel.ActiveAutoHideContent = null;
                string oldLevelName = _commandsDisplay.Content?.Level?.Name;
                if (oldLevelName != null)
                    _levelMenuItems[oldLevelName].Checked = false;

                _commandsDisplay.CloseAllChildTabs();
                _commandsDisplay.Close();
                _commandsDisplay = null;
                
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
            }

#if DEBUG
            //if (Directory.Exists(Singleton.PathToAI + "\\LatestBuiltData\\ENV"))
            //{
            //    Directory.Delete(Singleton.PathToAI + "\\DATA\\ENV\\" + level, true);
            //    CopyFilesRecursively(Singleton.PathToAI + "\\LatestBuiltData\\ENV\\" + level, Singleton.PathToAI + "\\DATA\\ENV\\" + level);
            //}
#endif

            _commandsDisplay = new CommandsDisplay(level);
            Singleton.OnLevelLoaded += ShowCommandsDisplayWhenLoaded;

            _progressUI = new ProgressUI();
            _progressUI.ShowLevelLoading(_commandsDisplay.Content.Level);
            _progressUI.BringToFront();
            this.BringToFront();
            this.Activate();
            EnableButtons(false, "Loading " + _commandsDisplay.Content.Level.Name + "...");

            _loadThread = new Thread(ThreadedLevelLoader);
            _loadThread.Start();

            _levelMenuItems[_commandsDisplay.Content.Level.Name].Checked = true;
            UpdateTitle();

            if (SettingsManager.GetBool(Singleton.Settings.ResetRenderFilters))
            {
                foreach (RenderFilterDefinitions.Definition definition in RenderFilterDefinitions.All)
                {
                    RenderFilters.SetEnabled(definition.FunctionType, false);
                }
                UnityConnection.Send.SendRenderFilterPacket();
                SetupRenderFiltersMenu();
            }

            Steam.UnlockAchievement(Steam.Achievements.FIRST_LOAD);
        }

        private void ThreadedLevelLoader()
        {
#if !CATHODE_FAIL_HARD
            try
            {
#endif
                _commandsDisplay.Content.Load();
#if !CATHODE_FAIL_HARD
            }
            catch
            {
                this.BeginInvoke(new Action(() =>
                {
                    CloseProgressUI();
                    EnableButtons(true, "");
                    //TODO: warn!
                }));
            }
#endif
        }

        private void CloseProgressUI()
        {
            if (_progressUI != null && !_progressUI.IsDisposed)
            {
                _progressUI.Close();
                _progressUI.Dispose();
                _progressUI = null;
            }
        }

        private void ShowCommandsDisplayWhenLoaded(LevelContent content)
        {
            Singleton.OnLevelLoaded -= ShowCommandsDisplayWhenLoaded;

            Singleton.Editor.BeginInvoke(new Action(() => 
            {
                CloseProgressUI();
                EnableButtons(true, "");

                _commandsDisplay.Resize += _commandsDisplay_Resize;
                _commandsDisplay.FormClosed += _commandsDisplay_FormClosed;
                _commandsDisplay.UpdateDockState();

                Singleton.Editor.Activate();
                Singleton.Editor.Focus();
            }));
        }

        private void _commandsDisplay_Resize(object sender, EventArgs e)
        {
            SettingsManager.SetFloat(Singleton.Settings.CommandsSplitWidth, (float)dockPanel.DockLeftPortion);
        }

        private void _commandsDisplay_FormClosed(object sender, FormClosedEventArgs e)
        {
            _commandsDisplay?.Dispose();
            _commandsDisplay = null;
        }

        private void saveLevel_Click(object sender, EventArgs e)
        {
            SaveLevel();
        }

        public void SaveLevel(bool successMsg = true)
        {
            if (_commandsDisplay == null) return;

            //Close alien down if it's open, it conflicts with our write locks!
            EditorUtils.CloseAI();

            Cursor.Current = Cursors.WaitCursor;
            statusText.Text = "Saving...";
            statusStrip.Update();

            _progressUI = new ProgressUI();
            _progressUI.ShowLevelSaving(_commandsDisplay.Content.Level);

            if (_commandsDisplay.CompositeDisplay != null)
                _commandsDisplay.CompositeDisplay.SaveAllFlowgraphs();

#if DEBUG
            if (SettingsManager.GetBool(Singleton.Settings.CompileInstances))
            {
                _commandsDisplay.Content.Level.Resources.Entries.Clear();
                _commandsDisplay.Content.Level.PhysicsMaps.Entries.Clear();
                //todo - clear others when i write them

                Instancing inst = new Instancing(_commandsDisplay.Content.Level);
                inst.GenerateInstances();
                inst.ProcessInstances();
            }
#endif

            //TODO: take a backup first
            _commandsDisplay.Content.Save();

            if (!_commandsDisplay.Content.Level.Commands.Compressed && SettingsManager.GetBool(Singleton.Settings.SavePakAndBin))
            {
                string ext = "BIN";
                if (Path.GetExtension(_commandsDisplay.Content.Level.Commands.Filepath).ToUpper() == ".BIN")
                    ext = "PAK";
                _commandsDisplay.Content.Level.Commands.Save(_commandsDisplay.Content.Level.Commands.Filepath.Substring(0, _commandsDisplay.Content.Level.Commands.Filepath.Length - 3) + ext, false);
            }

#if !DEBUG
            PatchManager.PatchFileIntegrityCheck(Singleton.Platform, Singleton.PathToAI);
            PatchManager.PatchPopupMessage(Singleton.Platform, Singleton.PathToAI);
            PatchManager.UpdateLevelListInPackages(Singleton.Platform, Singleton.PathToAI);

            PatchManager.PatchSkipFrontendFlag(Singleton.Platform, Singleton.PathToAI, SettingsManager.GetBool("OPT_SkipFE"));
            PatchManager.PatchNoUIFlag(Singleton.Platform, Singleton.PathToAI, SettingsManager.GetBool("OPT_HudDisabled"));
            PatchManager.PatchMemReplayLogFlag(Singleton.Platform, Singleton.PathToAI, SettingsManager.GetBool("OPT_Mem_Replay_Logs"));
            PatchManager.PatchUIPerfFlag(Singleton.Platform, Singleton.PathToAI, SettingsManager.GetBool("OPT_cUIEnabled_UIPerf"));

            if (SettingsManager.GetBool(Singleton.Settings.LaunchGameWhenSaved))
            {
                PatchManager.PatchLaunchMode(Singleton.Platform, Singleton.PathToAI, _commandsDisplay.Content.Level.Name);

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

            int saveCount = SettingsManager.GetInteger(Singleton.Settings.SaveCounter) + 1;
            SettingsManager.SetInteger(Singleton.Settings.SaveCounter, saveCount);
            if (saveCount >= 100)
                Steam.UnlockAchievement(Steam.Achievements.ONE_HUNDRED_SAVES);

            //if (saved)
            //{
                if (SettingsManager.GetBool(Singleton.Settings.ShowSavedMsgOpt) && successMsg)
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

        private Process _levelViewer = null;

        private void LevelViewerDropdown_DropDownOpening(object sender, EventArgs e)
        {
            RefreshLevelViewerMenuStateIfExited();
        }

        private void openLevelViewerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            KillLevelViewer();

            string editorPath = Singleton.PathToAI + "/DATA/MODTOOLS/REMOTE_ASSETS/levelviewer/";
            _levelViewer = Process.Start(new ProcessStartInfo
            {
                FileName = editorPath + "CathodeEditorGodot.exe",
                WorkingDirectory = editorPath,
            });

            if (_levelViewer == null)
                return;

            _levelViewer.EnableRaisingEvents = true;
            _levelViewer.Exited += LevelViewer_Exited;

            Steam.UnlockAchievement(Steam.Achievements.LEVEL_VIEWER_LAUNCHED);
            SetLevelViewerMenuOpen(true);
            levelViewerDropdown.HideDropDown();

            if (!SettingsManager.GetBool(Singleton.Settings.ConnectToLevelViewer))
            {
                connectToLevelViewer.PerformClick();
            }
        }

        private void LevelViewer_Exited(object sender, EventArgs e)
        {
            ReleaseLevelViewerProcess();
            SetLevelViewerMenuOpen(false);
        }

        private void SetLevelViewerMenuOpen(bool isOpen)
        {
            void Apply()
            {
                if (openLevelViewerToolStripMenuItem == null)
                    return;

                openLevelViewerToolStripMenuItem.Enabled = !isOpen;
                openLevelViewerToolStripMenuItem.Checked = isOpen;
            }

            if (InvokeRequired)
                BeginInvoke(new Action(Apply));
            else
                Apply();
        }

        private void RefreshLevelViewerMenuStateIfExited()
        {
            if (_levelViewer == null)
                return;

            try
            {
                _levelViewer.Refresh();
                if (!_levelViewer.HasExited)
                    return;
            }
            catch
            {
                // Process handle is gone (crashed/killed outside our tracking).
            }

            ReleaseLevelViewerProcess();
            SetLevelViewerMenuOpen(false);
        }

        private void ReleaseLevelViewerProcess()
        {
            if (_levelViewer == null)
                return;

            _levelViewer.Exited -= LevelViewer_Exited;
            try
            {
                _levelViewer.Dispose();
            }
            catch
            {
            }

            _levelViewer = null;
        }

        private void connectToLevelViewer_Click(object sender, EventArgs e)
        {
            connectToLevelViewer.Checked = !connectToLevelViewer.Checked;
            SettingsManager.SetBool(Singleton.Settings.ConnectToLevelViewer, connectToLevelViewer.Checked);

            if (connectToLevelViewer.Checked)
            {
                if (!UnityConnection.Send.Start())
                {
                    connectToLevelViewer.PerformClick();
                    MessageBox.Show("Failed to initialise Level Viewer connection.\nIs another instance of the script editor running?", "Connection failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                UnityConnection.Send.Stop();
            }
        }

        private void focusOnSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            focusOnSelectedToolStripMenuItem.Checked = !focusOnSelectedToolStripMenuItem.Checked;
            SettingsManager.SetBool(Singleton.Settings.UNITY_FocusEntity, focusOnSelectedToolStripMenuItem.Checked);
            UnityConnection.Send.SendSettingsPacket();
        }

        private void highlightAliasesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            highlightAliasesToolStripMenuItem.Checked = !highlightAliasesToolStripMenuItem.Checked;
            SettingsManager.SetBool(Singleton.Settings.UNITY_HighlightAliases, highlightAliasesToolStripMenuItem.Checked);
            UnityConnection.Send.SendSettingsPacket();
        }

        private void showCameraPositionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            showCameraPositionToolStripMenuItem.Checked = !showCameraPositionToolStripMenuItem.Checked;
            SettingsManager.SetBool(Singleton.Settings.UNITY_ShowCameraPosition, showCameraPositionToolStripMenuItem.Checked);
            UnityConnection.Send.SendSettingsPacket();
        }

        private void renderWireframeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            renderWireframeToolStripMenuItem.Checked = !renderWireframeToolStripMenuItem.Checked;
            SettingsManager.SetBool(Singleton.Settings.UNITY_RenderWireframe, renderWireframeToolStripMenuItem.Checked);
            UnityConnection.Send.SendSettingsPacket();
        }

        private void hideNestedScriptEntitiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            hideNestedScriptEntitiesToolStripMenuItem.Checked = !hideNestedScriptEntitiesToolStripMenuItem.Checked;
            SettingsManager.SetBool(Singleton.Settings.UNITY_HideNestedScriptEntities, hideNestedScriptEntitiesToolStripMenuItem.Checked);
            UnityConnection.Send.SendSettingsPacket();
        }

        private void resetRenderFiltersOnLoadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            resetRenderFiltersOnLoadToolStripMenuItem.Checked = !resetRenderFiltersOnLoadToolStripMenuItem.Checked;
            SettingsManager.SetBool(Singleton.Settings.ResetRenderFilters, resetRenderFiltersOnLoadToolStripMenuItem.Checked);
        }

        private void SetupTransformSnapMenus()
        {
            SetupTransformGridSnapMenu();
            SetupRotationSnapMenu();
            ApplyTransformSnapSelectionsFromSettings();
        }

        private void ApplyTransformSnapSelectionsFromSettings()
        {
            if (!SettingsManager.IsSet(Singleton.Settings.UNITY_TransformGridSnap))
                SettingsManager.SetFloat(Singleton.Settings.UNITY_TransformGridSnap, 0f);
            if (!SettingsManager.IsSet(Singleton.Settings.UNITY_RotationSnapDegrees))
                SettingsManager.SetFloat(Singleton.Settings.UNITY_RotationSnapDegrees, 0f);

            ApplyTransformGridSnapSelection(TransformSnapDefinitions.NormalizeGridSnap(
                SettingsManager.GetFloat(Singleton.Settings.UNITY_TransformGridSnap)));
            ApplyRotationSnapSelection(TransformSnapDefinitions.NormalizeRotationSnap(
                SettingsManager.GetFloat(Singleton.Settings.UNITY_RotationSnapDegrees)));
        }

        private void SetupTransformGridSnapMenu()
        {
            transformGridSnapToolStripMenuItem.DropDownItems.Clear();
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
                transformGridSnapToolStripMenuItem.DropDownItems.Add(item);
            }
        }

        private void SetupRotationSnapMenu()
        {
            rotationSnapToolStripMenuItem.DropDownItems.Clear();
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
                rotationSnapToolStripMenuItem.DropDownItems.Add(item);
            }
        }

        private void ApplyTransformGridSnapSelection(float value)
        {
            foreach (KeyValuePair<float, ToolStripMenuItem> entry in _transformGridSnapMenuItems)
                entry.Value.Checked = SnapValuesEqual(entry.Key, value);
        }

        private void ApplyRotationSnapSelection(float value)
        {
            foreach (KeyValuePair<float, ToolStripMenuItem> entry in _rotationSnapMenuItems)
                entry.Value.Checked = SnapValuesEqual(entry.Key, value);
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
            SettingsManager.SetFloat(Singleton.Settings.UNITY_TransformGridSnap, value);
            UnityConnection.Send.SendSettingsPacket();
        }

        private void RotationSnapMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;
            float value = (float)item.Tag;
            value = TransformSnapDefinitions.NormalizeRotationSnap(value);
            ApplyRotationSnapSelection(value);
            SettingsManager.SetFloat(Singleton.Settings.UNITY_RotationSnapDegrees, value);
            UnityConnection.Send.SendSettingsPacket();
        }

        private void SetupRenderFiltersMenu()
        {
            renderFiltersToolStripMenuItem.DropDownItems.Clear();
            _boxRenderFilterMenuItems.Clear();

            foreach (RenderFilterDefinitions.Definition definition in RenderFilterDefinitions.All
                .OrderBy(definition => definition.FunctionType.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                bool enabled = RenderFilters.IsEnabled(definition.FunctionTypeUInt);
                ToolStripMenuItem item = new ToolStripMenuItem(definition.FunctionType.ToString())
                {
                    CheckOnClick = false,
                    Checked = enabled,
                    Tag = definition.FunctionTypeUInt,
                    Image = RenderFilters.CreateMenuImage(definition, enabled),
                    ImageScaling = ToolStripItemImageScaling.None,
                };
                item.Click += BoxRenderFilterMenuItem_Click;
                _boxRenderFilterMenuItems[definition.FunctionTypeUInt] = item;
                renderFiltersToolStripMenuItem.DropDownItems.Add(item);
            }
        }

        private void BoxRenderFilterMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;
            uint functionType = (uint)item.Tag;
            item.Checked = !item.Checked;
            RenderFilters.SetEnabled(functionType, item.Checked);
            RenderFilters.UpdateMenuImage(item, functionType, item.Checked);
            UnityConnection.Send.SendRenderFilterPacket();
        }

        private void connectToRuntimeUtils_Click(object sender, EventArgs e)
        {
            connectToRuntimeUtils.Checked = !connectToRuntimeUtils.Checked;
            SettingsManager.SetBool(Singleton.Settings.RuntimeUtilsOpt, connectToRuntimeUtils.Checked);

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

        private void enableLevelViewerToolStripMenuItem_Click(object sender, EventArgs e)
        {
#if SHIP_BUILD
            enableLevelViewerToolStripMenuItem.Checked = !enableLevelViewerToolStripMenuItem.Checked;
            SettingsManager.SetBool(Singleton.Settings.LevelViewerEnabled, enableLevelViewerToolStripMenuItem.Checked);

            KillLevelViewer();

            if (!_settingUp)
            {
                if (_commandsDisplay?.Content?.Level != null)
                {
                    if (MessageBox.Show("Would you like to install the Level Viewer now? This will relaunch the app. Make sure you have saved!", "Level viewer download queued", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return;
                }
                UpdateManager.DoUpdate();
            }
#endif
        }

        private void KillLevelViewer()
        {
            if (_levelViewer == null)
                return;

            try
            {
                if (!_levelViewer.HasExited)
                {
                    _levelViewer.Kill();
                    _levelViewer.WaitForExit(2000);
                }
            }
            catch
            {
            }

            ReleaseLevelViewerProcess();
            SetLevelViewerMenuOpen(false);
        }

        private void showEntityIDs_Click(object sender, EventArgs e)
        {
            showEntityIDs.Checked = !showEntityIDs.Checked;
            SettingsManager.SetBool(Singleton.Settings.ShowShortGuids, showEntityIDs.Checked);

            _commandsDisplay?.Reload(true);
            //TODO: also reload hierarchy cache
        }

        private void searchOnlyCompositeNames_Click(object sender, EventArgs e)
        {
            searchOnlyCompositeNames.Checked = !searchOnlyCompositeNames.Checked;
            SettingsManager.SetBool(Singleton.Settings.CompNameOnlyOpt, searchOnlyCompositeNames.Checked);
        }

        private void showConfirmationWhenSavingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            showConfirmationWhenSavingToolStripMenuItem.Checked = !showConfirmationWhenSavingToolStripMenuItem.Checked;
            SettingsManager.SetBool(Singleton.Settings.ShowSavedMsgOpt, showConfirmationWhenSavingToolStripMenuItem.Checked);
        }

        private void useTexturedModelViewExperimentalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            useTexturedModelViewExperimentalToolStripMenuItem.Checked = !useTexturedModelViewExperimentalToolStripMenuItem.Checked;
            SettingsManager.SetBool(Singleton.Settings.ShowTexOpt, useTexturedModelViewExperimentalToolStripMenuItem.Checked);
        }

        private void showExplorerViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            showExplorerViewToolStripMenuItem.Checked = !showExplorerViewToolStripMenuItem.Checked;
            SettingsManager.SetBool(Singleton.Settings.EnableFileBrowser, showExplorerViewToolStripMenuItem.Checked);
            UpdateCommandsDisplayDockState();
        }

        private void autoHideExplorerViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            autoHideExplorerViewToolStripMenuItem.Checked = !autoHideExplorerViewToolStripMenuItem.Checked;
            SettingsManager.SetBool(Singleton.Settings.AutoHideCompositeDisplay, autoHideExplorerViewToolStripMenuItem.Checked);
            UpdateCommandsDisplayDockState();
        }

        private void keepFunctionUsesWindowOpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            keepFunctionUsesWindowOpenToolStripMenuItem.Checked = !keepFunctionUsesWindowOpenToolStripMenuItem.Checked;
            SettingsManager.SetBool(Singleton.Settings.KeepUsesWindowOpen, keepFunctionUsesWindowOpenToolStripMenuItem.Checked);
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
            SettingsManager.SetBool(Singleton.Settings.SavePakAndBin, savePAKAndBINToolStripMenuItem.Checked);
        }

        private void populateAllNodePinsWhenCreatedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            populateAllNodePinsWhenCreatedToolStripMenuItem.Checked = !populateAllNodePinsWhenCreatedToolStripMenuItem.Checked;
            SettingsManager.SetBool(Singleton.Settings.PopulateAllPinsOnCreateNode, populateAllNodePinsWhenCreatedToolStripMenuItem.Checked);
        }

        private void giveOptionToDeleteEntityWhenNoNodesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            giveOptionToDeleteEntityWhenNoNodesToolStripMenuItem.Checked = !giveOptionToDeleteEntityWhenNoNodesToolStripMenuItem.Checked;
            SettingsManager.SetBool(Singleton.Settings.OptionToDeleteEntityWithNode, giveOptionToDeleteEntityWhenNoNodesToolStripMenuItem.Checked);
        }

        private void resetUILayoutsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Normal;
            Width = _defaultWidth;
            Height = _defaultHeight;

            dockPanel.DockLeftPortion = _defaultSplitterDistance;
            dockPanel.DockRightPortion = _defaultSplitterDistance;
            dockPanel.DockBottomPortion = _defaultSplitterDistance;

            _commandsDisplay?.ResetSplitter();
            _commandsDisplay?.CompositeDisplay?.ResetPortions();
        }

        private void writeInstancedResourcesExperimentalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            writeInstancedResourcesExperimentalToolStripMenuItem.Checked = !writeInstancedResourcesExperimentalToolStripMenuItem.Checked;
            SettingsManager.SetBool(Singleton.Settings.CompileInstances, writeInstancedResourcesExperimentalToolStripMenuItem.Checked);
        }

        private void UpdateCommandsDisplayDockState()
        {
            if (_commandsDisplay == null)
            {
                Singleton.Editor.DockPanel.ActiveAutoHideContent = null;
                return;
            }
            _commandsDisplay.UpdateDockState();
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
            SettingsManager.SetBool(Singleton.Settings.LaunchGameWhenSaved, openGameOnSaveToolStripMenuItem.Checked);
        }

        private void showGamePlatformToolStripMenuItem_Click(object sender, EventArgs e)
        {
            showGamePlatformToolStripMenuItem.Checked = !showGamePlatformToolStripMenuItem.Checked;
            SettingsManager.SetBool(Singleton.Settings.ShowGamePlatform, showGamePlatformToolStripMenuItem.Checked);
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
            modelsToolStripMenuItem.Enabled = _commandsDisplay?.Content?.Level != null;
            materialsToolStripMenuItem.Enabled = _commandsDisplay?.Content?.Level != null;
            materialMappingsToolStripMenuItem.Enabled = _commandsDisplay?.Content?.Level != null;
            texturesToolStripMenuItem.Enabled = _commandsDisplay?.Content?.Level != null;
            galaxyToolStripMenuItem.Enabled = _commandsDisplay?.Content?.Level != null;
        }

        private void charactersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            assetSetsToolStripMenuItem.Enabled = _commandsDisplay?.Content?.Level != null;
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
            SettingsManager.SetBool(Singleton.Settings.AskBeforeDeletingNode, showConfirmationWhenDeletingNodeToolStripMenuItem.Checked);
        }

        private void miscToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //NOTE: When in compressed mode, we ALWAYS save in the BIN format, so hide this option in that case.
            savePAKAndBINToolStripMenuItem.Visible = _commandsDisplay?.Content?.Level?.Commands != null && _commandsDisplay.Content.Level.Commands.Compressed;

            //NOTE: We don't actually allow this to be changed (even though it could be done) because it's not much use, for now at least. Maybe some sort of conversion between compressed and uncompressed levels in future.
            writeCompressedToolStripMenuItem.Checked = _commandsDisplay?.Content?.Level?.Commands != null && _commandsDisplay.Content.Level.Commands.Compressed;
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

        private void useStagingBranchToolStripMenuItem_Click(object sender, EventArgs e)
        {
#if SHIP_BUILD
            useStagingBranchToolStripMenuItem.Checked = !useStagingBranchToolStripMenuItem.Checked;
            SettingsManager.SetBool(Singleton.Settings.UseStagingBranch, useStagingBranchToolStripMenuItem.Checked);
            SettingsManager.SetString(Singleton.Settings.RemoteBranch, useStagingBranchToolStripMenuItem.Checked ? "staging" : "master");
            if (!_settingUp)
            {
                if (_commandsDisplay?.Content?.Level != null)
                {
                    if (MessageBox.Show("Would you like to update now? This will relaunch the app. Make sure you have saved!", "Branch changed", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return;
                }
                UpdateManager.DoUpdate();
            }
#endif
        }

        private void checkForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
#if SHIP_BUILD
            if (UpdateManager.IsUpdateAvailable(Singleton.Version))
            {
                if (_commandsDisplay?.Content?.Level != null)
                {
                    if (MessageBox.Show("A new version of OpenCAGE is available! Would you like to update now? This will relaunch the app. Make sure you have saved!", "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return;
                }
                else
                {
                    MessageBox.Show("A new version of OpenCAGE is available!", "Update available", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                UpdateManager.DoUpdate();
            }
            else
            {
                MessageBox.Show("You are currently using version " + Singleton.Version + " which is the latest available on " + SettingsManager.GetString(Singleton.Settings.RemoteBranch, "master") + "!", "No update available", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
#endif
        }
    }
}
