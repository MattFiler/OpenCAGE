using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CathodeLib.ObjectExtensions;
using OpenCAGE.Popups.UserControls;
using OpenCAGE;
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
        private const int CurrentCompositeDisplayDockLayoutVersion = 1;
        private const float DefaultLevelViewerDockTopPortion = 0.35f;

        private CompositeBrowser _compositeBrowser;
        public CompositeBrowser CompositeBrowser => _compositeBrowser;
        public LevelContent Content => _compositeBrowser.Content;

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
        private bool _canExportChildren = true;
        private bool _isSubbed = false;
        private bool _innerDockLayoutRestored = false;
        private bool _suppressLevelViewerLayoutSave;
        private double _lastSavedInnerDockTopPortion = -1d;
        private int _lastInnerDockAreaHeight;
        private int _lastInnerClientHeight;
        private System.Windows.Forms.Panel _pathHeaderPanel = null;

        //TODO: if the composite is modified, store the modification info in CompositeUtils.SetModificationInfo -> need to add the concept of "modifying" the composite first though, which should be done off of events when deleting/adding stuff (can also show this state in the UI)

        public CompositeDisplay(CompositeBrowser compositeBrowser, EntityInspector entityInspector, EntityList entityList, LevelViewerPanel levelViewerPanel)
        {
            _compositeBrowser = compositeBrowser;
            _entityDisplay = entityInspector;
            _entityDisplay.AttachCompositeDisplay(this);
            _entityList = entityList;
            _levelViewerPanel = levelViewerPanel;

            InitializeComponent();

            SetupCompositeDisplayLayout();

            dockPanel.ShowDocumentIcon = false; //todo: tabs should be smaller
            dockPanel.DocumentTabStripLocation = DocumentTabStripLocation.Bottom;

            this.FormClosed += CompositeDisplay_FormClosed;
            this.DockStateChanged += CompositeDisplay_DockStateChanged;
            this.Resize += CompositeDisplay_Resize;
            this.Shown += CompositeDisplay_Shown;

            if (_levelViewerPanel != null)
            {
                _levelViewerPanel.DockStateChanged += LevelViewerPanel_DockStateChanged;
                _levelViewerPanel.Resize += LevelViewerPanel_Resize;
            }

            dockPanel.Layout += InnerDockPanel_Layout;

            pathBreadcrumb.SegmentClicked += LoadPathSegment;

            Singleton.OnCompositeDisplayOpening?.Invoke(this);
        }

        protected override string GetPersistString()
        {
            return "CompositeDisplay";
        }

        public void ResetPortions()
        {
            SettingsManager.SetInteger(Settings.CompositeDisplayDockLayoutVersion, 0);
            SettingsManager.SetString(Settings.LevelViewerPanelDockState, DockState.Hidden.ToString());
            _innerDockLayoutRestored = false;
            ApplyDefaultInnerDockLayout();
        }

        public void EnsureInnerDockLayoutRestored()
        {
            if (_innerDockLayoutRestored)
                return;

            _innerDockLayoutRestored = true;

            if (HasSavedInnerDockLayout())
            {
                ApplySavedDockTopPortion();
                if (SettingsManager.IsSet(Settings.LevelViewerPanelDockState))
                    RestoreLevelViewerVisibility();
            }
            else
            {
                ApplyDefaultInnerDockLayout();
            }
        }

        public void SaveInnerDockLayout()
        {
            if (dockPanel == null)
                return;

            try
            {
                SettingsManager.SetInteger(
                    Settings.CompositeDisplayDockLayoutVersion,
                    CurrentCompositeDisplayDockLayoutVersion);

                if (ShouldPersistDockTopPortion())
                {
                    double storedPortion = ToStoredDockTopPortion(dockPanel.DockTopPortion);
                    SettingsManager.SetFloat(
                        Settings.CompositeDisplayDockTopPortion,
                        (float)storedPortion);
                    _lastSavedInnerDockTopPortion = storedPortion;
                }

                string dockState = _levelViewerPanel != null
                    && _levelViewerPanel.DockState != DockState.Unknown
                    && _levelViewerPanel.DockState != DockState.Hidden
                    ? _levelViewerPanel.DockState.ToString()
                    : DockState.Hidden.ToString();
                SettingsManager.SetString(Settings.LevelViewerPanelDockState, dockState);
            }
            catch
            {
            }
        }

        private bool HasSavedInnerDockLayout()
        {
            return SettingsManager.GetInteger(Settings.CompositeDisplayDockLayoutVersion, 0)
                >= CurrentCompositeDisplayDockLayoutVersion
                && SettingsManager.IsSet(Settings.CompositeDisplayDockTopPortion);
        }

        private float GetSavedDockTopPortion()
        {
            float saved = SettingsManager.GetFloat(
                Settings.CompositeDisplayDockTopPortion,
                DefaultLevelViewerDockTopPortion);
            if (saved <= 1f)
                return saved;

            System.Drawing.Rectangle area = dockPanel?.DockArea ?? System.Drawing.Rectangle.Empty;
            double areaHeight = area.Height > 0 ? area.Height : Height;
            if (areaHeight <= 0)
                return DefaultLevelViewerDockTopPortion;

            return (float)Math.Max(0.05, Math.Min(0.95, saved / areaHeight));
        }

        private double ToStoredDockTopPortion(double portion)
        {
            if (portion <= 1.0)
                return portion;

            System.Drawing.Rectangle area = dockPanel?.DockArea ?? System.Drawing.Rectangle.Empty;
            double areaHeight = area.Height > 0 ? area.Height : Height;
            if (areaHeight <= 0)
                return DefaultLevelViewerDockTopPortion;

            return Math.Max(0.05, Math.Min(0.95, portion / areaHeight));
        }

        private void ApplySavedDockTopPortion()
        {
            if (dockPanel == null)
                return;

            float portion = GetSavedDockTopPortion();
            if (Math.Abs(dockPanel.DockTopPortion - portion) < double.Epsilon)
            {
                _lastSavedInnerDockTopPortion = portion;
                UpdateInnerDockAreaCache();
                return;
            }

            dockPanel.DockTopPortion = portion;
            _lastSavedInnerDockTopPortion = portion;
            UpdateInnerDockAreaCache();
        }

        private void ConvertInnerDockPixelPortionBeforeResize()
        {
            if (dockPanel == null)
                return;

            double heightBasis = _lastInnerDockAreaHeight > 0
                ? _lastInnerDockAreaHeight
                : dockPanel.DockArea.Height > 0
                    ? dockPanel.DockArea.Height
                    : ClientSize.Height;

            NormalizeInnerDockTopPortionToRatio(heightBasis);
        }

        private void NormalizeInnerDockTopPortionToRatio(double? heightBasis = null)
        {
            if (dockPanel == null || dockPanel.DockTopPortion <= 1.0)
                return;

            System.Drawing.Rectangle area = dockPanel.DockArea;
            double height = heightBasis
                ?? (area.Height > 0 ? area.Height : ClientSize.Height);
            if (height <= 0)
                return;

            dockPanel.DockTopPortion = Math.Max(
                0.05,
                Math.Min(0.95, dockPanel.DockTopPortion / height));
        }

        private void UpdateInnerDockAreaCache()
        {
            if (dockPanel == null)
                return;

            System.Drawing.Rectangle area = dockPanel.DockArea;
            if (area.Height > 0)
                _lastInnerDockAreaHeight = area.Height;
        }

        private void UpdateInnerClientSizeCache()
        {
            _lastInnerClientHeight = ClientSize.Height;
        }

        private void CompositeDisplay_Shown(object sender, EventArgs e)
        {
            NormalizeInnerDockTopPortionToRatio();
            UpdateInnerDockAreaCache();
            UpdateInnerClientSizeCache();
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
            NormalizeInnerDockTopPortionToRatio();
            UpdateInnerDockAreaCache();
            SaveInnerDockLayoutIfPortionChanged();
        }

        private void InnerDockPanel_Layout(object sender, LayoutEventArgs e)
        {
            SaveInnerDockLayoutIfPortionChanged();
        }

        private void CompositeDisplay_Resize(object sender, EventArgs e)
        {
            if (DockState == DockState.Hidden || DockState == DockState.Unknown)
                return;

            bool clientSizeChanged = ClientSize.Height != _lastInnerClientHeight;
            if (clientSizeChanged)
                ConvertInnerDockPixelPortionBeforeResize();

            SaveInnerDockLayoutIfPortionChanged();
            UpdateInnerDockAreaCache();
            UpdateInnerClientSizeCache();
        }

        private void RestoreLevelViewerVisibility()
        {
            string savedState = SettingsManager.GetString(
                Settings.LevelViewerPanelDockState,
                DockState.Hidden.ToString());
            if (!LevelViewerPanel.IsFeatureEnabled()
                || !Enum.TryParse(savedState, out DockState dockState)
                || dockState == DockState.Unknown
                || _levelViewerPanel == null)
            {
                _levelViewerPanel?.Hide();
                return;
            }

            if (dockState == DockState.Hidden)
            {
                if (!_levelViewerPanel.IsRunning)
                {
                    _levelViewerPanel.Hide();
                    return;
                }

                dockState = DockState.DockTop;
            }

            ApplySavedDockTopPortion();
            _levelViewerPanel.Show(dockPanel, dockState);
        }

        public void ApplyDefaultInnerDockLayout()
        {
            dockPanel.DockTopPortion = DefaultLevelViewerDockTopPortion;
            _lastSavedInnerDockTopPortion = DefaultLevelViewerDockTopPortion;
            UpdateInnerDockAreaCache();
            _levelViewerPanel?.Hide();
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

                if (_levelViewerPanel.DockPanel != dockPanel || _levelViewerPanel.DockState == DockState.Hidden)
                    _levelViewerPanel.Show(dockPanel, DockState.DockTop);

                if (_levelViewerPanel.IsRunning)
                    _levelViewerPanel.RefreshEmbeddedBounds();

                SaveInnerDockLayout();
            }
            finally
            {
                ScheduleLevelViewerLayoutSaveResume();
            }

            if (activate)
                _levelViewerPanel.Activate();
        }

        public void EnsureLevelViewerDocked()
        {
            if (_levelViewerPanel == null || dockPanel == null)
                return;

            EnsureInnerDockLayoutRestored();
            ApplySavedDockTopPortion();

            if (_levelViewerPanel.DockPanel != dockPanel || _levelViewerPanel.DockState == DockState.Hidden)
            {
                _levelViewerPanel.Show(dockPanel, DockState.DockTop);
                SaveInnerDockLayout();
            }

            if (_levelViewerPanel.IsRunning)
                _levelViewerPanel.RefreshEmbeddedBounds();
        }

        public void HideLevelViewerPanelForLoad()
        {
            if (_levelViewerPanel == null || _levelViewerPanel.DockState == DockState.Hidden)
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
            _pathHeaderPanel.Controls.Add(pathBreadcrumb);
            _pathHeaderPanel.Controls.Add(instanceInfo);
            _pathHeaderPanel.Resize += PathHeaderPanel_Resize;

            toolStrip1.Dock = DockStyle.Top;
            dockPanel.Dock = DockStyle.Fill;

            Controls.Add(dockPanel);
            Controls.Add(toolStrip1);
            Controls.Add(_pathHeaderPanel);

            PathHeaderPanel_Resize(_pathHeaderPanel, EventArgs.Empty);
        }

        private void PathHeaderPanel_Resize(object sender, EventArgs e)
        {
            if (_pathHeaderPanel == null)
                return;

            int rowHeight = _pathHeaderPanel.ClientSize.Height;
            int controlHeight = Math.Min(22, rowHeight);
            int y = Math.Max(0, (rowHeight - controlHeight) / 2);
            int infoWidth = instanceInfo.Width;

            instanceInfo.SetBounds(
                Math.Max(0, _pathHeaderPanel.ClientSize.Width - infoWidth),
                y,
                infoWidth,
                controlHeight);
            pathBreadcrumb.SetBounds(
                0,
                y,
                Math.Max(0, _pathHeaderPanel.ClientSize.Width - infoWidth - 2),
                controlHeight);
        }

        private void OnCompositeRenamed(Composite composite, string name)
        {
            if (!Populated || (!Path.AllComposites.Contains(composite) && composite != _composite)) return;
            this.Text = EditorUtils.GetCompositeName(_composite);
            _entityList.UpdateTitle();
            UpdatePathBreadcrumb();
        }

        private void OnCompsoiteDeleted(Composite composite)
        {
            if (!Populated)
                return;

            while (Path.AllComposites.Contains(composite) || _composite == composite)
                LoadParent();
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

        /* Call this to show the CompositeDisplay with the requested Composite content */
        public void PopulateUI(Composite composite)
        {
            Debug.Log("Composite Display", "PopulateUI called for " + composite.shortGUID.ToByteString() + " (" + composite.name + ")");

            EnsureInnerDockLayoutRestored();

            //If we're changing composite, we should store the flowgraph layouts from the previous one
            SaveAllFlowgraphs();

            if (!_isSubbed)
            {
                _entityList.List.SelectedEntityChanged += OnEntityListSelectionChanged;
                Singleton.OnCompositeRenamed += OnCompositeRenamed;
                Singleton.OnCompositeDeleted += OnCompsoiteDeleted;
                Singleton.OnEntityAdded += ReloadUIForNewEntity;
                Singleton.OnEntityDeleted += ReloadUIForDeletedEntity;
                _isSubbed = true;
            }

            EditorUtils.CompositeType type = Content.EditorUtils.GetCompositeType(composite);
            
            switch (type)
            {
                case EditorUtils.CompositeType.IS_ROOT:
                    this.Icon = Properties.Resources.globe;
                    break;
                case EditorUtils.CompositeType.IS_GLOBAL:
                case EditorUtils.CompositeType.IS_PAUSE_MENU:
                    this.Icon = Properties.Resources.cog;
                    break;
                case EditorUtils.CompositeType.IS_DISPLAY_MODEL:
                    this.Icon = Properties.Resources.Avatar_Icon;
                    break;
                case EditorUtils.CompositeType.IS_GENERIC_COMPOSITE:
                    this.Icon = Properties.Resources.d_Prefab_Icon;
                    break;
            }

            _entityList.List.Setup(composite, new CompositeEntityList.DisplayOptions() { ShowCheckboxes = true }, false);
            _path.Reset();
            this.Text = EditorUtils.GetCompositeName(composite);

            Reload(composite);
            Singleton.OnCompositeSelected?.Invoke(_composite);
        }

        /* Call this to hide the CompositeDisplay */
        public void DepopulateUI()
        {
            this.Hide();
            CompositeDisplay_FormClosed(null, null);
        }

        private void CompositeDisplay_FormClosed(object sender, FormClosedEventArgs e)
        {
            SaveInnerDockLayout();
            _entityList.List.SelectedEntityChanged -= OnEntityListSelectionChanged;
            //this.FormClosed -= CompositeDisplay_FormClosed;
            Singleton.OnCompositeRenamed -= OnCompositeRenamed;
            Singleton.OnEntityAdded -= ReloadUIForNewEntity;
            Singleton.OnEntityDeleted -= ReloadUIForDeletedEntity;
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

            for (int i = 0; i < _flowgraphs.Count; i++)
            {
                if (_flowgraphs[i] != null)
                    _flowgraphs[i].Close();
            }
            _flowgraphs.Clear();

            if (_renameComposite != null)
                _renameComposite.FormClosed -= _renameComposite_FormClosed;
            if (_createFlowgraphPopup != null)
                _createFlowgraphPopup.FormClosed -= _createFlowgraphPopup_FormClosed;
            if (_instanceInfoPopup != null)
                _instanceInfoPopup.FormClosed -= _instanceInfoPopup_FormClosed;

            _composite = null;

            if (_entityDisplay != null)
                _entityDisplay.DepopulateUI();

            CloseAllChildTabs();

            imageList.Images.Clear();
            imageList.Dispose();
            entityListIcons.Images.Clear();
            entityListIcons.Dispose();

            vS2015DarkTheme1.Dispose();
            vS2015BlueTheme1.Dispose();
        }

        private void Reload(Composite composite)
        {
            Debug.Log("Composite Display", "Private Reload called for " + composite.shortGUID.ToByteString() + " (" + composite.name + ")");
            Cursor.Current = Cursors.WaitCursor;

            //No need to find uses of entry point - it's the entry point
            findUses.Visible = Content.Level.Commands.EntryPoints[0] != composite;

            //Shouldn't be able to delete the root/PAUSEMENU/GLOBAL else it'll break stuff
            deleteComposite.Visible = !Content.Level.Commands.EntryPoints.Contains(composite);
            //Similarly, shouldn't be able to rename PAUSEMENU/GLOBAL as their names are used in code
            renameComposite.Visible = Content.Level.Commands.EntryPoints[1] != composite && Content.Level.Commands.EntryPoints[2] != composite;

            _composite = composite;
            this.Text = EditorUtils.GetCompositeName(composite);
            _entityList.UpdateTitle();
            UpdatePathBreadcrumb();

            //Remove dead links and empty aliases on first time
            if (!Content.Level.Commands.Utils.PurgedComposites.purged.Contains(_composite.shortGUID))
            {
                //Clear out any dead links
                Content.Level.Commands.Utils.PurgeDeadLinks(_composite);
                Content.Level.Commands.Utils.PurgedComposites.purged.Add(_composite.shortGUID);
            }

            ClearEntitySelection();
            CloseAllChildTabs();
            Reload(false);
            if (!ViewerSelectionSync.IsApplyingViewerSelection)
                this.Activate();

            _instanceInfoPopup?.Close();

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

                _path.StepForwards(current, entity);
                current = childComposite;
            }

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
            _path.Reset();

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

                _path.StepForwards(current, entity);
                current = childComposite;
            }

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

            Composite entryComposite = resolved[0].Item1;
            uint[] pathEntityGuids = new uint[resolved.Count];
            for (int i = 0; i < resolved.Count; i++)
                pathEntityGuids[i] = resolved[i].Item2.shortGUID.AsUInt32;

            return ApplyViewerSelectionPath(
                entryComposite,
                pathEntityGuids,
                selectLeafEntity: true,
                entity => IsCompositeInstance(entity, commands)
                    ? commands.GetComposite(((FunctionEntity)entity).function)
                    : null);
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

        /* Load the parent composite, one back from this composite */
        public void LoadParent()
        {
            if (_path.StepBackwards(out Composite composite, out Entity entity))
            {
                ClearEntitySelection();
                Reload(composite);
                SelectEntityAfterNavigationReload(entity, deferFlowgraphFocus: true);
            }
        }

        /* Jump to a composite segment in the breadcrumb path. */
        public void LoadPathSegment(int segmentIndex)
        {
            if (!_path.TryNavigateToCompositeIndex(_composite, segmentIndex, out Composite composite, out Entity entity))
                return;

            ClearEntitySelection();
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

        private void UpdatePathBreadcrumb()
        {
            if (!Populated)
                return;

            pathBreadcrumb.SetPath(_path, _composite);
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
                for (int i = 0; i < _flowgraphs.Count; i++)
                    if (_flowgraphs[i] != null)
                        _flowgraphs[i].Close();
                _flowgraphs.Clear();

                //If we support flowgraphs, load them
                Debug.Log("Composite Display", "Flowgraphs " + (SupportsFlowgraphs ? "Supported!" : "Not supported!"));
                if (SupportsFlowgraphs)
                {
                    List<FlowgraphMeta> layouts = FlowgraphLayoutManager.GetLayouts(Composite);
                    Debug.Log("Composite Display", "Found " + layouts.Count + " flowgraph layout(s)");
                    for (int i = 0; i < layouts.Count; i++)
                        CreateFlowgraphWindow(layouts[i]);

                    string prevLoaded = FlowgraphLayoutManager.GetSelectedPage(Composite);
                    if (prevLoaded != null)
                        _flowgraphs.FirstOrDefault(o => o.FlowgraphName == prevLoaded)?.Show();
                }
            }
            finally
            {
                dockPanel.ResumeLayout(true, true);
            }
            createFlowgraph.Visible = SupportsFlowgraphs;

            exportComposite.Enabled = false;
            Task.Factory.StartNew(() => UpdateExportCompositeVisibility());

            Singleton.OnCompositeReloaded?.Invoke(_composite);
        }

        /* Work out if we can export this composite: for now, we can't export composites that contain any resources, as the resource pointers would be wrong */
        private void UpdateExportCompositeVisibility()
        {
            try
            {
                _canExportChildren = true;
                bool visible = !DoesCompositeContainResource(_composite);
                EnableDisableButtonRun(visible);
            }
            catch { }
        }
        delegate void EnableDisableButtonRunDeleg(bool value);
        private void EnableDisableButtonRun(bool value)
        {
            if (toolStrip1.InvokeRequired)
            {
                this.toolStrip1.Invoke(new EnableDisableButtonRunDeleg
                 (EnableDisableButtonRun), value);
            }
            else
            {
                exportComposite.Enabled = value;
            }
        }
        private bool DoesCompositeContainResource(Composite comp)
        {
            List<ResourceType> allowedTypes = new List<ResourceType>();
            allowedTypes.Add(ResourceType.ANIMATED_MODEL);
            allowedTypes.Add(ResourceType.RENDERABLE_INSTANCE);
            allowedTypes.Add(ResourceType.COLLISION_MAPPING);

            //note - only unsupported now is DNYAMIC_PHYSICS_SYSTEM

            bool found = false;
            Parallel.ForEach(comp.functions, (ent, state) =>
            {
                if (_canExportChildren && !ent.function.IsFunctionType)
                {
                    Composite nestedComp = Content.Level.Commands.GetComposite(ent.function);
                    if (nestedComp != null)
                    {
                        if (DoesCompositeContainResource(nestedComp))
                        {
                            _mut.WaitOne();
                            _canExportChildren = false;
                            _mut.ReleaseMutex();
                        }
                    }
                }

                for (int i = 0; i < ent.resources.Count; i++)
                {
                    if (!allowedTypes.Contains(ent.resources[i].resource_type))
                    {
                        found = true;
                        state.Stop();
                    }
                }

                Parameter resources = ent.GetParameter("resource");
                if (resources != null)
                {
                    List<ResourceReference> resourceRefs = ((cResource)resources.content).value;
                    for (int i = 0; i < resourceRefs.Count; i++)
                    {
                        if (!allowedTypes.Contains(resourceRefs[i].resource_type))
                        {
                            found = true;
                            state.Stop();
                        }
                    }
                }
            });
            return found;
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

            if (_entityDisplay?.Entity == deletedEntity && _entityDisplay.Populated
                && ViewerSelectionSync.SuppressSyncBroadcastDepth == 0)
                _entityDisplay.Close();

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
            if (_entityList?.List != null)
            {
                _entityList.List.SelectedEntityChanged -= OnEntityListSelectionChanged;
                _entityList.List.ClearSelection();
                _entityList.List.SelectedEntityChanged += OnEntityListSelectionChanged;
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

            LoadEntity(entity, true);
            Singleton.Editor?.EntityInspector?.Activate();
        }

        public void LoadEntityDontFocusNode(Entity entity) => LoadEntity(entity, false);
        public void LoadEntityAndFocusNode(Entity entity) => LoadEntity(entity, true);
        public void LoadEntity(Entity entity, bool focusNode)
        {
            if (entity == null) return;

#if DEBUG
            _entityDisplay.PopulateUI(entity, true); //NOTE: always showing links in debug view to make validating things easier
#else
            _entityDisplay.PopulateUI(entity, !SupportsFlowgraphs);
#endif

            if (SupportsFlowgraphs && focusNode && !ViewerSelectionSync.IsApplyingViewerSelection)
                FocusEntityOnFlowgraph(entity);

            //Make sure the entity is selected in the list view too, but don't handle the event, else we'll get called again
            if (_entityList.List.SelectedEntity == null || _entityList.List.SelectedEntity.shortGUID != entity.shortGUID)
            {
                _entityList.List.SelectedEntityChanged -= OnEntityListSelectionChanged;
                _entityList.List.SelectEntity(entity);
                _entityList.List.SelectedEntityChanged += OnEntityListSelectionChanged;
            }
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

        public bool AnyFlowgraphsContainEntity(Entity entity)
        {
            foreach (Flowgraph flowgraph in _flowgraphs)
            {
                if (FlowgraphContainsEntity(flowgraph, entity))
                    return true;
            }
            return false;
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
            if (ask && MessageBox.Show("Are you sure you want to remove this entity?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            Singleton.OnEntityDeletePending?.Invoke(entity, Composite);

            switch (entity.variant)
            {
                case EntityVariant.VARIABLE:
                    Composite.RemoveVariable(entity.shortGUID);
                    break;
                case EntityVariant.FUNCTION:
                    Composite.RemoveFunction(entity.shortGUID);
                    break;
                case EntityVariant.ALIAS:
                    Composite.RemoveAlias(entity.shortGUID);
                    break;
                case EntityVariant.PROXY:
                    Composite.RemoveProxy(entity.shortGUID);
                    break;
            }

            List<Entity> entities = Composite.GetEntities();
            for (int i = 0; i < entities.Count; i++) //We should actually query every entity in the PAK, since we might be ref'd by a proxy or alias
            {
                List<EntityConnector> entLinks = new List<EntityConnector>();
                for (int x = 0; x < entities[i].childLinks.Count; x++)
                {
                    if (entities[i].childLinks[x].linkedEntityID != entity.shortGUID) entLinks.Add(entities[i].childLinks[x]);
                }
                entities[i].childLinks = entLinks;

                if (entities[i].variant == EntityVariant.FUNCTION)
                {
                    string entType = ShortGuidUtils.FindString(((FunctionEntity)entities[i]).function);
                    switch (entType)
                    {
                        case "TriggerSequence":
                            TriggerSequence triggerSequence = (TriggerSequence)entities[i];
                            List<TriggerSequence.SequenceEntry> triggers = new List<TriggerSequence.SequenceEntry>();
                            for (int x = 0; x < triggerSequence.sequence.Count; x++)
                            {
                                if (triggerSequence.sequence[x].connectedEntity.path.Length < 2 ||
                                    triggerSequence.sequence[x].connectedEntity.path[triggerSequence.sequence[x].connectedEntity.path.Length - 2] != entity.shortGUID)
                                {
                                    triggers.Add(triggerSequence.sequence[x]);
                                }
                            }
                            triggerSequence.sequence = triggers;
                            break;
                        case "CAGEAnimation":
                            CAGEAnimation cageAnim = (CAGEAnimation)entities[i];
                            List<CAGEAnimation.Connection> headers = new List<CAGEAnimation.Connection>();
                            for (int x = 0; x < cageAnim.connections.Count; x++)
                            {
                                if (cageAnim.connections[x].connectedEntity.path.Length < 2 ||
                                    cageAnim.connections[x].connectedEntity.path[cageAnim.connections[x].connectedEntity.path.Length - 2] != entity.shortGUID)
                                {
                                    headers.Add(cageAnim.connections[x]);
                                }
                            }
                            cageAnim.connections = headers;
                            break;
                    }
                }
            }

            Content.Level.Commands.Utils.PurgedComposites.purged.Clear(); //TODO: we should smartly remove from this list, rather than removing all

            if (_entityDisplay.Entity == entity && _entityDisplay.Populated
                && ViewerSelectionSync.SuppressSyncBroadcastDepth == 0)
                _entityDisplay.Close();

            RemoveEntityFromList(entity);

            Singleton.OnEntityDeleted?.Invoke(entity);
        }

        public void DuplicateEntity(Entity entity)
        {
            if (MessageBox.Show("Are you sure you want to duplicate this entity?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            AddCopyOfEntity(entity);
        }

        public Entity AddCopyOfEntity(Entity entity)
        {
            Singleton.OnEntityAddPending?.Invoke();
            Entity newEnt = MakeCopyOfEntity(entity);
            switch (newEnt.variant)
            {
                case EntityVariant.FUNCTION:
                    Composite.functions_dictionary.Add(((FunctionEntity)newEnt).shortGUID, (FunctionEntity)newEnt);
                    break;
                case EntityVariant.VARIABLE:
                    Composite.variables_dictionary.Add(((VariableEntity)newEnt).shortGUID, (VariableEntity)newEnt);
                    break;
                case EntityVariant.PROXY:
                    Composite.proxies_dictionary.Add(((ProxyEntity)newEnt).shortGUID, (ProxyEntity)newEnt);
                    break;
                case EntityVariant.ALIAS:
                    Composite.aliases_dictionary.Add(((AliasEntity)newEnt).shortGUID, (AliasEntity)newEnt);
                    break;
            }
            Content.EditorUtils.GenerateCompositeInstances(Content.Level.Commands);
            Singleton.OnEntityAdded?.Invoke(newEnt);

            return newEnt;
        }

        private Entity MakeCopyOfEntity(Entity entity)
        {
            //Generate new entity ID and name
            Entity newEnt = null;
            switch (entity.variant)
            {
                case EntityVariant.FUNCTION:
                    newEnt = ((FunctionEntity)entity).Copy();
                    break;
                case EntityVariant.VARIABLE:
                    newEnt = ((VariableEntity)entity).Copy();
                    break;
                case EntityVariant.ALIAS:
                    newEnt = ((AliasEntity)entity).Copy();
                    break;
                case EntityVariant.PROXY:
                    newEnt = ((ProxyEntity)entity).Copy();
                    break;
            }
            newEnt.shortGUID = ShortGuidUtils.GenerateRandom();
            if (newEnt.variant != EntityVariant.VARIABLE)
            {
                Content.Level.Commands.Utils.SetEntityName(
                        Composite.shortGUID,
                        newEnt.shortGUID,
                        Content.Level.Commands.Utils.GetEntityName(Composite.shortGUID, entity.shortGUID) + "_clone");

                //TODO: not using the below, because really we should check every entity's name to get the index to append.
                /*
                string name = EntityUtils.GetName(Composite.shortGUID, entity.shortGUID);
                string[] vals = name.Split('_');
                if (vals.Length > 1 && int.TryParse(vals[vals.Length - 1], out int index))
                {
                    index += 1;
                    name = name.Substring(0, name.Length - vals[vals.Length - 1].Length) + index;
                }
                EntityUtils.SetName(
                    Composite.shortGUID,
                    newEnt.shortGUID,
                    name);
                */
            }

            //Add parent links in to this entity that linked in to the other entity
            List<Entity> ents = Composite.GetEntities();
            List<EntityConnector> newLinks = new List<EntityConnector>();
            int num_of_new_things = 1;
            foreach (Entity ent in ents)
            {
                newLinks.Clear();
                foreach (EntityConnector link in ent.childLinks)
                {
                    if (link.linkedEntityID == entity.shortGUID)
                    {
                        EntityConnector newLink = new EntityConnector();
                        newLink.ID = ShortGuidUtils.Generate(DateTime.Now.ToString("G") + num_of_new_things.ToString()); num_of_new_things++;
                        newLink.linkedEntityID = newEnt.shortGUID;
                        newLink.linkedParamID = link.linkedParamID;
                        newLink.thisParamID = link.thisParamID;
                        newLinks.Add(newLink);
                    }
                }
                if (newLinks.Count != 0) ent.childLinks.AddRange(newLinks);
            }

            //TODO should update entity ID on collision_map resource

#if DEBUG
            //If entity is a composite instance, check to see if it should make a new PHYSICS.MAP entry
            if (entity.variant == EntityVariant.FUNCTION)
            {
                Composite comp = Content.Level.Commands.GetComposite(((FunctionEntity)entity).function);

                //TODO: need to recurse into all child composite instances to find ALL contained PhysicsSystem functions, rather than just the layer below
                FunctionEntity phys = comp?.functions.FirstOrDefault(o => o.function == FunctionType.PhysicsSystem);
                if (phys != null)
                {
                    List<ShortGuid> instancesEnt = new List<ShortGuid>();
                    List<EntityPath> pathsEnt = Content.EditorUtils.GetHierarchiesForEntity(Composite, entity);
                    List<ShortGuid> instancesPhys = new List<ShortGuid>();
                    List<EntityPath> pathsPhys = new List<EntityPath>();
                    pathsEnt.ForEach(path => {
                        instancesEnt.Add(path.GenerateCompositeInstanceID());

                        EntityPath pathPhys = path.Copy();
                        path.AddNextStep(phys.shortGUID);
                        pathsPhys.Add(pathPhys);

                        instancesPhys.Add(pathPhys.GenerateCompositeInstanceID());
                    });

                    List<PhysicsMaps.DYNAMIC_PHYSICS_SYSTEM> physMaps = Content.Level.PhysicsMaps.Entries.FindAll(physMap =>
                        instancesPhys.Contains(physMap.composite_instance_id) &&
                        physMap.entity.entity_id == entity.shortGUID &&
                        instancesEnt.Contains(physMap.entity.composite_instance_id)
                    );
                    physMaps.ForEach(physMap =>
                    {
                        PhysicsMaps.DYNAMIC_PHYSICS_SYSTEM newPhysMap = physMap.Copy();
                        newPhysMap.entity.entity_id = newEnt.shortGUID;

                        EntityPath pathPhys = pathsPhys.FirstOrDefault(x => x.GenerateCompositeInstanceID() == physMap.composite_instance_id);
                        EntityPath newPathPhys = pathPhys.Copy();
                        newPathPhys.path[newPathPhys.path.Length - 3] = newEnt.shortGUID;
                        newPhysMap.composite_instance_id = newPathPhys.GenerateCompositeInstanceID();
                        Content.Level.PhysicsMaps.Entries.Add(newPhysMap);
                        //Content.Level.PhysicsMaps.Entries[Content.Level.PhysicsMaps.Entries.IndexOf(physMap)] = newPhysMap;

                        //TODO: need to set pos/rot properly

                        Resources.Resource physRes = Content.Level.Resources.Entries.FirstOrDefault(res => res.composite_instance_id == physMap.composite_instance_id);
                        Resources.Resource newPhysRes = physRes.Copy();
                        newPhysRes.composite_instance_id = newPhysMap.composite_instance_id;
                        //newPhysRes.index = Content.Level.Resources.Entries.Count;
                        Content.Level.Resources.Entries.Add(newPhysRes);
                        //Content.Level.Resources.Entries[Content.Level.Resources.Entries.IndexOf(p)] = resPhys;
                    });
                }
            }
#endif

            return newEnt;
        }

        public void DeleteCheckedEntities()
        {
            if (!Populated || _entityList?.List == null)
                return;

            List<Entity> entities = _entityList.List.CheckedEntities;
            if (entities.Count == 0)
                return;

            if (MessageBox.Show("Are you sure you want to remove the selected entities?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            foreach (Entity entity in entities)
                DeleteEntity(entity, false, false);

            _entityList.List.LoadComposite(Composite);
            ReloadAllEntities();
        }

        private void exportComposite_Click(object sender, EventArgs e)
        {
            ExportComposite dialog = new ExportComposite(Composite, _canExportChildren);
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
                            ShowApplyDefaults = true,
                        });
                        dialog_hierarchy.Text = "Create Proxy";
                        break;
                    case EntityVariant.ALIAS:
                        dialog_hierarchy = new SelectHierarchy(_composite, new CompositeEntityList.DisplayOptions()
                        {
                            DisplayAliases = false,
                            DisplayFunctions = true,
                            DisplayProxies = true,
                            DisplayVariables = true,
                            ShowApplyDefaults = true,
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

        public Entity CreateCompositeInstanceEntity(string compositeName, PointF? flowgraphPosition = null)
        {
            if (string.IsNullOrWhiteSpace(compositeName))
                return null;

            return CreateCompositeInstanceEntity(Content.Level.Commands.GetComposite(compositeName), flowgraphPosition);
        }

        public Entity CreateCompositeInstanceEntity(Composite instanceComposite, PointF? flowgraphPosition = null)
        {
            if (!Populated || instanceComposite == null)
                return null;

            if (instanceComposite == Composite)
            {
                MessageBox.Show(
                    "You cannot create an entity which instances the composite it is contained within - this will result in an infinite loop at runtime! Please check your logic!.",
                    "Logic error!",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return null;
            }

            Singleton.OnEntityAddPending?.Invoke();

            Entity newEntity = Composite.AddFunction(instanceComposite);

            string baseName = System.IO.Path.GetFileName(instanceComposite.name.Replace('\\', '/'));
            int instanceCount = Composite.functions.Count(o => o.function == instanceComposite.shortGUID);
            string entityName = instanceCount > 1 ? baseName + "_" + instanceCount : baseName;
            Content.Level.Commands.Utils.SetEntityName(Composite, newEntity, entityName);

            if (SettingsManager.GetBool(Settings.PreviouslySearchedParamPopulationComp, false))
            {
                Content.Level.Commands.Utils.AddAllDefaultParameters(newEntity, Composite);
                newEntity.RemoveParameter("delete_me");
            }

            Content.EditorUtils.GenerateCompositeInstances(Content.Level.Commands);
            SettingsManager.SetString(Settings.PreviouslySelectedCompInstType, instanceComposite.name);

            Singleton.OnEntityAdded?.Invoke(newEntity);

            if (flowgraphPosition.HasValue)
                PlaceEntityOnFlowgraph(newEntity, flowgraphPosition.Value);

            return newEntity;
        }

        public Entity CreateFunctionEntity(FunctionType function, PointF? flowgraphPosition = null)
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

            int count = Composite.functions.Count(o => o.function == function) + 1;
            string entityName = function.ToString() + "_" + count;

            Singleton.OnEntityAddPending?.Invoke();
            Entity newEntity = Composite.AddFunction(function);
            Content.Level.Commands.Utils.SetEntityName(Composite, newEntity, entityName);
            SettingsManager.SetString(Settings.PreviouslySelectedFunctionType, function.ToString());

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
                    break;
                case EntityVariant.ALIAS:
                    ent = _composite.AddAlias(generatedHierarchy); 
                    break;
            }

            if (dialog_hierarchy.ApplyDefaultParams)
                Content.Level.Commands.Utils.AddAllDefaultParameters(ent, _composite);

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

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EditorClipboard.Entity = _entityList.List.SelectedEntity;
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AddCopyOfEntity(EditorClipboard.Entity);
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
        }
        private void _createFlowgraphPopup_FormClosed(object sender, FormClosedEventArgs e)
        {
            _createFlowgraphPopup = null;
        }

        private void CreateFlowgraphWindow(FlowgraphMeta meta)
        {
            Flowgraph flowgraph = new Flowgraph(Content.Level.Commands);
            _flowgraphs.Add(flowgraph);
            flowgraph.Show(dockPanel, DockState.Document);
            flowgraph.ShowFlowgraph(Composite, meta);
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
