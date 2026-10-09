using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CathodeLib.ObjectExtensions;
using OpenCAGE.Popups.UserControls;
using OpenCAGE;
using OpenCAGE.Undo;
using OpenCAGE.UnityConnection;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using WebSocketSharp;
using WeifenLuo.WinFormsUI.Docking;
using static CathodeLib.CompositeFlowgraphCompatibilityTable;
using static CathodeLib.CompositeFlowgraphTable;
using Path = System.IO.Path;

namespace OpenCAGE.DockPanels
{
    public partial class CompositeDisplay : DockContent
    {
        private const float DefaultLevelViewerDockTopPortion = 0.35f;

        private CompositeBrowser _compositeBrowser;
        public CompositeBrowser CompositeBrowser => _compositeBrowser;
        public LevelContent Content => _compositeBrowser?.Content;

        public bool Populated => _composite != null;

        private Composite _composite;
        public Composite Composite => _composite;

        private EntityList _entityList;
        public EntityList EntityListPanel => _entityList;

        private LevelViewerPanel _levelViewerPanel;
        public LevelViewerPanel LevelViewerPanel => _levelViewerPanel;

        public List<Flowgraph> Flowgraphs => _flowgraphs; //Really, I'd rather not expose this, but it's handy to be able to see flowgraph data that has been modified during the session. It should be treated as read only!
        private List<Flowgraph> _flowgraphs = new List<Flowgraph>();

        private EntityInspector _entityDisplay;
        public EntityInspector EntityDisplay => _entityDisplay;

        private CompositePath _path = new CompositePath();
        public CompositePath Path => _path;

        public bool SupportsFlowgraphs => FlowgraphLayoutManager.IsCompatible(Composite);

        public Action<Composite> OnCompositeDisplayReloaded;

        private static Mutex _mut = new Mutex();
        private bool _isSubbed = false;
        private bool _innerDockLayoutRestored = false;
        private bool _suppressLevelViewerLayoutSave;
        private double _lastSavedInnerDockTopPortion = -1d;
        private System.Windows.Forms.Panel _pathHeaderPanel = null;
        // Drops stale inspector rebuilds when the user clicks another entity mid-load/focus.
        private int _loadEntityGeneration = 0;

        //TODO: if the composite is modified, store the modification info in CompositeUtils.SetModificationInfo -> need to add the concept of "modifying" the composite first though, which should be done off of events when deleting/adding stuff (can also show this state in the UI)

        public CompositeDisplay(CompositeBrowser compositeBrowser, EntityInspector entityInspector, EntityList entityList, LevelViewerPanel levelViewerPanel)
        {
            _compositeBrowser = compositeBrowser;
            _entityDisplay = entityInspector;
            _entityDisplay.AttachCompositeDisplay(this);
            _entityList = entityList;
            _levelViewerPanel = levelViewerPanel;

            InitializeComponent();
            Theming.ThemeManager.ApplyToForm(this);
            EditorIcons.Bind(createVariableEntityToolStripMenuItem, EditorIcon.Parameter);
            EditorIcons.Bind(createFunctionEntityToolStripMenuItem, EditorIcon.Function);
            EditorIcons.Bind(createCompositeEntityToolStripMenuItem, EditorIcon.CompositeInstance);
            EditorIcons.Bind(createProxyEntityToolStripMenuItem, EditorIcon.Proxy);
            EditorIcons.Bind(createAliasToolStripMenuItem, EditorIcon.Alias);

            /* Made outside the designer's container, so nothing disposed it: a ToolTip hooks the top-level form and holds
               every control it has a tip for, so each closed display (and its inspector and level content) lived as long as
               the editor, through the back button's Click handler. */
            Disposed += (s, e) => _navigateBackTooltip.Dispose();

            CloseButton = false;
            CloseButtonVisible = false;

            SetupCompositeDisplayLayout();
            SetupLiveLinkResync();
            SetupLiveLinkActivity();

            dockPanel.ShowDocumentIcon = false; //todo: tabs should be smaller
            dockPanel.DocumentTabStripLocation = DocumentTabStripLocation.Bottom;
            Theming.ThemeManager.ApplyToDockPanel(dockPanel);

            this.FormClosed += CompositeDisplay_FormClosed;
            this.DockStateChanged += CompositeDisplay_DockStateChanged;

            if (_levelViewerPanel != null)
            {
                _levelViewerPanel.DockStateChanged += LevelViewerPanel_DockStateChanged;
                _levelViewerPanel.Resize += LevelViewerPanel_Resize;
            }

            dockPanel.Layout += InnerDockPanel_Layout;

            pathBreadcrumb.SegmentClicked += LoadPathSegment;

            Singleton.OnCompositeDisplayOpening?.Invoke(this);
        }

        public void ResetPortions()
        {
            SettingsManager.SetFloat(Settings.DockSplitterLevelViewer, DefaultLevelViewerDockTopPortion);
            _innerDockLayoutRestored = false;
            ApplyDefaultInnerDockLayout();
        }

        public void EnsureInnerDockLayoutRestored()
        {
            if (_innerDockLayoutRestored)
                return;

            _innerDockLayoutRestored = true;

            if (SettingsManager.IsSet(Settings.DockSplitterLevelViewer))
                ApplySavedDockTopPortion();
            else
                ApplyDefaultInnerDockLayout();
        }

        public void SaveInnerDockLayout()
        {
            if (dockPanel == null)
                return;

            try
            {
                if (ShouldPersistDockTopPortion())
                {
                    double storedPortion = ToStoredDockTopPortion(dockPanel.DockTopPortion);
                    SettingsManager.SetFloat(
                        Settings.DockSplitterLevelViewer,
                        (float)storedPortion);
                    _lastSavedInnerDockTopPortion = storedPortion;
                }
            }
            catch
            {
            }
        }

        private float GetSavedDockTopPortion()
        {
            float saved = SettingsManager.GetFloat(
                Settings.DockSplitterLevelViewer,
                DefaultLevelViewerDockTopPortion);
            if (saved <= 0f || saved > 1f)
                return DefaultLevelViewerDockTopPortion;
            return (float)Math.Max(0.05, Math.Min(0.95, saved));
        }

        private static double ToStoredDockTopPortion(double portion)
        {
            if (portion <= 0.0)
                return DefaultLevelViewerDockTopPortion;
            return Math.Max(0.05, Math.Min(0.95, portion));
        }

        private void ApplySavedDockTopPortion()
        {
            if (dockPanel == null)
                return;

            float portion = GetSavedDockTopPortion();
            if (Math.Abs(dockPanel.DockTopPortion - portion) < double.Epsilon)
            {
                _lastSavedInnerDockTopPortion = portion;
                return;
            }

            dockPanel.DockTopPortion = portion;
            _lastSavedInnerDockTopPortion = portion;
        }

        private bool ShouldPersistDockTopPortion()
        {
            if (_suppressLevelViewerLayoutSave || _levelViewerPanel == null)
                return false;

            return _levelViewerPanel.DockState != DockState.Hidden
                && _levelViewerPanel.DockState != DockState.Unknown;
        }

        private void SaveInnerDockLayoutIfPortionChanged()
        {
            if (dockPanel == null || !ShouldPersistDockTopPortion())
                return;

            double portion = dockPanel.DockTopPortion;
            if (Math.Abs(portion - _lastSavedInnerDockTopPortion) < double.Epsilon)
                return;

            SaveInnerDockLayout();
        }

        private void LevelViewerPanel_Resize(object sender, EventArgs e)
        {
            SaveInnerDockLayoutIfPortionChanged();
        }

        private void InnerDockPanel_Layout(object sender, LayoutEventArgs e)
        {
            SaveInnerDockLayoutIfPortionChanged();
        }

        public void RefreshInnerDockLayoutAfterResize()
        {
            if (IsDisposed || Disposing || dockPanel == null || dockPanel.IsDisposed)
                return;

            try
            {
                dockPanel.PerformLayout();
            }
            catch
            {
            }
        }

        public void ApplyDefaultInnerDockLayout()
        {
            dockPanel.DockTopPortion = DefaultLevelViewerDockTopPortion;
            _lastSavedInnerDockTopPortion = DefaultLevelViewerDockTopPortion;
            SaveInnerDockLayout();
        }

        private void CompositeDisplay_DockStateChanged(object sender, EventArgs e)
        {
            if (DockState == DockState.Hidden || DockState == DockState.Unknown)
                return;

            EnsureInnerDockLayoutRestored();
            SaveInnerDockLayout();
        }

        private void LevelViewerPanel_DockStateChanged(object sender, EventArgs e)
        {
            if (_suppressLevelViewerLayoutSave)
                return;

            SaveInnerDockLayout();
        }

        public void DetachLevelViewerPanel()
        {
            if (_levelViewerPanel == null)
                return;

            _levelViewerPanel.DockStateChanged -= LevelViewerPanel_DockStateChanged;
            _levelViewerPanel.Resize -= LevelViewerPanel_Resize;

            try
            {
                _levelViewerPanel.Hide();
                if (_levelViewerPanel.DockHandler.DockPanel != null)
                    _levelViewerPanel.DockHandler.Close();
            }
            catch
            {
            }
        }

        public void ReleaseLevelViewerForLayoutReset()
        {
            if (_levelViewerPanel == null)
                return;

            _levelViewerPanel.DockStateChanged -= LevelViewerPanel_DockStateChanged;
            _levelViewerPanel.Resize -= LevelViewerPanel_Resize;
            _levelViewerPanel.UndockForLayoutReset();
        }

        public void RepositionLevelViewerForLayoutReset()
        {
            if (_levelViewerPanel == null || dockPanel == null || !_levelViewerPanel.IsRunning)
                return;

            ApplySavedDockTopPortion();
            _levelViewerPanel.DockStateChanged += LevelViewerPanel_DockStateChanged;
            _levelViewerPanel.Resize += LevelViewerPanel_Resize;
            _levelViewerPanel.Show(dockPanel, DockState.DockTop);
            _levelViewerPanel.RefreshEmbeddedBounds();
            SaveInnerDockLayout();
        }

        public void ShowLevelViewerPanel(bool activate = true)
        {
            if (_levelViewerPanel == null || dockPanel == null)
                return;

            _suppressLevelViewerLayoutSave = true;
            try
            {
                EnsureInnerDockLayoutRestored();
                ApplySavedDockTopPortion();

                if (!UnhideLevelViewerWithoutActivating()
                    && (_levelViewerPanel.DockPanel != dockPanel || _levelViewerPanel.DockState == DockState.Hidden))
                    _levelViewerPanel.Show(dockPanel, DockState.DockTop);

                if (_levelViewerPanel.IsRunning)
                    _levelViewerPanel.RefreshEmbeddedBounds();

                SaveInnerDockLayout();
            }
            finally
            {
                ScheduleLevelViewerLayoutSaveResume();
            }

            /* Activating moves keyboard focus - out of the viewer's window when it has it - and that waits on the viewer's
               thread (its window is parented into ours, sharing input state). While the viewer is busy (a populate, a
               big spawn) OpenCAGE froze here until it was done. The panel is shown either way; focus is only moved when
               the viewer can take the messages. */
            if (activate && !UnityConnection.ViewerBusy.Likely && _levelViewerPanel.IsViewerResponding(50))
                _levelViewerPanel.Activate();
        }

        public void EnsureLevelViewerDocked()
        {
            if (_levelViewerPanel == null || dockPanel == null)
                return;

            EnsureInnerDockLayoutRestored();
            ApplySavedDockTopPortion();

            //A level load starting reaches this with the viewer still busy on the last one: froze 20 s in the stress run
            if (UnhideLevelViewerWithoutActivating())
                SaveInnerDockLayout();
            else if (_levelViewerPanel.DockPanel != dockPanel || _levelViewerPanel.DockState == DockState.Hidden)
            {
                _levelViewerPanel.Show(dockPanel, DockState.DockTop);
                SaveInnerDockLayout();
            }

            if (_levelViewerPanel.IsRunning)
                _levelViewerPanel.RefreshEmbeddedBounds();
        }

        /* Show ends by activating the panel, which gives the focus back to the window it last had focused - the viewer's -
           and waited on the viewer's thread, 6 s into a populate in the stress run, whatever the caller wanted. A panel that
           is only hidden here comes back without that when the viewer is busy. True when it was brought back that way. */
        private bool UnhideLevelViewerWithoutActivating()
        {
            if (_levelViewerPanel.DockPanel != dockPanel || !_levelViewerPanel.IsHidden || _levelViewerPanel.DockState == DockState.Unknown
                || !UnityConnection.ViewerBusy.FocusMoveWouldWait)
                return false;

            _levelViewerPanel.IsHidden = false;
            DockPane pane = _levelViewerPanel.Pane;
            if (pane != null && pane.ActiveContent != _levelViewerPanel && !pane.IsActivated && pane.DisplayingContents.Contains(_levelViewerPanel))
                pane.ActiveContent = _levelViewerPanel;
            return true;
        }

        public void HideLevelViewerPanelForLoad()
        {
            if (_levelViewerPanel == null || _levelViewerPanel.DockState == DockState.Hidden)
                return;

            /* Hiding a panel with focus in it hands the focus on, and with focus in the viewer's window that waits for the
               viewer's thread: a load started while the viewer was busy froze until it was done. It stays up instead - the
               viewer shows its own loading screen - and ShowLevelViewerPanel takes it as it finds it. */
            if (_levelViewerPanel.ContainsFocus && UnityConnection.ViewerBusy.FocusMoveWouldWait)
                return;

            _suppressLevelViewerLayoutSave = true;
            try
            {
                _levelViewerPanel.Hide();
            }
            finally
            {
                ScheduleLevelViewerLayoutSaveResume();
            }
        }

        private void ScheduleLevelViewerLayoutSaveResume()
        {
            if (IsHandleCreated && !IsDisposed)
            {
                BeginInvoke(new Action(() => _suppressLevelViewerLayoutSave = false));
                return;
            }

            _suppressLevelViewerLayoutSave = false;
        }

        public void HideLevelViewerPanel()
        {
            if (_levelViewerPanel == null || _levelViewerPanel.DockState == DockState.Hidden)
                return;

            _levelViewerPanel.Hide();
        }

        private System.Windows.Forms.Button _navigateBackButton;
        private System.Windows.Forms.Button _navigateBackDropDown;
        private System.Windows.Forms.ContextMenuStrip _navigateBackMenu;

        private void SetupCompositeDisplayLayout()
        {
            const int pathRowHeight = 24;

            Controls.Remove(dockPanel);
            Controls.Remove(instanceInfo);
            Controls.Remove(pathBreadcrumb);
            Controls.Remove(toolStrip1);

            pathBreadcrumb.Anchor = AnchorStyles.None;
            instanceInfo.Anchor = AnchorStyles.None;

            _pathHeaderPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = pathRowHeight,
                Name = "pathHeaderPanel",
            };

            //Back sits to the left of the path it navigates, the way a file browser does it
            _navigateBackMenu = new ContextMenuStrip();
            _navigateBackMenu.Opening += NavigateBackMenu_Opening;

            _navigateBackButton = new System.Windows.Forms.Button
            {
                Name = "navigateBack",
                Text = "\u25C0",
                FlatStyle = FlatStyle.Popup,
                TabStop = false,
                UseVisualStyleBackColor = true,
            };
            _navigateBackButton.Click += NavigateBackButton_Click;

            _navigateBackDropDown = new System.Windows.Forms.Button
            {
                Name = "navigateBackHistory",
                Text = "\u25BE",
                FlatStyle = FlatStyle.Popup,
                TabStop = false,
                UseVisualStyleBackColor = true,
            };
            _navigateBackDropDown.Click += NavigateBackDropDown_Click;

            _pathHeaderPanel.Controls.Add(_navigateBackButton);
            _pathHeaderPanel.Controls.Add(_navigateBackDropDown);
            _pathHeaderPanel.Controls.Add(pathBreadcrumb);
            _pathHeaderPanel.Controls.Add(instanceInfo);
            _pathHeaderPanel.Resize += PathHeaderPanel_Resize;

            toolStrip1.Dock = DockStyle.Top;
            dockPanel.Dock = DockStyle.Fill;

            Controls.Add(dockPanel);
            Controls.Add(toolStrip1);
            Controls.Add(_pathHeaderPanel);

            PathHeaderPanel_Resize(_pathHeaderPanel, EventArgs.Empty);
            RefreshNavigateBackState();
        }

        private const int NavigateBackWidth = 26;
        private const int NavigateBackDropDownWidth = 16;

        private void PathHeaderPanel_Resize(object sender, EventArgs e)
        {
            if (_pathHeaderPanel == null)
                return;

            int rowHeight = _pathHeaderPanel.ClientSize.Height;
            int controlHeight = Math.Min(22, rowHeight);
            int y = Math.Max(0, (rowHeight - controlHeight) / 2);
            int infoWidth = instanceInfo.Width;

            int x = 0;
            if (_navigateBackButton != null)
            {
                _navigateBackButton.SetBounds(x, y, NavigateBackWidth, controlHeight);
                x += NavigateBackWidth;
            }
            if (_navigateBackDropDown != null)
            {
                _navigateBackDropDown.SetBounds(x, y, NavigateBackDropDownWidth, controlHeight);
                x += NavigateBackDropDownWidth + 4;
            }

            instanceInfo.SetBounds(
                Math.Max(0, _pathHeaderPanel.ClientSize.Width - infoWidth),
                y,
                infoWidth,
                controlHeight);
            pathBreadcrumb.SetBounds(
                x,
                y,
                Math.Max(0, _pathHeaderPanel.ClientSize.Width - infoWidth - 2 - x),
                controlHeight);
        }

        /* Grey the back controls out when there's nowhere to go, and name the destination in the tooltip */
        private void RefreshNavigateBackState()
        {
            if (_navigateBackButton == null || _navigateBackDropDown == null)
                return;

            List<CompositeNavigationHistory.Entry> history = CompositeNavigationHistory.GetHistory();
            bool canGoBack = history.Count != 0;

            _navigateBackButton.Enabled = canGoBack;
            _navigateBackDropDown.Enabled = canGoBack;

            string tooltip = canGoBack
                ? "Back to " + DescribeHistoryEntry(history[0])
                : "No composites to go back to";
            _navigateBackTooltip.SetToolTip(_navigateBackButton, tooltip);
            _navigateBackTooltip.SetToolTip(_navigateBackDropDown, "Recently visited composites");
        }

        private readonly System.Windows.Forms.ToolTip _navigateBackTooltip = new System.Windows.Forms.ToolTip();

        private void NavigateBackButton_Click(object sender, EventArgs e)
        {
            NavigateBackTo(0);
        }

        private void NavigateBackDropDown_Click(object sender, EventArgs e)
        {
            if (_navigateBackDropDown == null || _navigateBackMenu == null)
                return;

            _navigateBackMenu.Show(_navigateBackDropDown, new Point(0, _navigateBackDropDown.Height));
        }

        private void NavigateBackMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _navigateBackMenu.Items.Clear();

            List<CompositeNavigationHistory.Entry> history = CompositeNavigationHistory.GetHistory();
            if (history.Count == 0)
            {
                e.Cancel = true;
                return;
            }

            for (int i = 0; i < history.Count; i++)
            {
                Composite composite = CompositeNavigationHistory.ResolveComposite(history[i]);
                ToolStripMenuItem item = new ToolStripMenuItem(DescribeHistoryEntry(history[i]))
                {
                    Tag = i,
                    ToolTipText = composite?.name,
                };
                item.Click += NavigateBackMenuItem_Click;
                _navigateBackMenu.Items.Add(item);
            }
        }

        /* Name the composite, and note the hierarchy it sat in so two entries for the same composite
           opened from different places can be told apart. */
        private string DescribeHistoryEntry(CompositeNavigationHistory.Entry entry)
        {
            Composite composite = CompositeNavigationHistory.ResolveComposite(entry);
            if (composite == null)
                return "(missing composite)";

            string name = EditorUtils.GetCompositeName(composite);
            if (entry.PathEntities.Count == 0)
                return name;

            Composite entryComposite = CompositeNavigationHistory.ResolveEntryComposite(entry);
            return name + "  (in " + (entryComposite == null ? "?" : EditorUtils.GetCompositeName(entryComposite)) + ")";
        }

        private void NavigateBackMenuItem_Click(object sender, EventArgs e)
        {
            if (!(sender is ToolStripMenuItem item) || !(item.Tag is int index))
                return;

            NavigateBackTo(index);
        }

        /* Going back consumes the entries up to the chosen one, so it can't bounce between two forever */
        private void NavigateBackTo(int index)
        {
            CompositeNavigationHistory.Entry target = CompositeNavigationHistory.StepBack(index);
            if (target == null)
            {
                RefreshNavigateBackState();
                return;
            }

            Composite entryComposite = CompositeNavigationHistory.ResolveEntryComposite(target);
            Composite composite = CompositeNavigationHistory.ResolveComposite(target);
            if (composite == null)
            {
                //Deleted since we were there - nothing to go back to
                RefreshNavigateBackState();
                return;
            }

            _suppressNavigationHistory = true;
            try
            {
                //Back into another hierarchy: its entry becomes the scene's root first, so the viewer builds that scene
                bool pathResolves = PathResolvesFrom(entryComposite, target.PathEntities, composite);
                if (pathResolves)
                    OpenAsSceneRoot(entryComposite);

                //Replay the drill path so the breadcrumb comes back the way it was, not just the composite.
                //A missing entry composite still leaves somewhere valid to land, so it falls through.
                bool restored = pathResolves
                    && ApplyViewerSelectionPath(
                        entryComposite,
                        target.PathEntities.Select(o => o.AsUInt32).ToList(),
                        false,
                        entity => GetChildCompositeForNavigation(entity));

                //No path to replay, or a hop that no longer resolves - fall back to the composite alone. Shown already as a
                //step down from another root, the browser's open would leave it there: opened at the top here instead
                if (!restored)
                {
                    if (_composite == composite && _path.AllEntities.Count != 0)
                        PopulateUI(composite);
                    else
                        CompositeBrowser?.LoadComposite(composite);
                }
            }
            finally
            {
                _suppressNavigationHistory = false;
            }

            RefreshNavigateBackState();
        }

        private void OnLevelLoadedClearNavigation(LevelContent content)
        {
            /* Raised on the level loader's thread. Refreshing the back button sets its tooltip, which
             * reaches for the button's window handle - and on the first load of a session the display
             * has not been shown yet, so that CREATED the handle, and every parent up to this form, on
             * the loader thread. Nothing complained until exit, when a nameless child of a panel docked
             * in here (with no handle of its own) checked its nearest ancestor's thread from the UI
             * thread and threw the cross-thread exception in the inspector's Dispose. This form cannot
             * marshal for itself before it has a handle, so the editor does it. */
            CommandsEditor editor = Singleton.Editor;
            if (editor != null && !editor.IsDisposed && editor.InvokeRequired)
            {
                try { editor.BeginInvoke(new Action(() => OnLevelLoadedClearNavigation(content))); }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }
            if (IsDisposed)
                return;

            _currentPlace = null;
            RefreshNavigateBackState();
        }

        /// <summary>The composite a drill hop steps into, or null when the entity isn't an instance.</summary>
        //Every hop of a drill path still instances a composite, from the entry down, and it still lands where it did
        private bool PathResolvesFrom(Composite entry, IReadOnlyList<ShortGuid> pathEntities, Composite landing)
        {
            if (entry == null || landing == null || pathEntities == null || pathEntities.Count == 0)
                return false;
            Composite hop = entry;
            foreach (ShortGuid id in pathEntities)
            {
                Entity entity = hop.GetEntityByID(id);
                hop = entity != null ? GetChildCompositeForNavigation(entity) : null;
                if (hop == null)
                    return false;
            }
            //An instance re-pointed since (Create Composite Variant switches it to the copy) leads somewhere the history doesn't name
            return hop.shortGUID == landing.shortGUID;
        }

        /// <summary>
        /// Make the entry the scene's root before a drill path is walked from it: opened at the top level, as the browser opens
        /// a composite, so the viewer is sent COMPOSITE_SELECTED and builds that scene. A walk from anything else rebuilds the
        /// breadcrumb from the entry with no word to the viewer, which keeps its old root and judges it by a path from another
        /// one (all of it greyed out, or the wrong placements bright). Nothing to do when the entry is the root already.
        /// </summary>
        internal void OpenAsSceneRoot(Composite entry)
        {
            if (entry == null || !Populated)
                return;
            Composite sceneRoot = _path.AllComposites.FirstOrDefault() ?? _composite;
            if (sceneRoot?.shortGUID == entry.shortGUID)
                return;
            //Shown already, a step down from another root: the browser's open of the composite on screen does nothing
            if (_composite == entry)
                PopulateUI(entry);
            else
                CompositeBrowser?.LoadComposite(entry);
        }

        /// <summary>
        /// A navigation made of several moves (an open, then a walk down from it) kept as one step in the Back history: where it
        /// started is recorded once, not each stop on the way.
        /// </summary>
        internal T NavigateAsOneStep<T>(Func<T> navigate)
        {
            CompositeNavigationHistory.Entry before = _currentPlace;
            bool wasSuppressed = _suppressNavigationHistory;
            _suppressNavigationHistory = true;
            try
            {
                return navigate();
            }
            finally
            {
                _suppressNavigationHistory = wasSuppressed;
                if (!wasSuppressed && before != null && _currentPlace != null && !before.SamePlaceAs(_currentPlace))
                    CompositeNavigationHistory.Record(before);
                RefreshNavigateBackState();
            }
        }

        private Composite GetChildCompositeForNavigation(Entity entity)
        {
            Commands commands = Content?.Level?.Commands;
            if (commands == null || entity == null || entity.variant != EntityVariant.FUNCTION)
                return null;

            FunctionEntity function = (FunctionEntity)entity;
            if (function.function.IsFunctionType)
                return null;

            return commands.GetComposite(function.function);
        }

        private bool _suppressNavigationHistory = false;

        //Where we are now, snapshotted once navigation settles - the place a later Back returns to
        private CompositeNavigationHistory.Entry _currentPlace = null;

        private void OnCompositeRenamed(Composite composite, string name)
        {
            if (!Populated || (!Path.AllComposites.Contains(composite) && composite != _composite)) return;
            this.Text = EditorUtils.GetCompositeName(_composite);
            _entityList.UpdateTitle();
            UpdatePathBreadcrumb();
            RefreshNavigateBackState();
        }

        private void OnCompsoiteDeleted(Composite composite)
        {
            if (!Populated)
                return;

            if (Path.AllComposites.Contains(composite) || _composite == composite)
            {
                /* Up to the deepest composite still there, in one step: the hop into the first one gone went with it, so that
                   is one above it. Stepping up a composite at a time reloaded each deleted one on the way (the viewer told
                   to show a composite that no longer exists). Every composite a delete takes is out of the script before the
                   first of these events. */
                List<Composite> entries = Content?.Level?.Commands?.Entries;
                List<Composite> above = Path.AllComposites;
                int firstGone = above.FindIndex(o => o == null || o == composite || entries == null || !entries.Contains(o));
                int land = (firstGone == -1 ? above.Count : firstGone) - 1;
                //Nowhere left to step up to (the removed composite is where the path started): closed, as for the composite
                //on screen removed with no path
                if (land < 0)
                {
                    CompositeBrowser?.CloseAllChildTabs();
                    return;
                }
                LoadPathSegment(land);
            }

            //A deleted composite can't be navigated back to. The history filters it out on read, but
            //the button would otherwise sit enabled offering somewhere that no longer exists.
            RefreshNavigateBackState();

            //Its instances went with it, without an OnEntityDeleted each: a proxy whose path ran
            //through one is dead now
            RestyleAllProxies();
        }

        //And an undo brings them back the same way, so a dead proxy may be alive again
        private void OnCompositeAddedRestyleProxies(Composite composite)
        {
            if (Populated)
                RestyleAllProxies();
        }

        /* Redraw every proxy on this display - nodes, list rows, markers - after a change that can alter
           what resolves without naming an entity (a composite added or deleted, with all its instances). */
        private void RestyleAllProxies()
        {
            if (_composite == null || Content?.Level?.Commands?.Utils == null)
                return;
            RebuildProxiedEntityCache();
            RefreshNodeMarkers();
            foreach (Flowgraph flowgraph in _flowgraphs)
                flowgraph?.RestyleProxies();
            foreach (ProxyEntity proxy in _composite.proxies)
                _entityList?.List?.UpdateEntityInList(proxy);
        }

        //Saves and compiles all Flowgraph layouts for this Composite
        public void SaveAllFlowgraphs()
        {
            if (Composite != null && Content != null && Content.Level.Commands != null && Content.Level.Commands.Utils != null && SupportsFlowgraphs)
            {
#if DEBUG
                int ogCount = Content.Level.Commands.Utils.CountLinks(_composite);
#endif
                int newCount = 0;
                Content.Level.Commands.Utils.ClearAllLinks(_composite);
                for (int i = 0; i < _flowgraphs.Count; i++)
                {
                    if (_flowgraphs[i] != null)
                    {
                        newCount += _flowgraphs[i].SaveAndCompile();
                    }
                }
                Debug.Log("Composite Display", System.IO.Path.GetFileName(Composite.name) + " -> Created " + newCount + " links from flowgraph pages!");
#if DEBUG
                if (ogCount != newCount)
                {
                    Debug.Log("Composite Display", "WARNING: Previously had " + ogCount + " links, now have " + newCount + " (difference of " + Math.Abs(ogCount - newCount) + "). If you did not change any layouts, this could be an error!");
                }
                else
                {
                    Debug.Log("Composite Display", "The number of links matches the previous count of " + ogCount);
                }
#endif

                var visibleFlowgraph = _flowgraphs.FirstOrDefault(o => o.Visible);
                if (visibleFlowgraph != null)
                {
                    FlowgraphLayoutManager.SetSelectedPage(Composite, visibleFlowgraph.FlowgraphName);
                }
            }
        }

        /* Call this to show the CompositeDisplay with the requested Composite content. With a path, the composite is
           one the user was stepped down into (put back after an import or a rebuild of the panels closed the display):
           the path goes back before the reload, so everything the reload works out from it - the breadcrumb, the zone
           colours, the place Back returns from, what the viewer is told - has it. */
        public void PopulateUI(Composite composite, CompositePath.Snapshot path = null)
        {
            if (composite == null || IsDisposed || Disposing)
                return;

            LevelContent content = Content;
            if (content == null)
                return;

            content.EnsureEditorUtils();
            if (content.EditorUtils == null)
                return;

            if (_entityList == null || _entityList.IsDisposed || _entityList.List == null)
                return;

            Debug.Log("Composite Display", "PopulateUI called for " + composite.shortGUID.ToByteString() + " (" + composite.name + ")");

            EnsureInnerDockLayoutRestored();

            //If we're changing composite, we should store the flowgraph layouts from the previous one
            SaveAllFlowgraphs();

            if (!_isSubbed)
            {
                _entityList.List.AllowMultiSelect = true; //multi-select editing is supported here (and only here)
                _entityList.List.SelectedEntityChanged += OnEntityListSelectionChanged;
                _entityList.List.SelectedEntitiesChanged += OnEntityListMultiSelectionChanged;
                Singleton.OnCompositeRenamed += OnCompositeRenamed;
                Singleton.OnCompositeDeleted += OnCompsoiteDeleted;
                Singleton.OnCompositeAdded += OnCompositeAddedRestyleProxies;
                Singleton.OnEntityAdded += ReloadUIForNewEntity;
                Singleton.OnEntityDeleted += ReloadUIForDeletedEntity;
                //The display outlives a level change, so the place we think we're in has to go with it
                Singleton.OnLevelLoaded += OnLevelLoadedClearNavigation;
                ViewerZoneSync.CurrentChanged += OnZoneTableChanged;
                _isSubbed = true;
            }

            EditorUtils.CompositeType type = content.EditorUtils.GetCompositeType(composite);
            
            //The viewer's scene is the composite the path starts from
            if (_levelViewerPanel != null)
                _levelViewerPanel.SetIsRootComposite((path?.EntryComposite == null ? type : content.EditorUtils.GetCompositeType(path.EntryComposite)) == EditorUtils.CompositeType.IS_ROOT);

            switch (type)
            {
                case EditorUtils.CompositeType.IS_ROOT:
                    EditorIcons.BindIcon(this, EditorIcon.RootComposite);
                    break;
                case EditorUtils.CompositeType.IS_GLOBAL:
                case EditorUtils.CompositeType.IS_PAUSE_MENU:
                    EditorIcons.BindIcon(this, EditorIcon.SystemComposite);
                    break;
                case EditorUtils.CompositeType.IS_DISPLAY_MODEL:
                    EditorIcons.BindIcon(this, EditorIcon.DisplayModel);
                    break;
                case EditorUtils.CompositeType.IS_GENERIC_COMPOSITE:
                    EditorIcons.BindIcon(this, EditorIcon.Composite);
                    break;
            }

            _entityList.List.Setup(composite, new CompositeEntityList.DisplayOptions() { ShowCheckboxes = false }, false);
            _path.Reset();
            if (path != null)
                _path.Restore(path);
            this.Text = EditorUtils.GetCompositeName(composite);

            Reload(composite);
            Singleton.OnCompositeSelected?.Invoke(_composite);
        }

        internal void AttachCompositeBrowser(CompositeBrowser compositeBrowser)
        {
            if (compositeBrowser == null || ReferenceEquals(_compositeBrowser, compositeBrowser))
                return;

            _compositeBrowser = compositeBrowser;
        }

        /* Call this to hide the CompositeDisplay */
        public void DepopulateUI()
        {
            SaveAllFlowgraphs();

            this.Hide();
            CompositeDisplay_FormClosed(null, null);
            ForgetLiveLinkActivityPlace();
        }

        private void CompositeDisplay_FormClosed(object sender, FormClosedEventArgs e)
        {
            SaveInnerDockLayout();
            _entityList.List.SelectedEntityChanged -= OnEntityListSelectionChanged;
            _entityList.List.SelectedEntitiesChanged -= OnEntityListMultiSelectionChanged;
            //this.FormClosed -= CompositeDisplay_FormClosed;
            Singleton.OnCompositeRenamed -= OnCompositeRenamed;
            Singleton.OnCompositeDeleted -= OnCompsoiteDeleted;
            Singleton.OnCompositeAdded -= OnCompositeAddedRestyleProxies;
            Singleton.OnEntityAdded -= ReloadUIForNewEntity;
            Singleton.OnEntityDeleted -= ReloadUIForDeletedEntity;
            //Left subscribed, the static event kept every closed display alive - its inspector, and the level content that showed
            Singleton.OnLevelLoaded -= OnLevelLoadedClearNavigation;
            ViewerZoneSync.CurrentChanged -= OnZoneTableChanged;
            _isSubbed = false;

            if (dialog_var != null)
                dialog_var.Close();
            if (dialog_func != null)
                dialog_func.Close();
            if (dialog_compinst != null)
                dialog_compinst.Close();
            if (dialog_hierarchy != null)
                dialog_hierarchy.Close();

            _entityList.List.ClearSelection();

            foreach (Flowgraph page in _flowgraphs.ToArray())
                page?.Close();
            _flowgraphs.Clear();

            if (_renameComposite != null)
                _renameComposite.FormClosed -= _renameComposite_FormClosed;
            if (_createFlowgraphPopup != null)
                _createFlowgraphPopup.FormClosed -= _createFlowgraphPopup_FormClosed;
            if (_instanceInfoPopup != null)
                _instanceInfoPopup.FormClosed -= _instanceInfoPopup_FormClosed;

            _composite = null;
            UnityConnection.Send.NoteEditorSceneRoot(0);

            if (_entityDisplay != null)
                _entityDisplay.DepopulateUI();

            CloseAllChildTabs();


            vS2015DarkTheme1.Dispose();
            vS2015BlueTheme1.Dispose();
        }

        private void Reload(Composite composite)
        {
            Debug.Log("Composite Display", "Private Reload called for " + composite.shortGUID.ToByteString() + " (" + composite.name + ")");
            Cursor.Current = Cursors.WaitCursor;

            LevelContent content = Content;
            Composite[] entryPoints = content?.Level?.Commands?.EntryPoints;
            if (content?.Level?.Commands == null || entryPoints == null || entryPoints.Length == 0)
            {
                Cursor.Current = Cursors.Default;
                return;
            }

            if (_composite != null)
                SaveAllFlowgraphs();

            //No need to find uses of entry point - it's the entry point
            findUses.Visible = entryPoints[0] != composite;

            //Shouldn't be able to delete the root/PAUSEMENU/GLOBAL else it'll break stuff
            deleteComposite.Visible = !entryPoints.Contains(composite);
            //Similarly, shouldn't be able to rename PAUSEMENU/GLOBAL as their names are used in code
            renameComposite.Visible =
                (entryPoints.Length <= 1 || entryPoints[1] != composite) &&
                (entryPoints.Length <= 2 || entryPoints[2] != composite);

            _composite = composite;
            this.Text = EditorUtils.GetCompositeName(composite);
            _entityList?.UpdateTitle();
            UpdatePathBreadcrumb();

            //Remove dead links and empty aliases on first time
            if (content.Level.Commands.Utils != null &&
                !content.Level.Commands.Utils.PurgedComposites.purged.Contains(_composite.shortGUID))
            {
                //Clear out any dead links
                content.Level.Commands.Utils.PurgeDeadLinks(_composite);
                content.Level.Commands.Utils.PurgedComposites.purged.Add(_composite.shortGUID);
            }

            ClearEntitySelection();
            CloseAllChildTabs();
            Reload(false);
            if (!ViewerSelectionSync.IsApplyingViewerSelection)
            {
                //Not while the viewer is busy: activating puts focus back in its window, and waited for it (see FocusMoveWouldWait).
                //The tab still comes to the front where that moves no focus - a pane that does not have it does not take it.
                if (!UnityConnection.ViewerBusy.FocusMoveWouldWait)
                    this.Activate();
                else if (Pane != null && Pane.ActiveContent != this && !Pane.IsActivated && Pane.DisplayingContents.Contains(this))
                    Pane.ActiveContent = this;
            }

            _instanceInfoPopup?.Close();

            //Both _composite and the drill path have settled by now. LoadChild/LoadParent mutate the
            //path before calling in here, so the place we just left is the snapshot from last time
            //rather than anything readable off _path at the top of this method.
            CompositeNavigationHistory.Entry arrivedAt = CompositeNavigationHistory.CreateEntry(_composite, _path);
            if (!_suppressNavigationHistory && _currentPlace != null && !_currentPlace.SamePlaceAs(arrivedAt))
                CompositeNavigationHistory.Record(_currentPlace);
            _currentPlace = arrivedAt;
            RefreshNavigateBackState();
            //...and so has the scene a viewer connecting now is to show (read on the socket's thread, never half way through a step)
            UnityConnection.Send.NoteEditorSceneRoot((_path?.AllComposites?.FirstOrDefault() ?? _composite)?.shortGUID.AsUInt32 ?? 0);

            Cursor.Current = Cursors.Default;
        }

        /// <summary>
        /// Replays a viewer drill-down path from an entry composite without re-running PopulateUI on the root
        /// (which would reset navigation when the display is already inside nested composites).
        /// </summary>
        public bool ApplyViewerSelectionPath(
            Composite entryComposite,
            IReadOnlyList<uint> pathEntityGuids,
            bool selectLeafEntity,
            Func<Entity, Composite> getChildComposite)
        {
            if (entryComposite == null || pathEntityGuids == null || getChildComposite == null)
                return false;

            if (pathEntityGuids.Count == 0)
            {
                if (selectLeafEntity)
                    return false;

                if (_composite?.shortGUID == entryComposite.shortGUID && _path.AllEntities.Count == 0)
                {
                    ClearEntitySelection();
                    return true;
                }

                _path.Reset();
                Reload(entryComposite);
                ClearEntitySelection();
                return true;
            }

            if (!selectLeafEntity)
            {
                int drillEntityCount = pathEntityGuids.Count;
                if (ViewerPathMatchesCurrent(entryComposite, pathEntityGuids, drillEntityCount, getChildComposite))
                {
                    ClearEntitySelection();
                    return true;
                }
            }

            if (selectLeafEntity)
            {
                int drillEntityCount = pathEntityGuids.Count - 1;
                if (ViewerPathMatchesCurrent(entryComposite, pathEntityGuids, drillEntityCount, getChildComposite))
                {
                    Entity entity = _composite.GetEntityByID(new ShortGuid(pathEntityGuids[pathEntityGuids.Count - 1]));
                    if (entity != null)
                    {
                        LoadEntity(entity, true);
                        return true;
                    }

                    // Drill navigation already matches; leaf may not exist yet (e.g. ENTITY_SELECTED
                    // arrived before ENTITY_ADDED). Do not reset navigation by falling through.
                    return false;
                }
            }
            else if (pathEntityGuids.Count > 0)
            {
                int parentDrillCount = pathEntityGuids.Count - 1;
                if (ViewerPathMatchesCurrent(entryComposite, pathEntityGuids, parentDrillCount, getChildComposite))
                {
                    Entity drillEntity = _composite.GetEntityByID(new ShortGuid(pathEntityGuids[pathEntityGuids.Count - 1]));
                    Composite childComposite = drillEntity != null ? getChildComposite(drillEntity) : null;
                    if (childComposite != null)
                    {
                        LoadChild(childComposite, drillEntity);
                        return true;
                    }
                }
            }

            return TryReplayViewerPathFromEntry(
                entryComposite,
                pathEntityGuids,
                selectLeafEntity,
                getChildComposite);
        }

        /// <summary>
        /// Walks a drill path from the currently displayed composite without resetting existing navigation,
        /// updating <see cref="CompositePath"/> only and reloading once at the destination.
        /// </summary>
        public bool NavigateToPathFromCurrentComposite(
            IReadOnlyList<uint> pathEntityGuids,
            int drillStepCount,
            int selectEntityIndex)
        {
            if (_composite == null || pathEntityGuids == null)
                return false;

            if (drillStepCount < 0 || selectEntityIndex < 0 || selectEntityIndex >= pathEntityGuids.Count)
                return false;

            //Walked on the side: a hop that no longer resolves leaves the path as it was, not with the hops before it added
            //under a composite that never changed (the breadcrumb, Back and the viewer's packets then disagreed)
            List<Composite> hopComposites = new List<Composite>();
            List<Entity> hopEntities = new List<Entity>();
            Composite current = _composite;
            for (int i = 0; i < drillStepCount; i++)
            {
                Entity entity = current.GetEntityByID(new ShortGuid(pathEntityGuids[i]));
                if (entity == null)
                    return false;

                if (entity.variant != EntityVariant.FUNCTION)
                    return false;

                FunctionEntity function = (FunctionEntity)entity;
                if (function.function.IsFunctionType)
                    return false;

                Composite childComposite = Content.Level.Commands.GetComposite(function.function);
                if (childComposite == null)
                    return false;

                hopComposites.Add(current);
                hopEntities.Add(entity);
                current = childComposite;
            }

            for (int i = 0; i < hopEntities.Count; i++)
                _path.StepForwards(hopComposites[i], hopEntities[i]);
            Reload(current);

            Entity selected = current.GetEntityByID(new ShortGuid(pathEntityGuids[selectEntityIndex]));
            if (selected == null)
                return false;

            LoadEntity(selected, true);
            return true;
        }

        /// <summary>
        /// Walks a viewer drill path updating <see cref="CompositePath"/> only, then reloads once at the destination.
        /// </summary>
        private bool TryReplayViewerPathFromEntry(
            Composite entryComposite,
            IReadOnlyList<uint> pathEntityGuids,
            bool selectLeafEntity,
            Func<Entity, Composite> getChildComposite)
        {
            /* Walked on the side first, the path reset and rebuilt only once every hop resolves: reset up front, a hop that
               no longer resolves (deleted since - Back to such a place) left an empty or part-built path under the composite
               still on screen, and the breadcrumb, Back, the viewer's packets and its grey-out all went by different places */
            List<Composite> hopComposites = new List<Composite>();
            List<Entity> hopEntities = new List<Entity>();
            Composite current = entryComposite;
            int drillStepCount = selectLeafEntity ? pathEntityGuids.Count - 1 : pathEntityGuids.Count;

            for (int i = 0; i < drillStepCount; i++)
            {
                Entity entity = current.GetEntityByID(new ShortGuid(pathEntityGuids[i]));
                if (entity == null)
                {
                    if (selectLeafEntity)
                        return TrySelectLeafEntityInCurrentComposite(pathEntityGuids);
                    return false;
                }

                Composite childComposite = getChildComposite(entity);
                if (childComposite == null)
                    return false;

                hopComposites.Add(current);
                hopEntities.Add(entity);
                current = childComposite;
            }

            _path.Reset();
            for (int i = 0; i < hopEntities.Count; i++)
                _path.StepForwards(hopComposites[i], hopEntities[i]);
            Reload(current);

            if (selectLeafEntity)
            {
                Entity leaf = current.GetEntityByID(new ShortGuid(pathEntityGuids[pathEntityGuids.Count - 1]));
                if (leaf == null)
                    return TrySelectLeafEntityInCurrentComposite(pathEntityGuids);

                LoadEntity(leaf, true);
            }
            else
            {
                ClearEntitySelection();
            }

            return true;
        }

        public bool TrySelectLeafEntityInCurrentComposite(IReadOnlyList<uint> pathEntityGuids)
        {
            if (pathEntityGuids == null || pathEntityGuids.Count == 0 || _composite == null)
                return false;

            Entity leaf = _composite.GetEntityByID(new ShortGuid(pathEntityGuids[pathEntityGuids.Count - 1]));
            if (leaf == null)
                return false;

            LoadEntity(leaf, true);
            return true;
        }

        /// <summary>
        /// Select an alias that was just added while the display is already showing its owner composite.
        /// </summary>
        public bool TrySelectAddedAlias(Composite ownerComposite, Entity entity)
        {
            if (!Populated || ownerComposite == null || entity == null || _composite == null)
                return false;

            if (_composite.shortGUID != ownerComposite.shortGUID)
                return false;

            if (_composite.GetEntityByID(entity.shortGUID) == null)
                return false;

            if (_entityList?.List != null && !_entityList.List.ContainsEntity(entity.shortGUID))
                _entityList.List.AddNewEntity(entity);

            LoadEntity(entity, true);
            return true;
        }

        private bool ViewerPathMatchesCurrent(
            Composite entryComposite,
            IReadOnlyList<uint> pathEntityGuids,
            int drillEntityCount,
            Func<Entity, Composite> getChildComposite)
        {
            if (_composite == null || entryComposite == null || pathEntityGuids == null || drillEntityCount < 0)
                return false;

            Composite expected = entryComposite;
            for (int i = 0; i < drillEntityCount; i++)
            {
                if (i >= pathEntityGuids.Count)
                    return false;

                Entity entity = expected.GetEntityByID(new ShortGuid(pathEntityGuids[i]));
                if (entity == null)
                    return false;

                Composite childComposite = getChildComposite(entity);
                if (childComposite == null)
                    return false;

                expected = childComposite;
            }

            if (expected.shortGUID != _composite.shortGUID)
                return false;

            if (_path.AllEntities.Count != drillEntityCount)
                return false;

            for (int i = 0; i < drillEntityCount; i++)
            {
                if (_path.AllEntities[i].shortGUID.AsUInt32 != pathEntityGuids[i])
                    return false;
            }

            return true;
        }

        /* Load a child composite within this composite */
        public void LoadChild(Composite composite, Entity entity)
        {
            _path.StepForwards(_composite, entity);
            Reload(composite);
        }

        public static bool IsCompositeInstance(Entity entity, Commands commands)
        {
            if (entity?.variant != EntityVariant.FUNCTION || commands == null)
                return false;

            FunctionEntity functionEntity = (FunctionEntity)entity;
            return !functionEntity.function.IsFunctionType && commands.GetComposite(functionEntity.function) != null;
        }

        /// <summary>
        /// Follows a proxy path from its entry composite, stepping through composite instances
        /// and selecting the target entity. Reuses the current display path when already aligned.
        /// </summary>
        public bool NavigateToProxyPath(ProxyEntity proxy)
        {
            if (proxy == null || !Populated)
                return false;

            Commands commands = Content.Level.Commands;
            List<Tuple<Composite, Entity>> resolved = commands.Utils.ResolveProxy(proxy);
            if (!commands.Utils.CouldResolve(resolved))
                return false;

            /* The proxy's path starts at the level's root. When it runs through the root of the scene on screen, it is walked from
               there and the scene is kept; otherwise its entry becomes the scene's root first (OpenAsSceneRoot), so the viewer
               builds that scene rather than judge the one it has by a path from another root. */
            Composite sceneRoot = _path.AllComposites.FirstOrDefault() ?? _composite;
            int from = 0;
            for (int i = 1; i < resolved.Count; i++)
            {
                if (resolved[i].Item1 == sceneRoot)
                {
                    from = i;
                    break;
                }
            }
            Composite entryComposite = resolved[from].Item1;
            uint[] pathEntityGuids = new uint[resolved.Count - from];
            for (int i = from; i < resolved.Count; i++)
                pathEntityGuids[i - from] = resolved[i].Item2.shortGUID.AsUInt32;

            return NavigateAsOneStep(() =>
            {
                OpenAsSceneRoot(entryComposite);
                return ApplyViewerSelectionPath(
                    entryComposite,
                    pathEntityGuids,
                    selectLeafEntity: true,
                    entity => IsCompositeInstance(entity, commands)
                        ? commands.GetComposite(((FunctionEntity)entity).function)
                        : null);
            });
        }

        public void StepIntoEntity(Entity entity)
        {
            if (entity == null || !Populated)
                return;

            switch (entity.variant)
            {
                case EntityVariant.PROXY:
                    NavigateToProxyPath((ProxyEntity)entity);
                    break;
                case EntityVariant.FUNCTION:
                    FunctionEntity functionEntity = (FunctionEntity)entity;
                    Composite childComposite = Content.Level.Commands.GetComposite(functionEntity.function);
                    if (childComposite == null || functionEntity.function.IsFunctionType)
                        return;
                    LoadChild(childComposite, entity);
                    break;
                case EntityVariant.ALIAS:
                    ShortGuid[] aliasPath = ((AliasEntity)entity).alias.path;
                    if (aliasPath == null || aliasPath.Length < 2)
                        return;

                    uint[] pathGuids = new uint[aliasPath.Length];
                    for (int i = 0; i < aliasPath.Length; i++)
                        pathGuids[i] = aliasPath[i].AsUInt32;

                    NavigateToPathFromCurrentComposite(
                        pathGuids,
                        aliasPath.Length - 2,
                        aliasPath.Length - 2);
                    break;
            }
        }

        public void StepIntoCompositeInstance(Entity entity)
        {
            if (!IsCompositeInstance(entity, Content.Level.Commands))
                return;

            LoadChild(Content.Level.Commands.GetComposite(((FunctionEntity)entity).function), entity);
        }

        /* Load the parent composite, one back from this composite. Reload clears the selection once it has the
           composite it goes to: cleared here, between the path stepping back and the composite changing, the
           deselect told the viewer of the shorter path inside the composite being left - a place that does not
           exist, which it navigated to (a full focus pass) until the next packet put it right. */
        public void LoadParent()
        {
            if (_path.StepBackwards(out Composite composite, out Entity entity))
            {
                Reload(composite);
                SelectEntityAfterNavigationReload(entity, deferFlowgraphFocus: true);
            }
        }

        /* Jump to a composite segment in the breadcrumb path (the selection is cleared by Reload, as for LoadParent). */
        public void LoadPathSegment(int segmentIndex)
        {
            if (!_path.TryNavigateToCompositeIndex(_composite, segmentIndex, out Composite composite, out Entity entity))
                return;

            Reload(composite);
            SelectEntityAfterNavigationReload(entity, deferFlowgraphFocus: true);
        }

        private void SelectEntityAfterNavigationReload(Entity entity, bool deferFlowgraphFocus = false)
        {
            if (entity == null)
            {
                ClearEntitySelection();
                return;
            }

            if (!deferFlowgraphFocus)
            {
                LoadEntity(entity, true);
                return;
            }

            // List/inspector selection must be applied synchronously so stale entities cannot be
            // stepped into via Go while flowgraph pages are still being recreated.
            LoadEntity(entity, focusNode: false);

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || !Populated)
                    return;

                FocusEntityOnFlowgraph(entity);
            }));
        }

        /* Back up the path to one of its composites for an undo or redo whose change belongs there: as LoadPathSegment,
           but the instance stepped out of is not selected. The edit selects what it changed next, and the flowgraph focus
           that LoadPathSegment queues for the instance came after it, panning the page back to the instance's node and
           highlighting it beside the inspector showing the edited entity. */
        public bool StepUpToPathSegment(int segmentIndex)
        {
            if (!_path.TryNavigateToCompositeIndex(_composite, segmentIndex, out Composite composite, out _))
                return false;

            Reload(composite);
            return true;
        }

        private void UpdatePathBreadcrumb()
        {
            if (!Populated)
                return;

            pathBreadcrumb.SetPath(_path, _composite);
        }

        public void RefreshPathBreadcrumb()
        {
            UpdatePathBreadcrumb();
        }

        /// <summary>The composites the user drilled through to reach the one on screen.</summary>
        public CompositePath.Snapshot CaptureNavigationPath()
        {
            return _path.Capture();
        }

        /// <summary>What is selected: the entities of a multi-selection, the one entity, or none.</summary>
        public List<Entity> CaptureSelection()
        {
            List<Entity> selected = _entityDisplay?.MultiSelectedEntities;
            if (selected == null && _entityDisplay?.Entity != null)
                selected = new List<Entity>() { _entityDisplay.Entity };
            return selected == null ? new List<Entity>() : new List<Entity>(selected);
        }

        /// <summary>
        /// Select again what was selected before the display was closed and opened again (<see cref="CaptureSelection"/>):
        /// as much of it as the composite on screen still holds.
        /// </summary>
        public void RestoreSelection(IEnumerable<Entity> selected)
        {
            if (_composite == null || selected == null)
                return;

            List<Entity> entities = selected.Where(o => o != null && _composite.GetEntityByID(o.shortGUID) == o).ToList();
            if (entities.Count == 1)
                LoadEntity(entities[0], false);
            else if (entities.Count > 1)
                ApplyMultiSelection(entities);
        }

        /// <summary>
        /// Where the display stands - the composite, the path stepped down to it, the selection - by id, for
        /// <see cref="CompositeImporter.ReopenClosedPlace"/> to put back once an import that closes the display is done.
        /// Null when nothing is open.
        /// </summary>
        public CompositePath.Place CapturePlace()
        {
            if (_composite == null)
                return null;

            return _path.CapturePlace(_composite, CaptureSelection());
        }

        /* Reload this display */
        public void Reload(bool alsoReloadEntities = true)
        {
            if (_composite == null)
            {
                Singleton.Editor.LoadComposite(Content.Level.Commands.EntryPoints[0], true);
                return;
            }

            Debug.Log("Composite Display", "Public Reload called for " + _composite.shortGUID.ToByteString() + " (" + _composite.name + ")");

            //Figure out if the composite supports flowgraphs: it won't if there's no layout defined, or if the composite has diverged from vanilla
            if (!FlowgraphLayoutManager.HasCompatibilityInfo(_composite))
                FlowgraphLayoutManager.EvaluateCompatibility(_composite);

            _entityList.List.LoadComposite(Composite);
            if (alsoReloadEntities) ReloadAllEntities();

            dockPanel.SuspendLayout(true);
            try
            {
                /* Not into the pages when the focus is in the viewer's window (a composite opened from a pick there): showing
                   a page activates it, which took the focus out of the viewport and waited on the viewer's thread to do so -
                   frozen behind a busy viewer. */
                bool pagesTakeFocus = _levelViewerPanel == null || !_levelViewerPanel.ContainsFocus;
                //And WinForms is told where the focus is first, or closing the old pages (and their emptied pane) moved it
                if (!pagesTakeFocus)
                    _levelViewerPanel.AdoptViewerFocus();

                foreach (Flowgraph page in _flowgraphs.ToArray())
                    page?.Close();
                _flowgraphs.Clear();

                //If we support flowgraphs, load them
                Debug.Log("Composite Display", "Flowgraphs " + (SupportsFlowgraphs ? "Supported!" : "Not supported!"));
                if (SupportsFlowgraphs)
                {
                    List<FlowgraphMeta> layouts = FlowgraphLayoutManager.GetLayouts(Composite);
                    Debug.Log("Composite Display", "Found " + layouts.Count + " flowgraph layout(s)");
                    for (int i = 0; i < layouts.Count; i++)
                        CreateFlowgraphWindow(layouts[i], pagesTakeFocus);

                    string prevLoaded = FlowgraphLayoutManager.GetSelectedPage(Composite);
                    Flowgraph selected = prevLoaded == null ? null : _flowgraphs.FirstOrDefault(o => o.FlowgraphName == prevLoaded);
                    if (selected != null)
                        selected.DockHandler.Show(dockPanel, DockState.Document, pagesTakeFocus);
                }
            }
            finally
            {
                dockPanel.ResumeLayout(true, true);
            }

            RebuildProxiedEntityCache();
            RefreshNodeMarkers();

            createFlowgraph.Visible = SupportsFlowgraphs;

            Singleton.OnCompositeReloaded?.Invoke(_composite);
        }

        /* Reload all entities loaded in this display */
        public void ReloadAllEntities()
        {
            if (_entityDisplay.Populated)
                _entityDisplay.Reload();
        }

        /* Reload a specific entity's UI (if it is loaded) */
        public void ReloadEntity(Entity entity)
        {
            if (_entityDisplay != null && _entityDisplay.Entity == entity)
                _entityDisplay.Reload();
        }

        /* Perform a partial UI reload for a newly added entity */
        private void ReloadUIForNewEntity(Entity newEnt)
        {
            if (newEnt == null || !Populated || Composite == null)
                return;

            if (Composite.GetEntityByID(newEnt.shortGUID) == null)
                return;

            //A new proxy may point at an entity shown on our flowgraphs
            if (newEnt.variant == EntityVariant.PROXY)
            {
                RebuildProxiedEntityCache();
                RefreshNodeMarkers();
            }

            _entityList.List.AddNewEntity(newEnt);

            //Viewer deep-select swaps add+select atomically; selection handler populates the inspector.
            if (ViewerSelectionSync.SuppressSyncBroadcastDepth > 0)
                return;

            LoadEntity(newEnt, false);
        }

        private void ReloadUIForDeletedEntity(Entity deletedEntity)
        {
            if (deletedEntity == null || !Populated)
                return;

            if (Composite.GetEntityByID(deletedEntity.shortGUID) != null)
                return;

            //A deleted proxy may have pointed at an entity shown on our flowgraphs
            if (deletedEntity.variant == EntityVariant.PROXY)
            {
                RebuildProxiedEntityCache();
                RefreshNodeMarkers();
            }

            /* The inspector may be showing this entity on its own, or as one of a multi-selection -
               either way what it is showing has just gone, and a grid over deleted entities is no use
               to anyone. */
            bool inspectorShowsIt = _entityDisplay?.Entity == deletedEntity
                || _entityDisplay?.MultiSelectedEntities?.Contains(deletedEntity) == true;
            if (inspectorShowsIt && _entityDisplay.Populated
                && ViewerSelectionSync.SuppressSyncBroadcastDepth == 0)
            {
                //A clear like any other, so the viewer hears that nothing is selected now
                _entityDisplay.ClearSelectedEntity();
            }

            RemoveEntityFromList(deletedEntity);
        }

        public bool RemoveEntityFromList(Entity entity)
        {
            if (entity == null || _entityList?.List == null)
                return false;

            return _entityList.List.RemoveEntity(entity);
        }

        public bool RemoveEntityFromList(ShortGuid entityId)
        {
            if (_entityList?.List == null)
                return false;

            return _entityList.List.RemoveEntity(entityId);
        }

        public void ReloadEntityListFromComposite()
        {
            if (!Populated || _entityList?.List == null)
                return;

            _entityList.List.LoadComposite(Composite);
            ReloadAllEntities();
        }

        /* Load an entity into the composite tabs UI */
        public void ClearEntitySelection()
        {
            _loadEntityGeneration++;

            if (_entityList?.List != null)
            {
                _entityList.List.SelectedEntityChanged -= OnEntityListSelectionChanged;
                _entityList.List.SelectedEntitiesChanged -= OnEntityListMultiSelectionChanged;
                _entityList.List.ClearSelection();
                _entityList.List.SelectedEntityChanged += OnEntityListSelectionChanged;
                _entityList.List.SelectedEntitiesChanged += OnEntityListMultiSelectionChanged;
            }

            _entityDisplay?.ClearSelectedEntity();
        }

        public void LoadEntityDontFocusNode(ShortGuid guid) => LoadEntity(guid, false);
        public void LoadEntityAndFocusNode(ShortGuid guid) => LoadEntity(guid, true);
        public void LoadEntity(ShortGuid guid, bool focusNode)
        {
            LoadEntity(Composite.GetEntityByID(guid), focusNode);
        }
        private void OnEntityListSelectionChanged(Entity entity)
        {
            if (entity == null)
            {
                _entityDisplay?.ClearSelectedEntity();
                return;
            }

            LoadEntity(entity, focusNode: false);
        }
        private void OnEntityListMultiSelectionChanged(List<Entity> entities)
        {
            LoadEntities(entities);
        }

        /* Several entities selected together in the viewport: show that selection here, in the list
           and in the inspector. The first entity leads, as it does everywhere else. */
        public void ApplyViewerMultiSelection(List<Entity> entities) => ApplyMultiSelection(entities);

        /// <summary>
        /// Show these entities as the selection: the list, the inspector, and - through the inspector's
        /// reload - the viewport. Used wherever something other than a click settles what is selected,
        /// such as a paste, which selects everything it just made.
        /// </summary>
        public void ApplyMultiSelection(List<Entity> entities)
        {
            if (entities == null || entities.Count < 2 || IsDisposed || Disposing)
                return;

            if (_entityList?.List != null)
            {
                _entityList.List.SelectedEntityChanged -= OnEntityListSelectionChanged;
                _entityList.List.SelectedEntitiesChanged -= OnEntityListMultiSelectionChanged;
                _entityList.List.SelectEntities(entities);
                _entityList.List.SelectedEntityChanged += OnEntityListSelectionChanged;
                _entityList.List.SelectedEntitiesChanged += OnEntityListMultiSelectionChanged;
            }

            LoadEntities(entities);
        }

        /* Load multiple entities into the inspector for multi-editing (e.g. from a flowgraph multi-selection) */
        public void LoadEntities(List<Entity> entities)
        {
            if (entities == null || entities.Count == 0) return;
            if (entities.Count == 1)
            {
                LoadEntity(entities[0], false);
                return;
            }
            if (_entityDisplay == null || _entityDisplay.IsDisposed)
                return;
            if (IsDisposed || Disposing || Composite == null)
                return;

            //Clear the entity list selection so it doesn't disagree with the multi-selection - unless the
            //list itself is the source (its selection already matches the entities being loaded)
            if (_entityList?.List != null && _entityList.List.SelectedEntity != null)
            {
                List<Entity> listSelection = _entityList.List.SelectedEntities;
                bool listIsSource = listSelection.Count == entities.Count
                    && entities.All(o => listSelection.FirstOrDefault(l => l.shortGUID == o.shortGUID) != null);
                if (!listIsSource)
                {
                    _entityList.List.SelectedEntityChanged -= OnEntityListSelectionChanged;
                    _entityList.List.SelectedEntitiesChanged -= OnEntityListMultiSelectionChanged;
                    _entityList.List.ClearSelection();
                    _entityList.List.SelectedEntityChanged += OnEntityListSelectionChanged;
                    _entityList.List.SelectedEntitiesChanged += OnEntityListMultiSelectionChanged;
                }
            }

            int generation = ++_loadEntityGeneration;
            List<Entity> entitiesToLoad = new List<Entity>(entities);
            bool viewerOriginated = ViewerSelectionSync.IsApplyingViewerSelection;
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || Disposing || generation != _loadEntityGeneration)
                    return;
                if (_entityDisplay == null || _entityDisplay.IsDisposed)
                    return;

                //Same as the single load: anything deleted while this waited its turn is not shown
                entitiesToLoad.RemoveAll(o => Composite?.GetEntityByID(o.shortGUID) == null);
                if (entitiesToLoad.Count == 0)
                    return;

                if (viewerOriginated)
                    ViewerSelectionSync.RunAsViewerOriginated(() => _entityDisplay.PopulateUI(entitiesToLoad, false));
                else
                    _entityDisplay.PopulateUI(entitiesToLoad, false);
            }));
        }

        public void LoadEntityDontFocusNode(Entity entity) => LoadEntity(entity, false);
        public void LoadEntityAndFocusNode(Entity entity) => LoadEntity(entity, true);
        public void LoadEntity(Entity entity, bool focusNode)
        {
            if (entity == null) return;
            if (_entityDisplay == null || _entityDisplay.IsDisposed)
                return;
            if (IsDisposed || Disposing || Composite == null)
                return;

            // Start canvas focus before the inspector rebuild so selection stays interactive
            // and a later click can retarget the in-progress lerp.
            if (SupportsFlowgraphs && focusNode && !ViewerSelectionSync.IsApplyingViewerSelection)
                FocusEntityOnFlowgraph(entity);

            /* Make sure the entity is selected in the list view too, but don't handle the event, else
               we'll get called again. The row count matters as well as which row: coming back to one
               entity from a multi-selection has to clear the rows that are no longer selected, and
               SelectedEntity alone can still name this one while others sit selected above it. */
            if (_entityList?.List != null &&
                (_entityList.List.SelectedEntity == null
                    || _entityList.List.SelectedEntity.shortGUID != entity.shortGUID
                    || _entityList.List.SelectedEntities.Count > 1))
            {
                _entityList.List.SelectedEntityChanged -= OnEntityListSelectionChanged;
                _entityList.List.SelectEntity(entity);
                _entityList.List.SelectedEntityChanged += OnEntityListSelectionChanged;
            }

            int generation = ++_loadEntityGeneration;
            Entity entityToLoad = entity;
            bool displayLinks = !SupportsFlowgraphs;
            //The inspector populate is deferred, so the viewer-originated flag must be captured now
            //and re-entered inside the deferred action - otherwise the inspector's Activate() runs
            //after the flag has been released and steals Win32 focus from the embedded viewer.
            bool viewerOriginated = ViewerSelectionSync.IsApplyingViewerSelection;
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || Disposing || generation != _loadEntityGeneration)
                    return;
                if (_entityDisplay == null || _entityDisplay.IsDisposed)
                    return;
                /* Deleted while this populate waited its turn. Deleting several entities selects the
                   next row in the list as each one goes, so the load for an entity that is about to be
                   deleted lands after it has gone - and the inspector sat there showing a grid over a
                   deleted entity. */
                if (Composite?.GetEntityByID(entityToLoad.shortGUID) == null)
                    return;

                if (viewerOriginated)
                    ViewerSelectionSync.RunAsViewerOriginated(() => _entityDisplay.PopulateUI(entityToLoad, displayLinks));
                else
                    _entityDisplay.PopulateUI(entityToLoad, displayLinks);
            }));
        }
        public void CloseAllChildTabsExcept(Entity entity)
        {
            if (_entityDisplay == null || _entityDisplay.Entity == entity)
                return;

            //Note: we don't actually close tabs here, we just hide them - they can be repurposed then instead of spawning new ones
            _entityDisplay.DepopulateUI();
        }
        public void CloseAllChildTabs()
        {
            CloseAllChildTabsExcept(null);
        }

        private void findUses_Click(object sender, EventArgs e)
        {
            Singleton.Editor?.EntitySearch?.SearchForComposite(Composite);
        }

        public System.Drawing.Image FindReferencesIcon => findUses.Image;

        /// <summary>Collect the pin IDs of the entity's input pins that have live connections on any flowgraph page.</summary>
        public void CollectConnectedInputPins(Entity entity, HashSet<ShortGuid> results)
        {
            foreach (Flowgraph flowgraph in _flowgraphs)
                flowgraph?.CollectConnectedInputPins(entity, results);
        }

        public bool AnyFlowgraphsContainEntity(Entity entity)
        {
            foreach (Flowgraph flowgraph in _flowgraphs)
            {
                if (FlowgraphContainsEntity(flowgraph, entity))
                    return true;
            }
            return false;
        }

        /* Every entity in the level named by a proxy or an alias path, anywhere. Built once for a
           whole delete rather than per entity: a pointer names its target by id, so an id appearing
           in one of those paths is something still reaching for that entity. */
        public HashSet<ShortGuid> CollectPointerTargets()
        {
            HashSet<ShortGuid> targets = new HashSet<ShortGuid>();
            if (Content?.Level?.Commands == null)
                return targets;

            foreach (Composite composite in Content.Level.Commands.Entries)
            {
                foreach (ProxyEntity proxy in composite.proxies)
                    AddPath(targets, proxy.proxy);
                foreach (AliasEntity alias in composite.aliases)
                    AddPath(targets, alias.alias);
            }

            return targets;
        }

        private static void AddPath(HashSet<ShortGuid> targets, EntityPath path)
        {
            if (path?.path == null)
                return;

            foreach (ShortGuid step in path.path)
                targets.Add(step);
        }

        /// <summary>
        /// Entities that are in the world in their own right: the types the viewport's Create menu
        /// places, which are positioned in the level as well as wired into the scripting. Removing a
        /// script node for one of these leaves the entity where it is - only deleting the entity
        /// outright (from the viewport, the entity list, or Delete Entity on a node) takes it away.
        /// </summary>
        public static bool IsInSceneEntity(Entity entity)
        {
            return entity is FunctionEntity function
                && function.function.IsFunctionType
                && RenderFilterDefinitions.IsSupported(function.function.AsFunctionType);
        }

        /// <summary>
        /// Is anything still reaching for this entity? Its nodes on any open page, a trigger sequence
        /// or CAGEAnimation naming it, or a proxy or alias somewhere in the level. Pass
        /// <paramref name="pointerTargets"/> from <see cref="CollectPointerTargets"/> when checking
        /// several entities at once.
        /// </summary>
        /// <remarks>
        /// Links between entities deliberately don't count. They are the flowgraph's own wiring - all
        /// but the loneliest entity has some - and deleting the entity takes them with it (and undo
        /// puts them back). What counts is a use the flowgraph can't see: an animation or trigger
        /// sequence driving it, or a pointer to it from another composite.
        /// </remarks>
        public bool IsEntityStillReferenced(Entity entity, HashSet<ShortGuid> pointerTargets = null)
        {
            if (entity == null || Composite == null)
                return false;

            if (AnyFlowgraphsContainEntity(entity))
                return true;

            if ((pointerTargets ?? CollectPointerTargets()).Contains(entity.shortGUID))
                return true;

            foreach (Entity other in Composite.GetEntities())
            {
                if (other.shortGUID == entity.shortGUID)
                    continue;

                if (other is TriggerSequence triggerSequence)
                {
                    foreach (TriggerSequence.SequenceEntry entry in triggerSequence.sequence)
                        if (PathNames(entry.connectedEntity, entity.shortGUID))
                            return true;
                }
                else if (other is CAGEAnimation animation)
                {
                    foreach (CAGEAnimation.Connection connection in animation.connections)
                        if (PathNames(connection.connectedEntity, entity.shortGUID))
                            return true;
                }
            }

            return false;
        }

        private static bool PathNames(EntityPath path, ShortGuid entityId)
        {
            if (path?.path == null)
                return false;

            foreach (ShortGuid step in path.path)
                if (step == entityId)
                    return true;

            return false;
        }

        /* The live page of that name, for undo to work on */
        public Flowgraph FindFlowgraph(string name)
        {
            return _flowgraphs.FirstOrDefault(o => o != null && !o.IsDisposed && o.FlowgraphName == name);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (UndoKeys.TryHandle(keyData))
                return true;
            if (UnityConnection.ViewerCreateModeKeys.TryHandle(keyData))
                return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        //Entities in this composite that are pointed to by a proxy anywhere in the level
        private readonly HashSet<ShortGuid> _proxiedEntities = new HashSet<ShortGuid>();

        private void RebuildProxiedEntityCache()
        {
            _proxiedEntities.Clear();
            if (_composite == null || Content?.Level?.Commands?.Utils == null)
                return;

            CommandsUtils utils = Content.Level.Commands.Utils;
            foreach (Composite comp in Content.Level.Commands.Entries)
            {
                foreach (ProxyEntity proxy in comp.proxies)
                {
                    (Composite targetComp, Entity targetEnt) = utils.GetResolvedTarget(utils.ResolveProxy(proxy));
                    if (targetEnt != null && targetComp?.shortGUID == _composite.shortGUID)
                        _proxiedEntities.Add(targetEnt.shortGUID);
                }
            }
        }

        /* Update the multi-node / proxy-reference markers shown on flowgraph nodes */
        public void RefreshNodeMarkers()
        {
            if (_flowgraphs.Count == 0)
                return;

            Dictionary<ShortGuid, int> nodeCounts = new Dictionary<ShortGuid, int>();
            foreach (Flowgraph flowgraph in _flowgraphs)
            {
                if (flowgraph?.Nodegraph == null)
                    continue;
                foreach (STNode node in flowgraph.Nodegraph.Nodes)
                {
                    if (node?.Entity == null)
                        continue;
                    nodeCounts.TryGetValue(node.ShortGUID, out int count);
                    nodeCounts[node.ShortGUID] = count + 1;
                }
            }

            foreach (Flowgraph flowgraph in _flowgraphs)
            {
                if (flowgraph?.Nodegraph == null)
                    continue;
                foreach (STNode node in flowgraph.Nodegraph.Nodes)
                {
                    if (node?.Entity == null)
                        continue;
                    node.ShowMultiNodeMarker = nodeCounts.TryGetValue(node.ShortGUID, out int count) && count > 1;
                    node.ShowProxyRefMarker = _proxiedEntities.Contains(node.ShortGUID);
                }
            }

            RefreshZoneColours();
        }

        /// <summary>
        /// Stripe every node in the colour of its entity's zone, the colour the viewport draws that zone in,
        /// while it is highlighting zones. The inspector shows the same colour for the entity it has open.
        /// </summary>
        public void RefreshZoneColours()
        {
            ZoneColours zones = ZoneColours.For(_composite, _path);

            foreach (Flowgraph flowgraph in _flowgraphs)
            {
                if (flowgraph?.Nodegraph == null)
                    continue;

                bool changed = false;
                foreach (STNode node in flowgraph.Nodegraph.Nodes)
                {
                    if (node.Entity == null)
                        continue;
                    ZoneColours.Match match = zones?.Find(node.Entity);
                    changed |= node.SetZone(match?.Colour ?? Color.Empty);
                }
                if (changed)
                    flowgraph.Nodegraph.Invalidate();
            }
        }

        //A new table was worked out (an edit, the level loading) or zones were switched on or off
        private void OnZoneTableChanged()
        {
            if (!Populated || IsDisposed)
                return;
            RefreshZoneColours();
            _entityDisplay?.RefreshZoneColour();
        }

        private void FocusEntityOnFlowgraph(Entity entity)
        {
            if (entity == null)
                return;

            Flowgraph activePage = _flowgraphs.FirstOrDefault(o => o.Visible);
            if (activePage != null && FlowgraphContainsEntity(activePage, entity))
            {
                FocusEntityOnFlowgraphDeferred(activePage, entity);
                return;
            }

            foreach (Flowgraph flowgraph in _flowgraphs)
            {
                if (flowgraph.Visible || !FlowgraphContainsEntity(flowgraph, entity))
                    continue;

                flowgraph.Show();
                FocusEntityOnFlowgraphDeferred(flowgraph, entity);
                return;
            }
        }

        private static void FocusEntityOnFlowgraphDeferred(Flowgraph flowgraph, Entity entity)
        {
            if (flowgraph == null || entity == null || flowgraph.IsDisposed)
                return;

            // ShowFlowgraph restores canvas position via BeginInvoke; defer focus so it is not overwritten.
            flowgraph.BeginInvoke(new Action(() =>
            {
                if (!flowgraph.IsDisposed)
                    flowgraph.SelectAllNodesForEntity(entity);
            }));
        }

        private static bool FlowgraphContainsEntity(Flowgraph flowgraph, Entity entity)
        {
            if (flowgraph == null || entity == null)
                return false;

            foreach (STNode node in flowgraph.Nodegraph.Nodes)
            {
                if (node.Entity?.shortGUID == entity.shortGUID)
                    return true;
            }

            return false;
        }

        private void deleteComposite_Click(object sender, EventArgs e)
        {
            _compositeBrowser.DeleteComposite(_composite);
        }

        public void DeleteEntity(Entity entity, bool ask = true, bool reloadUI = true)
        {
            if (entity == null || Composite == null || Composite.GetEntityByID(entity.shortGUID) == null)
                return;
            Composite composite = Composite;
            if (ask && MessageBox.Show("Are you sure you want to remove this entity?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            //The box pumps messages: while it was up the entity may have gone (a second Delete from the viewport, an
            //undo) or the display moved on - applying the delete then threw "no longer in the composite"
            if (Composite != composite || composite.GetEntityByID(entity.shortGUID) == null)
                return;

            //The removal itself - the dictionary entry, every link into the entity, the trigger and
            //animation references that went through it, its nodes on saved and open pages - is the
            //edit, which is what puts each piece back on undo. The inspector and the entity list follow
            //OnEntityDeleted as they do for any other removal.
            UndoStack.Current.Apply(new EntityDeleteEdit(Composite, entity, "Delete " + UndoLabels.Entity(Composite, entity)));
        }

        /// <summary>
        /// Delete several entities as one step, with one question rather than one per entity.
        /// Returns false if the user said no, or there was nothing to delete.
        /// </summary>
        public bool DeleteEntities(List<Entity> entities, bool ask = true)
        {
            if (entities == null || Composite == null)
                return false;

            List<Entity> toDelete = entities
                .Where(o => o != null && Composite.GetEntityByID(o.shortGUID) != null)
                .GroupBy(o => o.shortGUID)
                .Select(o => o.First())
                .ToList();
            if (toDelete.Count == 0)
                return false;

            if (toDelete.Count == 1)
            {
                DeleteEntity(toDelete[0], ask);
                return Composite.GetEntityByID(toDelete[0].shortGUID) == null;
            }

            Composite composite = Composite;
            if (ask && MessageBox.Show("Are you sure you want to remove these " + toDelete.Count + " entities?",
                    "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return false;
            }
            //As DeleteEntity: the box pumps messages, and the display may have moved on meanwhile
            if (Composite != composite)
                return false;

            using (UndoStack.Current.BeginGroup("Delete " + UndoLabels.Count(toDelete.Count, "entity", "entities")))
            {
                foreach (Entity entity in toDelete)
                    DeleteEntity(entity, ask: false);
            }

            return true;
        }

        /* Copy entities of this composite to the shared clipboard, capturing the current drill path
           so a reference-paste in an ancestor composite can build aliases down to them. */
        public void CopyEntitiesToClipboard(List<EntityClipboard.Entry> entries)
        {
            if (Composite == null || entries == null || entries.Count == 0)
                return;

            List<EntityClipboard.PathStep> pathSteps = new List<EntityClipboard.PathStep>();
            for (int i = 0; i < _path.AllComposites.Count && i < _path.AllEntities.Count; i++)
            {
                if (_path.AllComposites[i] == null || _path.AllEntities[i] == null)
                {
                    pathSteps.Clear(); //A broken chain can't be used for aliasing
                    break;
                }
                pathSteps.Add(new EntityClipboard.PathStep()
                {
                    CompositeId = _path.AllComposites[i].shortGUID.AsUInt32,
                    InstanceEntityId = _path.AllEntities[i].shortGUID.AsUInt32,
                });
            }

            foreach (EntityClipboard.Entry entry in entries)
                ResolveAliasEntryForCopy(entry);

            EntityClipboard.Set(Composite.shortGUID.AsUInt32, entries, pathSteps);
        }

        /* An alias entry records the entity it points at, so a clone paste can copy that rather than
           make a second pointer to it (deep-selecting something in the viewport and pasting used to
           give you another alias, which puts nothing in the world). The entity's own position is
           relative to the composite that owns it, so where it sits from here is worked out now, while
           the path to it is still in front of us - the alias itself may be gone by paste time. */
        private void ResolveAliasEntryForCopy(EntityClipboard.Entry entry)
        {
            if (entry == null || Composite == null || Content?.Level?.Commands == null)
                return;

            AliasEntity alias = Composite.GetEntityByID(new ShortGuid(entry.EntityId)) as AliasEntity;
            if (alias == null)
                return;

            List<Tuple<Composite, Entity>> path = Content.Level.Commands.Utils.ResolveAlias(alias, Composite);
            (Composite targetComposite, Entity target) = Content.Level.Commands.Utils.GetResolvedTarget(path);
            if (targetComposite == null || target == null || targetComposite.shortGUID == Composite.shortGUID)
                return;

            entry.ResolvedEntityId = target.shortGUID.AsUInt32;
            entry.ResolvedCompositeId = targetComposite.shortGUID.AsUInt32;

            /* Where it sits: the alias's own override of the position wins, since that is where the user
               has actually put it, and every instance on the way down is folded in over the top. An
               entity with no position of its own is not given one - it isn't placed in the world. */
            cTransform local = InstanceTransform.TransformOf(alias) ?? InstanceTransform.TransformOf(target);
            if (local == null)
                return;

            cTransform chain = null;
            for (int i = 0; i < path.Count - 1; i++)
                chain = InstanceTransform.Compose(chain, InstanceTransform.TransformOf(path[i].Item2));

            entry.ResolvedPlacement = InstanceTransform.Compose(chain, local);
        }

        /* Clone all clipboard entities into this composite: new GUIDs, unique names, parameters kept,
           and (when restoreInternalLinks) links kept only where both ends were copied (remapped to the
           new GUIDs). Returns one result per clipboard entry - duplicate entries share a clone. */
        public List<Tuple<EntityClipboard.Entry, Entity>> CloneClipboardEntities(bool restoreInternalLinks = true)
        {
            List<Tuple<EntityClipboard.Entry, Entity>> results = new List<Tuple<EntityClipboard.Entry, Entity>>();
            if (!EntityClipboard.HasContent || !Populated || Composite == null)
                return results;

            Composite sourceComposite = Content.Level.Commands.GetComposite(new ShortGuid(EntityClipboard.SourceCompositeId));
            if (sourceComposite == null)
                return results;

            Singleton.OnEntityAddPending?.Invoke();

            Dictionary<uint, Entity> clonesBySourceId = new Dictionary<uint, Entity>();
            foreach (EntityClipboard.Entry entry in EntityClipboard.Entries)
            {
                /* An aliased entry copies the entity the alias points at, which lives in the composite
                   further down that the alias names - so it is fetched from there, and placed where it
                   sits from here rather than where it sits in there. */
                Composite entryComposite = sourceComposite;
                uint entryEntityId = entry.EntityId;
                if (entry.HasResolvedTarget)
                {
                    Composite resolved = Content.Level.Commands.GetComposite(new ShortGuid(entry.ResolvedCompositeId));
                    if (resolved != null)
                    {
                        entryComposite = resolved;
                        entryEntityId = entry.ResolvedEntityId;
                    }
                }

                if (!clonesBySourceId.TryGetValue(entryEntityId, out Entity clone))
                {
                    Entity source = entryComposite.GetEntityByID(new ShortGuid(entryEntityId));
                    if (source == null)
                        continue;

                    clone = CloneEntityForPaste(entryComposite, source);
                    if (clone == null)
                        continue;

                    if (entry.ResolvedPlacement != null)
                        SetPastedPlacement(clone, entry.ResolvedPlacement);

                    clonesBySourceId.Add(entryEntityId, clone);
                }
                results.Add(new Tuple<EntityClipboard.Entry, Entity>(entry, clone));
            }

            if (clonesBySourceId.Count == 0)
                return results;

            //Restore the links that ran between the copied entities, remapped onto the clones
            if (restoreInternalLinks)
            {
                foreach (KeyValuePair<uint, Entity> pair in clonesBySourceId)
                {
                    Entity source = sourceComposite.GetEntityByID(new ShortGuid(pair.Key));
                    if (source == null)
                        continue;

                    foreach (EntityConnector link in source.childLinks)
                    {
                        if (clonesBySourceId.TryGetValue(link.linkedEntityID.AsUInt32, out Entity linkedClone))
                            pair.Value.AddParameterLink(link.thisParamID, linkedClone.shortGUID, link.linkedParamID);
                    }
                }
            }

            //A copied animation or trigger sequence points at the copies of what it drove, not the originals
            Dictionary<Entity, Entity> copiedFromSource = new Dictionary<Entity, Entity>();
            foreach (KeyValuePair<uint, Entity> pair in clonesBySourceId)
            {
                Entity source = sourceComposite.GetEntityByID(new ShortGuid(pair.Key));
                if (source != null) copiedFromSource[source] = pair.Value;
            }
            RemapCopiedReferences(Content.Level.Commands, sourceComposite, Composite, copiedFromSource);

            Content.EditorUtils.GenerateCompositeInstances(Content.Level.Commands);
            using (UndoStack.Current.BeginGroup("Paste " + UndoLabels.Count(clonesBySourceId.Count, "entity", "entities")))
            {
                foreach (Entity clone in clonesBySourceId.Values)
                {
                    UndoStack.Current.Record(new EntityAddEdit(Composite, clone, "Paste " + UndoLabels.Entity(Composite, clone)));
                    Singleton.OnEntityAdded?.Invoke(clone);
                }
            }

            return results;
        }

        private Entity CloneEntityForPaste(Composite sourceComposite, Entity source)
        {
            return CloneEntityForPaste(Content.Level.Commands, sourceComposite, source, Composite);
        }

        /// <summary>
        /// After a copy: a copied CAGEAnimation's tracks and bindings, its animation-entity events, and a copied TriggerSequence's
        /// entries that named an entity copied with it now name that entity's copy - otherwise a duplicated camera move keeps
        /// driving the original camera, and one copied elsewhere points at nothing. <paramref name="copies"/> maps each source
        /// entity (in <paramref name="source"/>) to its copy (in <paramref name="destination"/>). With <paramref name="keyOffset"/>,
        /// the x/y/z keys of a remapped entity's own position move by it, as the entity itself was moved. Returns what is still
        /// left pointing outside the copies, one line each, for a caller to report. Static so the MCP copy_entities tool does the
        /// same inside its own undo step (the copies are new, so nothing here needs its own undo record).
        /// </summary>
        internal static List<string> RemapCopiedReferences(Commands commands, Composite source, Composite destination, IDictionary<Entity, Entity> copies, System.Numerics.Vector3? keyOffset = null)
        {
            List<string> left = new List<string>();
            if (copies == null || copies.Count == 0)
                return left;
            Dictionary<ShortGuid, ShortGuid> ids = new Dictionary<ShortGuid, ShortGuid>();
            foreach (KeyValuePair<Entity, Entity> pair in copies)
                if (pair.Key != null && pair.Value != null) ids[pair.Key.shortGUID] = pair.Value.shortGUID;
            ShortGuid position = ShortGuidUtils.Generate("position");
            string[] axes = { "x", "y", "z" };

            //A stored path whose first step is a copied entity in the source composite, read from there, now starts at its copy
            EntityPath Remap(EntityPath path, string owner, string what)
            {
                if (path?.path == null || path.path.Length == 0)
                    return path;
                if (ids.TryGetValue(path.path[0], out ShortGuid copied) && source.GetEntityByID(path.path[0]) != null)
                {
                    ShortGuid[] steps = (ShortGuid[])path.path.Clone();
                    steps[0] = copied;
                    return new EntityPath(steps);
                }
                (Composite _, Entity target) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(path, destination));
                if (target == null)
                    left.Add(owner + ": " + what + " points at nothing in " + destination.name);
                else if (destination == source)
                    left.Add(owner + ": " + what + " still names " + commands.Utils.GetEntityName(destination, target) + " (it was not copied)");
                return path;
            }

            foreach (KeyValuePair<Entity, Entity> pair in copies)
            {
                Entity copy = pair.Value;
                if (copy == null) continue;
                string owner = commands.Utils.GetEntityName(destination, copy);
                if (copy is CAGEAnimation animation)
                {
                    foreach (CAGEAnimation.Connection connection in animation.connections)
                    {
                        if (connection == null) continue;
                        EntityPath before = connection.connectedEntity;
                        connection.connectedEntity = Remap(before, owner, connection.binding_type == ObjectType.ENTITY ? "a track" : "its " + connection.binding_type.ToString().ToLowerInvariant() + " binding");
                        //The copy was moved by the offset: its own position keys go with it (a nested entity's are relative to its instance)
                        if (keyOffset != null && !ReferenceEquals(before, connection.connectedEntity) && connection.target_param == position && connection.connectedEntity.path.Count(o => o != ShortGuid.Invalid) == 1)
                        {
                            int axis = Array.FindIndex(axes, o => ShortGuidUtils.Generate(o) == connection.target_sub_param);
                            CAGEAnimation.FloatTrack track = animation.floatTracks.FirstOrDefault(o => o != null && o.shortGUID == connection.target_track);
                            if (axis >= 0 && track != null)
                            {
                                float by = axis == 0 ? keyOffset.Value.X : axis == 1 ? keyOffset.Value.Y : keyOffset.Value.Z;
                                foreach (CAGEAnimation.FloatTrack.Keyframe key in track.keyframes)
                                    key.value.Y += by;
                            }
                        }
                    }
                    foreach (CAGEAnimation.EventTrack track in animation.eventTracks)
                    {
                        if (track?.keyframes == null) continue;
                        foreach (CAGEAnimation.EventTrack.Keyframe key in track.keyframes)
                        {
                            if (key == null || key.track_type != CATHODE.Enums.ANIM_TRACK_TYPE.T_GUID) continue;
                            if (ids.TryGetValue(key.forward, out ShortGuid copied))
                                key.forward = copied;
                            else if (destination.GetEntityByID(key.forward) == null)
                                left.Add(owner + ": an event at " + Math.Round(key.time, 3) + " s fires an entity that is not in " + destination.name);
                        }
                    }
                }
                else if (copy is TriggerSequence sequence)
                {
                    foreach (TriggerSequence.SequenceEntry entry in sequence.sequence)
                        if (entry != null) entry.connectedEntity = Remap(entry.connectedEntity, owner, "an entry");
                }
            }
            return left;
        }

        /// <summary>
        /// A copy of <paramref name="source"/> added to <paramref name="destination"/> as a clone paste makes one:
        /// a new id, its resources rebound, no links, a unique "_N" name (a variable keeps its pin name), its pin
        /// type if it is a variable, and the source's modified marks. Null when it would put a composite inside
        /// itself. Static so the MCP copy_entities tool makes the same copy inside its own undo step.
        /// </summary>
        internal static Entity CloneEntityForPaste(Commands commands, Composite sourceComposite, Entity source, Composite destination)
        {
            //Composite instances must not create infinite instancing loops in this composite
            if (source.variant == EntityVariant.FUNCTION)
            {
                FunctionEntity functionEntity = (FunctionEntity)source;
                if (!functionEntity.function.IsFunctionType)
                {
                    Composite instanceComposite = commands.GetComposite(functionEntity.function);
                    if (instanceComposite != null
                        && commands.Utils.WouldCreateCompositeInstanceCycle(destination, instanceComposite))
                    {
                        return null;
                    }
                }
            }

            Entity clone = null;
            switch (source.variant)
            {
                case EntityVariant.FUNCTION:
                    clone = ((FunctionEntity)source).Copy();
                    break;
                case EntityVariant.VARIABLE:
                    clone = ((VariableEntity)source).Copy();
                    break;
                case EntityVariant.ALIAS:
                    clone = ((AliasEntity)source).Copy();
                    break;
                case EntityVariant.PROXY:
                    clone = ((ProxyEntity)source).Copy();
                    break;
            }
            if (clone == null)
                return null;

            clone.shortGUID = ShortGuidUtils.GenerateRandom();
            clone.RebindResourcesAsCopyOf(source);
            clone.childLinks.Clear(); //No external links: only links between copied entities get restored

            switch (clone.variant)
            {
                case EntityVariant.FUNCTION:
                    destination.functions_dictionary.Add(((FunctionEntity)clone).shortGUID, (FunctionEntity)clone);
                    break;
                case EntityVariant.VARIABLE:
                    destination.variables_dictionary.Add(((VariableEntity)clone).shortGUID, (VariableEntity)clone);
                    break;
                case EntityVariant.PROXY:
                    destination.proxies_dictionary.Add(((ProxyEntity)clone).shortGUID, (ProxyEntity)clone);
                    break;
                case EntityVariant.ALIAS:
                    destination.aliases_dictionary.Add(((AliasEntity)clone).shortGUID, (AliasEntity)clone);
                    break;
            }

            //Name the clone "<source>_1" (or _2, _3... until unique). Variables keep their name: it's the pin name.
            if (clone.variant != EntityVariant.VARIABLE)
            {
                string baseName = commands.Utils.GetEntityName(sourceComposite, source);
                commands.Utils.SetEntityName(clone, GetUniquePasteName(commands, destination, baseName));
            }

            /* A variable's pin type is not on the entity but in the pin table, keyed by its guid - so the
               copy, under a fresh guid, had none: nothing to draw its node from (crash 391 in
               NodeUtils.AddAllPins) and a plain parameter when saved. It takes the source's. */
            if (clone.variant == EntityVariant.VARIABLE)
            {
                CompositePinInfoTable.PinInfo sourceInfo = commands.Utils.GetPinInfo(sourceComposite, (VariableEntity)source);
                if (sourceInfo != null)
                {
                    commands.Utils.SetPinInfo(destination, new CompositePinInfoTable.PinInfo()
                    {
                        VariableGUID = clone.shortGUID,
                        PinTypeGUID = sourceInfo.PinTypeGUID,
                        PinEnumTypeGUID = sourceInfo.PinEnumTypeGUID,
                    });
                }
                else
                {
                    Debug.Log("Composite Display", "The variable " + source.shortGUID.ToByteString() + " in " + sourceComposite.name + " has no pin info to copy");
                }
            }

            //The clone carries the source's values, so it inherits its "modified from default" state too
            ParameterModificationTracker.CopyEntityModifications(sourceComposite.shortGUID, source.shortGUID, destination.shortGUID, clone.shortGUID);

            return clone;
        }

        /// <summary>
        /// The name a new function entity gets: its type and the next number up ("TriggerSimple_3" with two already
        /// here), however it was added - palette, flowgraph, viewport, the Add Function dialog or MCP.
        /// </summary>
        public static string NewFunctionName(Commands commands, Composite composite, FunctionType function)
        {
            return NextNewEntityName(commands, composite, function.ToString(), composite.functions.Count(o => o.function == function));
        }

        /// <summary>The name a new composite instance gets, numbered the same way: "Door_Package_1", "Door_Package_2".</summary>
        public static string NewInstanceName(Commands commands, Composite composite, Composite instanced)
        {
            return NextNewEntityName(commands, composite, EditorUtils.GetCompositeName(instanced), composite.functions.Count(o => o.function == instanced.shortGUID));
        }

        /* Counting alone handed a name out twice after a delete or a rename (TriggerSimple_1 deleted, the next new one was
           TriggerSimple_2 again), so the count is where the search starts, and a name already here is stepped past */
        private static string NextNewEntityName(Commands commands, Composite composite, string stem, int alreadyHere)
        {
            HashSet<string> taken = new HashSet<string>(composite.GetEntities().Select(o => commands.Utils.GetEntityName(composite, o)), StringComparer.OrdinalIgnoreCase);
            for (int i = alreadyHere + 1; ; i++)
                if (!taken.Contains(stem + "_" + i))
                    return stem + "_" + i;
        }

        internal static string GetUniquePasteName(Commands commands, Composite destination, string baseName)
        {
            //A source already ending in _N ("door_2") continues that numbering ("door_3", "door_4"...)
            //rather than becoming "door_2_1".
            string root = baseName;
            int index = 1;
            int underscore = baseName.LastIndexOf('_');
            if (underscore > 0 && underscore < baseName.Length - 1
                && int.TryParse(baseName.Substring(underscore + 1), out int existingIndex) && existingIndex >= 0)
            {
                root = baseName.Substring(0, underscore);
                index = existingIndex + 1;
            }

            HashSet<string> usedNames = new HashSet<string>();
            foreach (Entity existing in destination.GetEntities())
                usedNames.Add(commands.Utils.GetEntityName(destination, existing));

            string name = root + "_" + index;
            while (usedNames.Contains(name))
            {
                index++;
                name = root + "_" + index;
            }
            return name;
        }

        /* Paste the clipboard into this composite from outside the flowgraph UI (viewport Ctrl+V,
           entity list Paste). Where the composite uses flowgraphs, this goes through the page on
           screen: links are compiled back out of the pages on save, so a link that isn't on one
           would be thrown away (and would mark the composite as diverged from its layout). */
        public void PasteClipboardFromViewport()
        {
            if (!EntityClipboard.HasContent || !Populated)
                return;

            SelectPastedClones(CloneClipboardEntities(restoreInternalLinks: !SupportsFlowgraphs));
        }

        /* The page paste announces its own selection; the page-less branches did not, so a
           multi-entity paste (or viewport duplicate) selected only the last clone - and the viewer's
           gizmo then drove one copy. Select the whole set the same way a multi-selection does. */
        private void SelectPastedClones(List<Tuple<EntityClipboard.Entry, Entity>> pasted)
        {
            if (pasted == null)
                return;

            List<Entity> clones = new List<Entity>();
            foreach (Tuple<EntityClipboard.Entry, Entity> pair in pasted)
            {
                if (pair.Item2 != null && !clones.Contains(pair.Item2))
                    clones.Add(pair.Item2);
            }

            if (clones.Count > 1)
                ApplyMultiSelection(clones);
            else if (clones.Count == 1)
                LoadEntity(clones[0], false);
        }

        /// <summary>
        /// Duplicate entities in place (a viewport shift-clone): copies with new GUIDs at the same
        /// position, selected so the viewer can hand its drag to them. Goes through the same clone-paste
        /// path as Ctrl+V, so it carries every fix that path has (resource rebind, alias resolution,
        /// unique naming) - but without disturbing the user's real clipboard.
        /// </summary>
        public void DuplicateEntities(List<Entity> sources, object undoGesture = null)
        {
            if (sources == null || sources.Count == 0 || !Populated || Composite == null)
                return;

            //Save and restore the real clipboard around the copy: a duplicate must not clobber it
            uint savedComposite = EntityClipboard.SourceCompositeId;
            List<EntityClipboard.Entry> savedEntries = EntityClipboard.Entries;
            List<EntityClipboard.PathStep> savedPath = EntityClipboard.SourcePath;

            try
            {
                List<EntityClipboard.Entry> entries = new List<EntityClipboard.Entry>();
                foreach (Entity source in sources)
                {
                    if (source != null)
                        entries.Add(new EntityClipboard.Entry() { EntityId = source.shortGUID.AsUInt32, Offset = System.Drawing.Point.Empty });
                }
                if (entries.Count == 0)
                    return;

                //Resolves deep-select aliases to their target + placement, exactly as a copy does
                CopyEntitiesToClipboard(entries);

                string label = sources.Count == 1
                    ? "Duplicate " + OpenCAGE.Undo.UndoLabels.Entity(Composite, sources[0])
                    : "Duplicate " + OpenCAGE.Undo.UndoLabels.Count(sources.Count, "entity", "entities");
                //A viewport shift-clone's drag joins this step (see UndoStack.BeginGroup)
                using (OpenCAGE.Undo.UndoStack.Current.BeginGroup(label, undoGesture))
                    PasteClipboardFromViewport();
            }
            finally
            {
                EntityClipboard.Set(savedComposite, savedEntries, savedPath);
            }
        }

        private void exportComposite_Click(object sender, EventArgs e)
        {
            ExportComposite dialog = new ExportComposite(Composite);
            dialog.Show();
        }

        /* Context menu composite close options */
        private void closeSelected_Click(object sender, EventArgs e)
        {
            CloseAllChildTabs();
            Close();
        }

        private void createVariableEntityToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CreateEntity(EntityVariant.VARIABLE);
        }
        private void createFunctionEntityToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CreateEntity(EntityVariant.FUNCTION);
        }
        private void createCompositeEntityToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CreateEntity(EntityVariant.FUNCTION, true);
        }
        private void createProxyEntityToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CreateEntity(EntityVariant.PROXY);
        }
        private void createAliasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CreateEntity(EntityVariant.ALIAS);
        }

        AddEntity_Variable dialog_var = null;
        AddEntity_Function dialog_func = null;
        AddEntity_CompositeInstance dialog_compinst = null;
        SelectHierarchy dialog_hierarchy = null; EntityVariant dialog_hierarchy_entvar;

        /* Re-pointing a proxy: the same picker as creating one, rooted at the level's entry point and
           opened as far down the proxy's current path as still exists - for a dead proxy (see
           CommandsUtils.IsDeadProxy) that lands the user where the break is. */
        private ProxyEntity _retargetingProxy = null;
        public void ChangeProxyTarget(ProxyEntity proxy)
        {
            if (proxy == null || _composite == null || _composite.GetEntityByID(proxy.shortGUID) != proxy)
                return;
            if (Content?.Level?.Commands?.EntryPoints == null || Content.Level.Commands.EntryPoints.Length == 0 || Content.Level.Commands.EntryPoints[0] == null)
                return;

            if (dialog_hierarchy != null)
                dialog_hierarchy.Close();

            _retargetingProxy = proxy;
            dialog_hierarchy_entvar = EntityVariant.PROXY;
            dialog_hierarchy = new SelectHierarchy(Content.Level.Commands.EntryPoints[0], new CompositeEntityList.DisplayOptions()
            {
                DisplayAliases = false,
                DisplayFunctions = true,
                DisplayProxies = false,
                DisplayVariables = false,
            });
            dialog_hierarchy.Text = "Change Proxy Target - " + Content.Level.Commands.Utils.GetEntityName(_composite, proxy);
            ShortGuid[] path = proxy.proxy?.path ?? new ShortGuid[0];
            int end = path.Length;
            if (end > 0 && path[end - 1] == ShortGuid.Invalid)
                end--; //the terminator some paths carry
            if (end > 2)
            {
                //path[0] is the composite the path is read from and the last entry is the target itself:
                //the instances between them are the navigation (minus a doubled hop, which the resolver skips)
                List<ShortGuid> instances = new List<ShortGuid>(end - 2);
                for (int i = 1; i < end - 1; i++)
                {
                    if (i > 1 && path[i] == path[i - 1])
                        continue;
                    instances.Add(path[i]);
                }
                dialog_hierarchy.TryRestoreNavigation(instances.ToArray());
            }
            dialog_hierarchy.OnHierarchyGenerated += OnProxyRetargetHierarchyGenerated;
            dialog_hierarchy.FormClosed += (s, e) => { if (_retargetingProxy == proxy) _retargetingProxy = null; };
            dialog_hierarchy.Show();
            dialog_hierarchy.Focus();
        }
        private void OnProxyRetargetHierarchyGenerated(ShortGuid[] generatedHierarchy)
        {
            ProxyEntity proxy = _retargetingProxy;
            _retargetingProxy = null;
            if (proxy == null || _composite == null || _composite.GetEntityByID(proxy.shortGUID) != proxy || generatedHierarchy == null)
                return;

            Commands commands = Content.Level.Commands;
            List<ShortGuid> hierarchy = new List<ShortGuid>();
            hierarchy.Add(commands.EntryPoints[0].shortGUID);
            hierarchy.AddRange(generatedHierarchy);
            ShortGuid[] newPath = hierarchy.ToArray();

            (Composite pointedComp, Entity pointedEnt) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveProxy(newPath));
            if (!(pointedEnt is FunctionEntity target))
            {
                MessageBox.Show("A proxy can only point to a function entity.", "Cannot re-point proxy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ShortGuid[] pathBefore = (ShortGuid[])(proxy.proxy?.path ?? new ShortGuid[0]).Clone();
            ShortGuid functionBefore = proxy.function;
            proxy.proxy = new EntityPath(newPath);
            proxy.function = target.function;

            UndoStack.Current.Record(new ProxyRetargetEdit(_composite, proxy, pathBefore, functionBefore,
                "Re-point " + UndoLabels.Entity(_composite, proxy) + " to " + commands.Utils.GetEntityName(pointedComp, target)));
            AfterProxyRetargeted(_composite, proxy);
        }

        /// <summary>
        /// A proxy points somewhere else now (by the user, or undo): its nodes, the markers on what it
        /// points at, the inspector and the viewer's copy all follow.
        /// </summary>
        public void AfterProxyRetargeted(Composite composite, ProxyEntity proxy)
        {
            if (proxy == null)
                return;

            //The level changed, whether by the user or by undo: none of the events DirtyTracker listens to fire for this
            DirtyTracker.MarkLevelDataModified();
            if (!Populated)
                return;

            RebuildProxiedEntityCache();
            RefreshNodeMarkers();
            if (composite == _composite)
            {
                foreach (Flowgraph flowgraph in _flowgraphs)
                    flowgraph?.RestyleEntity(proxy);
                _entityList?.List?.UpdateEntityInList(proxy);
            }
            if (_entityDisplay != null && _entityDisplay.Entity == proxy)
                _entityDisplay.Reload();

            Send.EntityRetargeted(proxy, composite);
        }
        public Popups.Base.BaseWindow CreateEntity(EntityVariant variant = EntityVariant.FUNCTION, bool composite = false)
        {
            if (variant == EntityVariant.FUNCTION && !composite)
            {
                if (dialog_func != null)
                    dialog_func.Close();

                dialog_func = new AddEntity_Function(Composite, SupportsFlowgraphs);
                dialog_func.Show();
                dialog_func.Focus();

                return dialog_func;
            }
            else if (variant == EntityVariant.FUNCTION && composite)
            {
                if (dialog_compinst != null)
                    dialog_compinst.Close();

                dialog_compinst = new AddEntity_CompositeInstance(Composite, SupportsFlowgraphs);
                dialog_compinst.Show();
                dialog_compinst.Focus();

                return dialog_compinst;
            }
            else if (variant == EntityVariant.PROXY || variant == EntityVariant.ALIAS)
            {
                if (dialog_hierarchy != null)
                    dialog_hierarchy.Close();

                dialog_hierarchy_entvar = variant;
                switch (dialog_hierarchy_entvar)
                {
                    case EntityVariant.PROXY:
                        dialog_hierarchy = new SelectHierarchy(Content.Level.Commands.EntryPoints[0], new CompositeEntityList.DisplayOptions()
                        {
                            DisplayAliases = false,
                            DisplayFunctions = true,
                            DisplayProxies = false,
                            DisplayVariables = false,
                        });
                        dialog_hierarchy.Text = "Create Proxy";
                        dialog_hierarchy.TryRestoreNavigation(
                            ParseSavedProxyHierarchy(SettingsManager.GetStringArray(Settings.PreviouslySelectedProxyHierarchy)),
                            SettingsManager.GetString(Settings.PreviouslySearchedProxyEntity));
                        break;
                    case EntityVariant.ALIAS:
                        dialog_hierarchy = new SelectHierarchy(_composite, new CompositeEntityList.DisplayOptions()
                        {
                            DisplayAliases = false,
                            DisplayFunctions = true,
                            DisplayProxies = true,
                            DisplayVariables = true,
                        });
                        dialog_hierarchy.Text = "Create Alias";
                        break;
                }
                dialog_hierarchy.OnHierarchyGenerated += OnNewEntityHierarchyGenerated;
                dialog_hierarchy.Show();
                dialog_hierarchy.Focus();

                return dialog_hierarchy;
            }
            else if (variant == EntityVariant.VARIABLE)
            {
                if (dialog_var != null)
                    dialog_var.Close();

                dialog_var = new AddEntity_Variable(Composite, SupportsFlowgraphs);
                dialog_var.Show();
                dialog_var.Focus();

                return dialog_var;
            }
            return null; 
        }

        private static ShortGuid[] ParseSavedProxyHierarchy(string[] saved)
        {
            if (saved == null || saved.Length == 0)
                return null;

            List<ShortGuid> path = new List<ShortGuid>(saved.Length);
            for (int i = 0; i < saved.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(saved[i]))
                    continue;
                try
                {
                    path.Add(new ShortGuid(saved[i]));
                }
                catch
                {
                    // Skip malformed entries from older/corrupt settings
                }
            }
            return path.Count > 0 ? path.ToArray() : null;
        }

        public Entity CreateCompositeInstanceEntity(string compositeName, PointF? flowgraphPosition = null, cTransform position = null)
        {
            if (string.IsNullOrWhiteSpace(compositeName))
                return null;

            return CreateCompositeInstanceEntity(Content.Level.Commands.GetComposite(compositeName), flowgraphPosition, position);
        }

        public Entity CreateCompositeInstanceEntity(Composite instanceComposite, PointF? flowgraphPosition = null, cTransform position = null)
        {
            //The entity and the node placed for it undo as one step
            using (UndoStack.Current.BeginGroup("Add " + System.IO.Path.GetFileName((instanceComposite?.name ?? "").Replace('\\', '/'))))
                return CreateCompositeInstanceEntityCore(instanceComposite, flowgraphPosition, position);
        }
        private Entity CreateCompositeInstanceEntityCore(Composite instanceComposite, PointF? flowgraphPosition, cTransform position)
        {
            if (!Populated || instanceComposite == null)
                return null;

            if (Content.Level.Commands.Utils.WouldCreateCompositeInstanceCycle(Composite, instanceComposite))
            {
                MessageBox.Show(
                    "You cannot instance a composite that already contains this composite (directly or through nested instances) — that would create an infinite loop at runtime.",
                    "Logic error!",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return null;
            }

            string entityName = NewInstanceName(Content.Level.Commands, Composite, instanceComposite);

            Singleton.OnEntityAddPending?.Invoke();

            Entity newEntity = Composite.AddFunction(instanceComposite);
            Content.Level.Commands.Utils.SetEntityName(Composite, newEntity, entityName);

            if (SettingsManager.GetBool(Settings.PreviouslySearchedParamPopulationComp, false))
            {
                Content.Level.Commands.Utils.AddAllDefaultParameters(newEntity, Composite);
                newEntity.RemoveParameter("delete_me");
            }

            //Set before the instance pass below, which is what works out where the instance sits in the world
            if (position != null)
                AddPlacedPosition(newEntity, position);

            Content.EditorUtils.GenerateCompositeInstances(Content.Level.Commands);
            SettingsManager.SetString(Settings.PreviouslySelectedCompInstType, instanceComposite.name);

            UndoStack.Current.Record(new EntityAddEdit(Composite, newEntity, "Add " + entityName));
            Singleton.OnEntityAdded?.Invoke(newEntity);

            if (flowgraphPosition.HasValue)
                PlaceEntityOnFlowgraph(newEntity, flowgraphPosition.Value);

            return newEntity;
        }

        /// <summary>
        /// A position chosen by placing the entity in the viewport is an edit like any other, so the
        /// inspector shows it bold from the start rather than only after the next move.
        /// </summary>
        /* An entity lifted out of a nested composite by a paste keeps the look of where it was, not the
           numbers: its own position was relative to the composite that owned it, and here it needs the
           instances it sat inside folded in. Marked modified for the same reason a viewport placement is
           - it is a value this paste chose, and the inspector should show it as one. */
        private void SetPastedPlacement(Entity entity, cTransform placement)
        {
            if (entity == null || placement == null)
                return;

            Parameter parameter = entity.AddParameter("position", placement);
            ParameterModificationTracker.SetParameterModified(Composite.shortGUID, entity.shortGUID, parameter.name);
        }

        private void AddPlacedPosition(Entity entity, cTransform position)
        {
            Parameter parameter = entity.AddParameter("position", position);
            ParameterModificationTracker.SetParameterModified(Composite.shortGUID, entity.shortGUID, parameter.name);
        }

        /* A box-shaped entity (Box, the trigger, nav and fog volumes) with no size is drawn at half 0.5 x 1 x 0.5 - the size
           the level build takes it as too - but the definitions' default, which inspecting it used to write in, is zero: an
           empty volume. It showed at the stand-in size, did nothing in the game, and vanished from the viewport the first
           time it was sent to it again (an undo of its delete, a duplicate). It starts with the size it is shown at. */
        private void GiveBoxItsShownSize(Entity entity, FunctionType function) => GiveNewBoxItsShownSize(Composite, entity, function);

        /// <summary>For a box volume just made: no size, or the definitions' zero, becomes the size it is drawn at.</summary>
        public static void GiveNewBoxItsShownSize(Composite composite, Entity entity, FunctionType function)
        {
            if (!IsBoxShaped(function))
                return;
            Parameter existing = entity.GetParameter("half_dimensions");
            if (existing != null && !(existing.content is cVector3 zero && zero.value == System.Numerics.Vector3.Zero))
                return;
            Parameter parameter = entity.AddParameter("half_dimensions", new cVector3(0.5f, 1f, 0.5f));
            if (composite != null)
                ParameterModificationTracker.SetParameterModified(composite.shortGUID, entity.shortGUID, parameter.name);
        }

        /// <summary>A function the viewport draws as a box volume. (GetPreviewKind answers Box for anything it does not list.)</summary>
        public static bool IsBoxShaped(FunctionType function) =>
            RenderFilterDefinitions.TryGetDefinition(function, out RenderFilterDefinitions.Definition definition) && definition.PreviewKind == RenderPreviewKind.Box;

        public Entity CreateFunctionEntity(FunctionType function, PointF? flowgraphPosition = null, cTransform position = null)
        {
            //The entity and the node placed for it undo as one step
            using (UndoStack.Current.BeginGroup("Add " + function))
                return CreateFunctionEntityCore(function, flowgraphPosition, position);
        }
        private Entity CreateFunctionEntityCore(FunctionType function, PointF? flowgraphPosition, cTransform position)
        {
            if (!Populated || Composite == null)
                return null;

            if (function == FunctionType.PhysicsSystem && Composite.functions.FirstOrDefault(o => o.function == FunctionType.PhysicsSystem) != null)
            {
                MessageBox.Show("You are trying to add a PhysicsSystem entity to a composite that already has one applied.", "PhysicsSystem error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            if (function == FunctionType.EnvironmentModelReference && Composite.functions.FirstOrDefault(o => o.function == FunctionType.EnvironmentModelReference) != null)
            {
                MessageBox.Show("You are trying to add a EnvironmentModelReference entity to a composite that already has one applied.", "EnvironmentModelReference error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            string entityName = NewFunctionName(Content.Level.Commands, Composite, function);

            Singleton.OnEntityAddPending?.Invoke();
            Entity newEntity = Composite.AddFunction(function);
            Content.Level.Commands.Utils.SetEntityName(Composite, newEntity, entityName);
            if (position != null)
                AddPlacedPosition(newEntity, position);
            GiveBoxItsShownSize(newEntity, function);
            SettingsManager.SetString(Settings.PreviouslySelectedFunctionType, function.ToString());
            EntityPaletteRecent.RecordFunction(function);

            UndoStack.Current.Record(new EntityAddEdit(Composite, newEntity, "Add " + entityName));
            Singleton.OnEntityAdded?.Invoke(newEntity);

            if (flowgraphPosition.HasValue)
                PlaceEntityOnFlowgraph(newEntity, flowgraphPosition.Value);

            return newEntity;
        }

        public Entity CreateVariableEntity(CompositePinType pinType, PointF? flowgraphPosition = null)
        {
            //The entity and the node placed for it undo as one step
            using (UndoStack.Current.BeginGroup("Add " + pinType.ToUIString()))
                return CreateVariableEntityCore(pinType, flowgraphPosition);
        }
        private Entity CreateVariableEntityCore(CompositePinType pinType, PointF? flowgraphPosition)
        {
            if (!Populated || Composite == null)
                return null;

            if (pinType == CompositePinType.CompositeInputVariablePin || pinType == CompositePinType.CompositeOutputVariablePin)
                return null;

            DataType datatype = pinType.GetDataType();
            ShortGuid enumType = new ShortGuid(0);

            bool isEnum = pinType == CompositePinType.CompositeInputEnumVariablePin || pinType == CompositePinType.CompositeOutputEnumVariablePin;
            bool isEnumString = pinType == CompositePinType.CompositeInputEnumStringVariablePin || pinType == CompositePinType.CompositeOutputEnumStringVariablePin;
            if (isEnum)
            {
                string[] enumNames = Enum.GetNames(typeof(EnumType)).OrderBy(o => o).ToArray();
                int index = SettingsManager.GetInteger(Settings.PrevVariableType_Enum);
                if (enumNames.Length == 0)
                    return null;
                if (index < 0 || index >= enumNames.Length)
                    index = 0;
                enumType = ShortGuidUtils.Generate(enumNames[index]);
            }
            else if (isEnumString)
            {
                string[] enumNames = Enum.GetNames(typeof(EnumStringType)).OrderBy(o => o).ToArray();
                int index = SettingsManager.GetInteger(Settings.PrevVariableType_EnumString);
                if (enumNames.Length == 0)
                    return null;
                if (index < 0 || index >= enumNames.Length)
                    index = 0;
                enumType = ShortGuidUtils.Generate(enumNames[index]);
            }

            string baseName = pinType.ToUIString().Replace(" ", "_");
            int count = 1;
            string entityName = baseName + "_" + count;
            while (Composite.variables.Any(o => o.name == ShortGuidUtils.Generate(entityName)))
            {
                count++;
                entityName = baseName + "_" + count;
            }

            Singleton.OnEntityAddPending?.Invoke();
            VariableEntity newEntity = Composite.AddVariable(entityName, datatype);
            Content.Level.Commands.Utils.SetPinInfo(Composite, new CompositePinInfoTable.PinInfo()
            {
                VariableGUID = newEntity.shortGUID,
                PinTypeGUID = new ShortGuid((uint)pinType),
                PinEnumTypeGUID = enumType
            });
            Content.Level.Commands.Utils.AddAllDefaultParameters(newEntity, Composite, true, ParameterVariant.REFERENCE_PIN | ParameterVariant.TARGET_PIN | ParameterVariant.STATE_PARAMETER | ParameterVariant.INPUT_PIN | ParameterVariant.OUTPUT_PIN | ParameterVariant.PARAMETER | ParameterVariant.INTERNAL | ParameterVariant.METHOD_FUNCTION | ParameterVariant.METHOD_PIN);
            if (newEntity.parameters.Count > 0 && newEntity.parameters[0].content.dataType == DataType.ENUM)
            {
                cEnum enumParam = (cEnum)newEntity.parameters[0].content;
                enumParam.enumID = enumType;
                enumParam.enumIndex = Content.Level.Commands.Utils.GetEnum(enumType).Entries[0].Index;
            }

            EntityPaletteRecent.RecordVariable(pinType);
            UndoStack.Current.Record(new EntityAddEdit(Composite, newEntity, "Add " + entityName));
            Singleton.OnEntityAdded?.Invoke(newEntity);

            if (flowgraphPosition.HasValue)
                PlaceEntityOnFlowgraph(newEntity, flowgraphPosition.Value);

            return newEntity;
        }

        public void PlaceEntityOnFlowgraph(Entity entity, PointF canvasPosition)
        {
            if (!SupportsFlowgraphs || entity == null)
                return;

            Flowgraph flowgraph = _flowgraphs.FirstOrDefault(o => o.Visible) ?? _flowgraphs.FirstOrDefault();
            flowgraph?.PlaceEntityAt(entity, canvasPosition);
        }

        private void OnNewEntityHierarchyGenerated(ShortGuid[] generatedHierarchy)
        {
            using (UndoStack.Current.BeginGroup(dialog_hierarchy_entvar == EntityVariant.PROXY ? "Add proxy" : "Add alias"))
                OnNewEntityHierarchyGeneratedCore(generatedHierarchy);
        }
        private void OnNewEntityHierarchyGeneratedCore(ShortGuid[] generatedHierarchy)
        {
            Singleton.OnEntityAddPending?.Invoke();

            Entity ent = null;
            switch (dialog_hierarchy_entvar)
            {
                case EntityVariant.PROXY:
                    List<ShortGuid> hierarchy = new List<ShortGuid>();
                    hierarchy.Add(Content.Level.Commands.EntryPoints[0].shortGUID);
                    hierarchy.AddRange(generatedHierarchy);
                    ent = _composite.AddProxy(Content.Level.Commands, hierarchy.ToArray());
                    (Composite pointedComp, Entity pointedEnt) = Content.Level.Commands.Utils.GetResolvedTarget(Content.Level.Commands.Utils.ResolveProxy((ProxyEntity)ent));
                    Content.Level.Commands.Utils.SetEntityName(_composite, ent, Content.Level.Commands.Utils.GetEntityName(pointedComp, pointedEnt) + " Proxy");
                    if (dialog_hierarchy != null)
                    {
                        SettingsManager.SetStringArray(
                            Settings.PreviouslySelectedProxyHierarchy,
                            dialog_hierarchy.CurrentPathEntities.Select(g => g.ToByteString()).ToArray());
                        SettingsManager.SetString(Settings.PreviouslySearchedProxyEntity, dialog_hierarchy.CurrentEntitySearch ?? "");
                    }
                    break;
                case EntityVariant.ALIAS:
                    ent = _composite.AddAlias(generatedHierarchy); 
                    break;
            }

            if (ent != null)
                UndoStack.Current.Record(new EntityAddEdit(_composite, ent, "Add " + UndoLabels.Entity(_composite, ent)));
            Singleton.OnEntityAdded?.Invoke(ent);
        }

        ShowInstanceInfo _instanceInfoPopup = null;
        private void instanceInfo_Click(object sender, EventArgs e)
        {
            if (_instanceInfoPopup != null)
            {
                _instanceInfoPopup.BringToFront();
                _instanceInfoPopup.Focus();
                return;
            }

            _instanceInfoPopup = new ShowInstanceInfo(this);
            _instanceInfoPopup.Show();
            _instanceInfoPopup.FormClosed += _instanceInfoPopup_FormClosed;
        }
        private void _instanceInfoPopup_FormClosed(object sender, FormClosedEventArgs e)
        {
            _instanceInfoPopup = null;
        }

        RenameComposite _renameComposite;
        private void renameComposite_Click(object sender, EventArgs e)
        {
            if (_renameComposite != null)
                _renameComposite.Close();

            _renameComposite = new RenameComposite(_composite);
            _renameComposite.Show();
            _renameComposite.FormClosed += _renameComposite_FormClosed;
        }
        private void _renameComposite_FormClosed(object sender, FormClosedEventArgs e)
        {
            _renameComposite = null;
        }

        private void createFlowgraph_Click(object sender, EventArgs e)
        {
            CreateFlowgraph();
        }
        RenameGeneric _createFlowgraphPopup;
        public void CreateFlowgraph()
        {
            if (_createFlowgraphPopup != null)
                _createFlowgraphPopup.Close();

            _createFlowgraphPopup = new RenameGeneric("", new RenameGeneric.RenameGenericContent()
            {
                Title = "Create new flowgraph for " + _composite.name,
                Description = "New Flowgraph Name",
                ButtonText = "Create Flowgraph"
            });
            _createFlowgraphPopup.Show();
            _createFlowgraphPopup.OnRenamed += OnCreateFlowgraph;
            _createFlowgraphPopup.FormClosed += _createFlowgraphPopup_FormClosed;
        }
        private void OnCreateFlowgraph(string name)
        {
            List<FlowgraphMeta> layouts = FlowgraphLayoutManager.GetLayouts(_composite);
            for (int i = 0; i < layouts.Count; i++)
            {
                if (layouts[i].Name ==  name)
                {
                    MessageBox.Show("Cannot create new flowgraph named '" + name + "', as there is already a flowgraph with that name in this Composite! Please pick a unique name.", "Name taken!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            FlowgraphMeta meta = FlowgraphLayoutManager.SaveLayout(null, _composite, name);
            CreateFlowgraphWindow(meta);
            DirtyTracker.MarkLevelDataModified(); //flowgraph layouts are saved with the level
            UndoStack.Current.Record(new PagePresenceEdit(_composite, meta, true, "Add page " + name));
        }
        private void _createFlowgraphPopup_FormClosed(object sender, FormClosedEventArgs e)
        {
            _createFlowgraphPopup = null;
        }

        internal Flowgraph CreateFlowgraphWindow(FlowgraphMeta meta, bool activate = true)
        {
            Flowgraph flowgraph = new Flowgraph(Content.Level.Commands);
            _flowgraphs.Add(flowgraph);
            //A page the user deletes closes itself; it must not linger here to be compiled or saved again
            flowgraph.FormClosed += (sender, e) => _flowgraphs.Remove(flowgraph);
            flowgraph.DockHandler.Show(dockPanel, DockState.Document, activate);
            flowgraph.ShowFlowgraph(Composite, meta);
            AttachLiveLinkActivity(flowgraph);
            return flowgraph;
        }

        public void SelectEntityOnFlowgraph(string flowgraph, Entity entity)
        {
            Flowgraph fg = _flowgraphs.FirstOrDefault(o => o.FlowgraphName == flowgraph);
            if (fg == null) return;
            fg.Show();
            fg.SelectAllNodesForEntity(entity);
        }
    }
}
