using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.Popups;
using OpenCAGE;
using OpenCAGE.UnityConnection;
using OpenCAGE.UserControls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using WebSocketSharp;
using WeifenLuo.WinFormsUI.Docking;
using ListViewItem = System.Windows.Forms.ListViewItem;

namespace OpenCAGE.DockPanels
{
    public partial class CompositeBrowser : DockContent
    {
        public const string CompositeDragFormat = "OpenCAGE.CompositeInstance";

        private LevelContent _content;
        public LevelContent Content => _content;

        //Set when the level has been handed to a replacement browser, so closing this one doesn't take
        //the level down with it
        private bool _contentHandedOver = false;

        /// <summary>
        /// Give up ownership of the loaded level so another browser can adopt it. After this the browser
        /// is finished with - close it.
        /// </summary>
        public LevelContent DetachContent()
        {
            _contentHandedOver = true;

            LevelContent content = _content;
            _content = null;
            return content;
        }

        private TreeUtility _treeUtility = null;
        private CancellationTokenSource _prevTaskToken = null;

        private string _currentDisplayFolderPath = "";

        public CompositeDisplay CompositeDisplay => Singleton.Editor?.CompositeDisplay;

        AddComposite _addCompositeDialog = null;
        AddFolder _addFolderDialog = null;

        private const int MinTreePanelSize = 100;
        private const int DefaultTreePanelSize = 160;

        //The large-icon list with composite previews in it, derived from the stock large icons on first use
        private const int BrowserPreviewSize = 96;
        private ImageList _previewImageList = null;
        private int _defaultSplitterDistance = DefaultTreePanelSize;
        private Panel _treeSearchPanel = null;

        //What sits under the tree - nothing, the folder browser or the flat preview list - as last applied
        private CompositeBrowserMode _mode = CompositeBrowserModes.Default;
        private Panel _pathPanel = null;
        private CompositePathBreadcrumb _pathBreadcrumb = null;

        /* The flat list gets its previews as they come into view rather than all at once. A level has well
           over a thousand composites; decoding every preview up front would hold the window for seconds and
           keep tens of megabytes of previews for the few dozen on screen. So the list is built with stock
           icons, and this timer watches for it having moved - however it moved: wheel, scrollbar, keys, a
           selection scrolled into view - and previews what is on screen and a screen either side. */
        private System.Windows.Forms.Timer _previewFillTimer;
        private bool _previewFillPending = false;
        private Point _previewFillScroll = new Point(-1, -1);
        private Size _previewFillClientSize = Size.Empty;
        private const int PreviewFillIntervalMs = 100;
        private const int PreviewDecodesPerTick = 40;
        //How many previews the derived list may hold before it is emptied and refilled with what is on screen
        private const int MaxListPreviews = 512;

        //Set up by the shared constructor tail rather than by each constructor, so not readonly
        private System.Windows.Forms.Timer _treeSelectionDebounceTimer;
        private TreeNode _pendingTreeSelection = null;
        private Point _treeDragStartPoint;
        private bool _treeDragInProgress = false;
        //Screen point of a drop onto the level viewer, taken during the drag and acted on once it ends
        private Point? _viewportDropPoint = null;
        private bool _suppressTreeSelectionDebounce = false;
        private bool _suppressSelectionRestore = false;

        public CompositeBrowser(string levelName)
        {
            InitializeComponent();
            Theming.ThemeManager.ApplyToForm(this);

            SetupBrowserLayout();

            this.FormClosed += CompositeBrowser_FormClosed;
            this.Load += CompositeBrowser_Load;
            this.VisibleChanged += CompositeBrowser_VisibleChanged;
            this.DockStateChanged += CompositeBrowser_DockStateChanged;
            this.Resize += CompositeBrowser_Resize;

            _content = new LevelContent(levelName);
            _treeUtility = new TreeUtility(treeView1, TreeType.SCRIPTS);
            FinishConstruction();
        }

        /// <summary>
        /// Rebuild the browser around a level that is already loaded.
        ///
        /// Used when the panels have to be torn down and recreated mid-session - switching the docking
        /// theme, for instance - so that the level doesn't have to be read off disk again. The level is
        /// handed over by <see cref="DetachContent"/> on the browser being replaced.
        /// </summary>
        public CompositeBrowser(LevelContent existing)
        {
            InitializeComponent();
            Theming.ThemeManager.ApplyToForm(this);

            SetupBrowserLayout();

            this.FormClosed += CompositeBrowser_FormClosed;
            this.Load += CompositeBrowser_Load;
            this.VisibleChanged += CompositeBrowser_VisibleChanged;
            this.DockStateChanged += CompositeBrowser_DockStateChanged;
            this.Resize += CompositeBrowser_Resize;

            _content = existing;
            _treeUtility = new TreeUtility(treeView1, TreeType.SCRIPTS);
            FinishConstruction();
        }

        private void FinishConstruction()
        {

            SetupReorganiseDragDrop();
            //Package files dropped on the browser open like anywhere else (after our own handlers, which step aside for them)
            PackageDropTarget.Attach(this);

            treeView1.MouseMove += FileTree_MouseMove;

            _treeSelectionDebounceTimer = new System.Windows.Forms.Timer(components) { Interval = 200 };
            _treeSelectionDebounceTimer.Tick += TreeSelectionDebounceTimer_Tick;

            _previewFillTimer = new System.Windows.Forms.Timer(components) { Interval = PreviewFillIntervalMs };
            _previewFillTimer.Tick += PreviewFillTimer_Tick;

            Singleton.OnCompositeRenamed += OnCompositeRenamed;
            SettingsManager.SettingsChanged += OnSettingsChanged;
            CompositePreviewManager.PreviewsChanged += OnPreviewsChanged;

            ApplyBrowserMode();
        }

        private void SetupBrowserLayout()
        {
            splitContainer1.Panel1.Controls.Remove(treeView1);
            splitContainer1.Panel1.Controls.Remove(entity_search_box);
            splitContainer1.Panel1.Controls.Remove(entity_search_clear_btn);

            entity_search_box.Anchor = AnchorStyles.None;
            entity_search_clear_btn.Anchor = AnchorStyles.None;

            _treeSearchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 24,
                Name = "treeSearchPanel",
            };
            _treeSearchPanel.Controls.Add(entity_search_box);
            _treeSearchPanel.Controls.Add(entity_search_clear_btn);
            _treeSearchPanel.Resize += TreeSearchPanel_Resize;
            LayoutTreeSearchRow();

            treeView1.Anchor = AnchorStyles.None;
            treeView1.Dock = DockStyle.Fill;

            // Search sits above the tree only; tree and file list share the split below the toolbar.
            splitContainer1.Panel1.Controls.Add(treeView1);
            splitContainer1.Panel1.Controls.Add(_treeSearchPanel);

            SetupFileBrowserPathRow();

            toolStrip1.Dock = DockStyle.Top;
            splitContainer1.Anchor = AnchorStyles.None;
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            splitContainer1.Panel1MinSize = MinTreePanelSize;
            splitContainer1.Panel2MinSize = MinTreePanelSize;

            if (_treeSearchPanel.Parent == this)
                Controls.Remove(_treeSearchPanel);

            // Lower z-order (index 0) must be the Fill control so Top-docked siblings reserve space first.
            Controls.SetChildIndex(splitContainer1, 0);
            Controls.SetChildIndex(toolStrip1, 1);

            splitContainer1.Layout += SplitContainer1_Layout;
        }

        private void SetupFileBrowserPathRow()
        {
            splitContainer1.Panel2.Controls.Remove(goBackOnPath);
            splitContainer1.Panel2.Controls.Remove(listView1);

            goBackOnPath.Anchor = AnchorStyles.None;

            //The same breadcrumb as the Composite Display's top bar and the entity pickers, spelling the
            //folder being browsed as (Root) › folder › subfolder, every folder but the last a link back up to it
            _pathBreadcrumb = new CompositePathBreadcrumb
            {
                Name = "fileBrowserPathBreadcrumb",
            };
            _pathBreadcrumb.SegmentClicked += PathSegmentClicked;

            _pathPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 24,
                Name = "fileBrowserPathPanel",
            };
            _pathPanel.Controls.Add(goBackOnPath);
            _pathPanel.Controls.Add(_pathBreadcrumb);
            _pathPanel.Resize += FileBrowserPathPanel_Resize;
            FileBrowserPathPanel_Resize(_pathPanel, EventArgs.Empty);

            listView1.Anchor = AnchorStyles.None;
            listView1.Dock = DockStyle.Fill;

            splitContainer1.Panel2.Controls.Add(listView1);
            splitContainer1.Panel2.Controls.Add(_pathPanel);
        }

        private void TreeSearchPanel_Resize(object sender, EventArgs e)
        {
            LayoutTreeSearchRow();
        }

        private void FileBrowserPathPanel_Resize(object sender, EventArgs e)
        {
            if (!(sender is Panel pathPanel))
                return;

            goBackOnPath.SetBounds(0, 1, goBackOnPath.Width, 22);
            _pathBreadcrumb.SetBounds(
                goBackOnPath.Width + 2,
                1,
                Math.Max(0, pathPanel.ClientSize.Width - goBackOnPath.Width - 2),
                22);
        }

        private void LayoutTreeSearchRow()
        {
            if (_treeSearchPanel == null)
                return;

            /* The X only shows once there is something to clear, the way the other search rows do it,
             * so the box takes the whole width until then. */
            bool showClear = entity_search_box.Text.Length != 0;
            entity_search_clear_btn.Visible = showClear;

            int clearWidth = showClear ? entity_search_clear_btn.Width + 2 : 0;
            entity_search_clear_btn.SetBounds(
                Math.Max(0, _treeSearchPanel.ClientSize.Width - entity_search_clear_btn.Width),
                1,
                entity_search_clear_btn.Width,
                20);

            entity_search_box.SetBounds(
                0,
                1,
                Math.Max(0, _treeSearchPanel.ClientSize.Width - clearWidth),
                20);
        }

        private void SplitContainer1_Layout(object sender, LayoutEventArgs e)
        {
            ApplySplitterDistance();
        }

        private void CompositeBrowser_VisibleChanged(object sender, EventArgs e)
        {
            if (!Visible)
                return;

            EnsureCompositeTreePopulated();
            ApplySplitterDistance();
        }

        private void CompositeBrowser_DockStateChanged(object sender, EventArgs e)
        {
            if (DockState == DockState.Hidden || DockState == DockState.Unknown)
                return;

            BeginInvoke(new Action(() =>
            {
                EnsureCompositeTreePopulated();
                ApplySplitterDistance();
            }));
        }

        private void CompositeBrowser_Resize(object sender, EventArgs e)
        {
            ApplySplitterDistance();
        }

        private void OnCompositeRenamed(Composite composite, string name)
        {
            ReloadList();
        }

        private void ClearTreeNodeTags(TreeNode node)
        {
            if (node.Tag != null)
            {
                node.Tag = null;
            }
            foreach (TreeNode child in node.Nodes)
            {
                ClearTreeNodeTags(child);
            }
        }

        public void LoadInitialComposite()
        {
            ClearCompositeTreeSearch();
            SelectCompositeAndReloadList(Content.Level.Commands.EntryPoints[0]);
        }

        public void EnsureCompositeTreePopulated()
        {
            if (Content?.Level?.Commands == null || !Content.Level.Commands.Loaded)
                return;

            if (treeView1 == null || treeView1.IsDisposed || _treeUtility == null)
                return;

            Content.EnsureEditorUtils();
            ReloadList();
        }

        private void ClearCompositeTreeSearch()
        {
            _currentSearch = "";
            entity_search_box.Text = "";
        }

        private void CompositeBrowser_Load(object sender, EventArgs e)
        {
            if (Enum.TryParse<View>(SettingsManager.GetString(Settings.FileBrowserViewOpt), out View view))
                SetViewMode(view);

            ApplySplitterDistance();

            Task.Factory.StartNew(() => EnumStringListViewItems.PopulateGlobalEntries());

            if (Content.IsLevelDataLoaded)
                OnLevelDataReady();
        }

        public void OnLevelDataReady()
        {
            if (!Content.IsLevelDataLoaded)
                return;

            Content.EnsureEditorUtils();
            EnsureCompositeTreePopulated();

            Task.Factory.StartNew(() => Content.EditorUtils?.GenerateEntityNameCache(Singleton.Editor));
            Task.Factory.StartNew(() => EnumStringListViewItems.PopulateLevelSpecificEntries());
        }

        private void CompositeBrowser_FormClosed(object sender, FormClosedEventArgs e)
        {
            this.FormClosed -= CompositeBrowser_FormClosed;
            this.Load -= CompositeBrowser_Load;
            this.VisibleChanged -= CompositeBrowser_VisibleChanged;
            this.DockStateChanged -= CompositeBrowser_DockStateChanged;
            this.Resize -= CompositeBrowser_Resize;
            Singleton.OnCompositeRenamed -= OnCompositeRenamed;
            SettingsManager.SettingsChanged -= OnSettingsChanged;
            CompositePreviewManager.PreviewsChanged -= OnPreviewsChanged;

            _treeSelectionDebounceTimer.Stop();
            _treeSelectionDebounceTimer.Tick -= TreeSelectionDebounceTimer_Tick;
            _previewFillTimer.Stop();
            _previewFillTimer.Tick -= PreviewFillTimer_Tick;
            if (_pathBreadcrumb != null)
                _pathBreadcrumb.SegmentClicked -= PathSegmentClicked;
            if (treeView1 != null)
                treeView1.MouseMove -= FileTree_MouseMove;

            if (_renameComposite != null)
                _renameComposite.FormClosed -= _renameComposite_FormClosed;
            if (_addCompositeDialog != null)
            {
                _addCompositeDialog.FormClosed -= addCompositeDialogClosed;
                _addCompositeDialog.OnCompositeAdded -= SelectCompositeAndReloadList;
            }
            if (_addFolderDialog != null)
            {
                _addFolderDialog.FormClosed -= addFolderDialogClosed;
                _addFolderDialog.OnFolderAdded -= SelectCompositeAndReloadList;
            }
            if (listView1 != null)
            {
                foreach (ListViewItem item in listView1.Items)
                {
                    if (item.Tag != null)
                    {
                        item.Tag = null; 
                    }
                }
                listView1.Items.Clear();
            }
            
            if (treeView1 != null)
            {
                foreach (TreeNode node in treeView1.Nodes)
                {
                    ClearTreeNodeTags(node);
                }
                treeView1.Nodes.Clear();
            }

            if (!_contentHandedOver)
                _content?.Dispose();

            _content = null;

            _treeUtility?.ForceClearTree();
            _treeUtility = null;

            _prevTaskToken?.Cancel();

            _addCompositeDialog?.Close();
            _addFolderDialog?.Close();

            imageList.Images.Clear();
            imageList.Dispose();
            FileBrowserImageListLarge.Images.Clear();
            FileBrowserImageListLarge.Dispose();
            FileBrowserImageListSmall.Images.Clear();
            FileBrowserImageListSmall.Dispose();
            if (_previewImageList != null)
            {
                CompositePreviewImages.Release(_previewImageList);
                _previewImageList.Images.Clear();
                _previewImageList.Dispose();
                _previewImageList = null;
            }
        }

        public void SelectCompositeAndReloadList(Composite composite)
        {
            Content.EnsureEditorUtils();
            Content.Level.Commands.Entries = Content.Level.Commands.Entries.OrderBy(o => o.name).ToList();
            ReloadList();
            LoadComposite(composite);
        }

        /* For undo: the browser after composites came or went */
        public void RefreshList()
        {
            ReloadList();
        }

        /* For undo: what follows a rename, however many composites it touched */
        public void AfterCompositesRenamed(List<Composite> renamed)
        {
            if (renamed == null || renamed.Count == 0)
                return;

            RefreshCachedCompositeInstances(renamed);
            DirtyTracker.MarkLevelDataModified();
            if (renamed.Count == 1)
            {
                Singleton.OnCompositeRenamed?.Invoke(renamed[0], NormalisePath(renamed[0].name));
            }
            else
            {
                CompositeDisplay?.ReloadEntityListFromComposite();
                Composite openComposite = CompositeDisplay?.Populated == true ? CompositeDisplay.Composite : null;
                if (openComposite != null)
                    Singleton.OnCompositeRenamed?.Invoke(openComposite, NormalisePath(openComposite.name));
            }
            ReloadList();
        }

        /* Reload the folder/composite display */
        private void ReloadList(bool updateListViewToo = true)
        {
            if (Content?.Level?.Commands == null || !Content.Level.Commands.Loaded)
                return;

            if (treeView1 == null || treeView1.IsDisposed || _treeUtility == null)
                return;

            //The list's icons are picked by composite type, which the editor utils know
            Content.EnsureEditorUtils();
            if (updateListViewToo)
                _treeUtility.UpdateFileTree(GetCompositeNamesForTree());

            listView1.BeginUpdate();
            try
            {
                listView1.Items.Clear();
                ApplyBrowserPreviewList();
                switch (_mode)
                {
                    case CompositeBrowserMode.TreeAndBrowser:
                        UpdatePathRow();
                        BuildFolderList();
                        break;
                    case CompositeBrowserMode.TreeAndPreview:
                        BuildFlatList();
                        break;
                }
            }
            finally
            {
                listView1.EndUpdate();
            }
            _previewFillPending = true;

            if (!_suppressSelectionRestore)
                RestoreSelectionForLoadedComposite(updateListViewToo);
        }

        /* The folder being browsed: its subfolders, and the composites directly in it */
        private void BuildFolderList()
        {
            string[] currentPathSplit = _currentDisplayFolderPath.Split('/');
            bool currentPathIsRoot = currentPathSplit.Length == 1 && currentPathSplit[0] == "";

            Dictionary<string, ListViewItem> addedItems = new Dictionary<string, ListViewItem>(StringComparer.OrdinalIgnoreCase);
            Composite rootComposite = Content.Level.Commands.EntryPoints[0];
            foreach (Composite composite in Content.Level.Commands.Entries)
            {
                //Make sure this folder/composite should be visible at the current folder path
                string name = composite.name.Replace('\\', '/');
                string[] nameSplit = name.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (nameSplit.Length == 0)
                {
                    if (composite != rootComposite || !currentPathIsRoot)
                        continue;

                    AddCompositeListItem(composite, "(Root)", false, addedItems);
                    continue;
                }

                bool shouldAdd = true;
                if (!currentPathIsRoot)
                {
                    for (int i = 0; i < currentPathSplit.Length; i++)
                    {
                        if (currentPathSplit[i] == "")
                            continue;

                        if (i >= nameSplit.Length || !string.Equals(currentPathSplit[i], nameSplit[i], StringComparison.OrdinalIgnoreCase))
                        {
                            shouldAdd = false;
                            break;
                        }
                    }
                }
                if (!shouldAdd) continue;

                //Get formatting
                int visibleSegmentIndex = currentPathIsRoot ? 0 : currentPathSplit.Length;
                if (visibleSegmentIndex >= nameSplit.Length)
                    continue;

                bool isFolder = nameSplit.Length > visibleSegmentIndex + 1;
                string text = nameSplit[visibleSegmentIndex];
                if (text == "") continue;

                AddCompositeListItem(composite, text, isFolder, addedItems, folderName: isFolder ? text : null);
            }
        }

        /* Every composite in the level in one list, by name, filtered by the search box. Built with stock
           icons only: the previews follow as the list scrolls to them (see the preview fill timer). */
        private void BuildFlatList()
        {
            Composite rootComposite = Content.Level.Commands.EntryPoints[0];
            bool namesOnly = SettingsManager.GetBool(Settings.CompNameOnlyOpt);

            List<KeyValuePair<string, Composite>> composites = new List<KeyValuePair<string, Composite>>(Content.Level.Commands.Entries.Count);
            foreach (Composite composite in Content.Level.Commands.Entries)
            {
                if (composite == null || IsFolderPlaceholder(composite))
                    continue;   //an empty folder's placeholder is not a composite to open
                if (!MatchesSearch(composite, namesOnly))
                    continue;
                string leaf = IsUnnamedRoot(composite, rootComposite) ? "(Root)" : EditorUtils.GetCompositeName(composite);
                if (leaf.Length == 0)
                    continue;
                composites.Add(new KeyValuePair<string, Composite>(leaf, composite));
            }

            //By the name shown, the root first; the same name in two folders is ordered by the folders
            composites.Sort((a, b) =>
            {
                if (a.Value == rootComposite) return b.Value == rootComposite ? 0 : -1;
                if (b.Value == rootComposite) return 1;
                int byLeaf = string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
                return byLeaf != 0 ? byLeaf : string.Compare(a.Value.name, b.Value.name, StringComparison.OrdinalIgnoreCase);
            });

            ListViewItem[] items = new ListViewItem[composites.Count];
            for (int i = 0; i < composites.Count; i++)
            {
                Composite composite = composites[i].Value;
                ListViewItemContent content = new ListViewItemContent()
                {
                    Composite = composite,
                    StockImageIndex = StockImageIndexFor(composite),
                };
                items[i] = new ListViewItem()
                {
                    Text = composites[i].Key,
                    ImageIndex = content.StockImageIndex,
                    //The name under the preview is the leaf; the whole path is a hover away
                    ToolTipText = IsUnnamedRoot(composite, rootComposite) ? "(Root)" : composite.name.Replace('\\', '/'),
                    Tag = content,
                };
            }
            listView1.Items.AddRange(items);
        }

        private static bool IsUnnamedRoot(Composite composite, Composite rootComposite)
        {
            return composite == rootComposite && string.IsNullOrWhiteSpace(composite.name);
        }

        /* The icon a composite gets when it has no preview: the prefab, or the globe, cog and avatar for the
           root, the global and pause menu, and a display model */
        private int StockImageIndexFor(Composite composite)
        {
            EditorUtils.CompositeType type = Content.EditorUtils.GetCompositeType(composite);
            return type == EditorUtils.CompositeType.IS_ROOT ? 2 : type == EditorUtils.CompositeType.IS_PAUSE_MENU || type == EditorUtils.CompositeType.IS_GLOBAL ? 3 : type == EditorUtils.CompositeType.IS_DISPLAY_MODEL ? 4 : 0;
        }

        /* The folder being browsed, spelt out on the breadcrumb: (Root), then each folder down to it */
        private void UpdatePathRow()
        {
            if (_pathBreadcrumb == null)
                return;

            List<string> labels = new List<string>() { "(Root)" };
            foreach (string part in NormalisePath(_currentDisplayFolderPath).Split('/'))
            {
                if (part.Length != 0)
                    labels.Add(part);
            }
            _pathBreadcrumb.SetSegments(labels);
        }

        /* A folder on the breadcrumb was clicked: browse it, the way entering one from the list does */
        private void PathSegmentClicked(int segmentIndex)
        {
            string[] parts = NormalisePath(_currentDisplayFolderPath).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            int keep = Math.Max(0, Math.Min(segmentIndex, parts.Length));
            _currentDisplayFolderPath = string.Join("/", parts, 0, keep);

            ReloadList(false);
            _treeUtility.SelectNode(_currentDisplayFolderPath);
            treeView1.SelectedNode?.Expand();
        }

        private List<string> GetCompositeNamesForTree()
        {
            Composite rootComposite = Content.Level.Commands.EntryPoints[0];
            return Content.Level.Commands.Entries
                .Select(composite =>
                {
                    string name = composite.name?.Replace('\\', '/') ?? "";
                    if (string.IsNullOrWhiteSpace(name) && composite == rootComposite)
                        return "(Root)";
                    return name;
                })
                .ToList();
        }

        private string GetCompositeTreePath(Composite composite)
        {
            if (composite == null)
                return "";

            string name = composite.name?.Replace('\\', '/') ?? "";
            if (!string.IsNullOrWhiteSpace(name))
                return name;

            if (Content.Level.Commands.EntryPoints[0] == composite)
                return "(Root)";

            return name;
        }

        private void AddCompositeListItem(Composite composite, string text, bool isFolder, Dictionary<string, ListViewItem> addedItems, string folderName = null)
        {
            if (text == "")
                return;

            ListViewItemContent content = new ListViewItemContent() { IsFolder = isFolder };
            if (isFolder) content.FolderName = folderName ?? text;
            else content.Composite = composite;
            content.StockImageIndex = isFolder ? 1 : StockImageIndexFor(composite);

            ListViewItem newItem = new ListViewItem()
            {
                Text = text,
                ImageIndex = content.StockImageIndex,
                Tag = content
            };

            if (addedItems.TryGetValue(text, out ListViewItem existingItem))
            {
                ListViewItemContent existingContent = (ListViewItemContent)existingItem.Tag;
                if (existingContent.IsFolder && !isFolder)
                {
                    listView1.Items.Remove(existingItem);
                    listView1.Items.Add(newItem);
                    addedItems[text] = newItem;
                }

                return;
            }

            addedItems.Add(text, newItem);
            listView1.Items.Add(newItem);
        }

        private void RestoreSelectionForLoadedComposite(bool updateTreeSelection)
        {
            Composite loadedComposite = CompositeDisplay?.Populated == true ? CompositeDisplay.Composite : null;
            if (loadedComposite == null)
                return;

            //The folder browser can only select what is in the folder it shows; the flat list has everything
            if (_mode == CompositeBrowserMode.TreeAndBrowser && GetCompositeParentFolderPath(loadedComposite) != _currentDisplayFolderPath)
                return;

            _suppressSelectionRestore = true;
            try
            {
                if (updateTreeSelection)
                    SelectCompositeInTree(loadedComposite);

                SelectCompositeInListView(loadedComposite);
            }
            finally
            {
                _suppressSelectionRestore = false;
            }
        }

        private void SelectCompositeInTree(Composite composite)
        {
            _suppressTreeSelectionDebounce = true;
            try
            {
                _treeUtility.SelectNode(GetCompositeTreePath(composite), expandPath: true);
            }
            finally
            {
                _suppressTreeSelectionDebounce = false;
            }
        }

        /* Dock the panel where it lives, and lay it out for the browser mode */
        public void UpdateDockState()
        {
            DockAreas = DockAreas.DockLeft;

            if (DockState == DockState.DockLeftAutoHide)
                Show(Singleton.Editor.DockPanel, DockState.DockLeft);

            if (DockState == DockState.Hidden || DockState == DockState.Unknown || DockState == DockState.Float)
            {
                if (Pane == null)
                    Show(Singleton.Editor.DockPanel, DockState.DockLeft);
            }

            DockAreas = DockAreas.DockLeft;

            ApplyBrowserMode();
            splitContainer1.FixedPanel = FixedPanel.None;

            ApplySplitterDistance();

            Singleton.Editor.DockPanel.ActiveAutoHideContent = null;
        }

        /* The mode from the settings, applied: the lower panel collapsed for the tree alone, the path row
           only for the folder browser, and the list rebuilt for whichever it now shows. Nothing needs a
           restart - the tree stays as it is and the list is remade under it. */
        private void ApplyBrowserMode()
        {
            CompositeBrowserMode mode = CompositeBrowserModes.Current;
            bool changed = mode != _mode;
            _mode = mode;

            splitContainer1.Panel2Collapsed = mode == CompositeBrowserMode.TreeOnly;
            if (_pathPanel != null)
                _pathPanel.Visible = mode == CompositeBrowserMode.TreeAndBrowser;

            if (changed)
                ReloadList(false);

            if (mode == CompositeBrowserMode.TreeAndPreview)
                _previewFillTimer?.Start();
            else
                _previewFillTimer?.Stop();
        }

        private void ApplySplitterDistance()
        {
            if (splitContainer1 == null || splitContainer1.IsDisposed)
                return;

            int min = splitContainer1.Panel1MinSize;
            int available = splitContainer1.Orientation == System.Windows.Forms.Orientation.Horizontal
                ? splitContainer1.Height
                : splitContainer1.Width;
            if (available <= 0)
                return;

            int max = available - splitContainer1.SplitterWidth - splitContainer1.Panel2MinSize;
            if (max < min)
                return;

            int desired = SettingsManager.GetInteger(Settings.CompositeBrowserSplitter, _defaultSplitterDistance);
            desired = Math.Max(min, Math.Min(desired, max));
            if (splitContainer1.SplitterDistance == desired)
                return;

            splitContainer1.SplitterDistance = desired;
        }

        public void ResetSplitter()
        {
            SettingsManager.SetInteger(Settings.CompositeBrowserSplitter, _defaultSplitterDistance);
            ApplySplitterDistance();
        }

        //UI: handle saving split container width between commands/runs 
        private void treeView1_Resize(object sender, EventArgs e)
        {
            int distance = splitContainer1.SplitterDistance;
            if (distance >= MinTreePanelSize)
                SettingsManager.SetInteger(Settings.CompositeBrowserSplitter, distance);
        }

        /* File browser: select folder/composite */
        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressSelectionRestore)
                return;

            if (listView1.SelectedItems.Count != 1) return;

            ListViewItem item = listView1.SelectedItems[0];
            ListViewItemContent content = (ListViewItemContent)item.Tag;
            if (content.IsFolder)
            {
                if (_currentDisplayFolderPath == "") _currentDisplayFolderPath = content.FolderName;
                else _currentDisplayFolderPath = _currentDisplayFolderPath + "/" + content.FolderName;

                ReloadList(false);
            }
            else
            {
                LoadComposite(content.Composite);
            }

            //The folder browser's tree follows the folder; the flat list's already has the composite selected (loading it did that)
            if (_mode == CompositeBrowserMode.TreeAndBrowser)
            {
                _treeUtility.SelectNode(_currentDisplayFolderPath);
                treeView1.SelectedNode?.Expand();
            }
        }

        /* File list: select folder/composite */
        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (treeView1.SelectedNode == null || _suppressTreeSelectionDebounce)
                return;

            _pendingTreeSelection = e.Node;
            _treeSelectionDebounceTimer.Stop();
            _treeSelectionDebounceTimer.Start();
        }

        private void TreeSelectionDebounceTimer_Tick(object sender, EventArgs e)
        {
            _treeSelectionDebounceTimer.Stop();
            if (_treeDragInProgress || _pendingTreeSelection == null)
                return;

            ProcessTreeNodeSelection(_pendingTreeSelection);
        }

        private void ProcessTreeNodeSelection(TreeNode node)
        {
            if (node?.Tag == null)
                return;

            TreeItem item = (TreeItem)node.Tag;
            switch (item.Item_Type)
            {
                case TreeItemType.EXPORTABLE_FILE:
                    LoadComposite(item.String_Value);
                    break;
                case TreeItemType.DIRECTORY:
                    _currentDisplayFolderPath = item.String_Value;
                    if (_mode == CompositeBrowserMode.TreeAndBrowser)
                        ReloadList(false);
                    break;
            }
        }

        /* File path: go back */
        private void goBackOnPath_Click(object sender, EventArgs e)
        {
            if (_currentDisplayFolderPath == "") return;

            string[] pathSplit = (_currentDisplayFolderPath + "/").Split('/');
            _currentDisplayFolderPath = _currentDisplayFolderPath.Substring(0, _currentDisplayFolderPath.Length - pathSplit[pathSplit.Length - 2].Length);
            if (pathSplit.Length != 2) _currentDisplayFolderPath = _currentDisplayFolderPath.Substring(0, _currentDisplayFolderPath.Length - 1);

            ReloadList(false);
        }

        private class ListViewItemContent
        {
            public bool IsFolder;
            public Composite Composite;
            public string FolderName;
            //The icon the item falls back on, and whether the flat list has looked for its preview yet
            public int StockImageIndex;
            public PreviewState Preview;
        }

        private enum PreviewState
        {
            NotLooked,
            None,
            Shown,
        }

        private void SelectComposite(Composite composite)
        {
            if (composite == null)
                return;

            _treeSelectionDebounceTimer.Stop();
            _pendingTreeSelection = null;

            SelectCompositeInTree(composite);
            SyncFileBrowserToComposite(composite);

            this.BringToFront();
            this.Focus();
        }

        private static string GetCompositeParentFolderPath(Composite composite)
        {
            if (composite == null || string.IsNullOrEmpty(composite.name))
                return "";

            string normalizedName = composite.name.Replace('\\', '/').Trim('/');
            if (normalizedName == "")
                return "";

            int lastSeparator = normalizedName.LastIndexOf('/');
            return lastSeparator < 0 ? "" : normalizedName.Substring(0, lastSeparator);
        }

        private void SyncFileBrowserToComposite(Composite composite)
        {
            if (composite == null)
                return;

            //The folder browser opens the composite's folder; the flat list holds every composite already
            _currentDisplayFolderPath = GetCompositeParentFolderPath(composite);
            if (_mode == CompositeBrowserMode.TreeAndBrowser)
                ReloadList(false);
            SelectCompositeInListView(composite);
        }

        private void SelectCompositeInListView(Composite composite)
        {
            if (composite == null)
                return;

            //Scrolling the selection into view is a move the preview fill should follow at once
            _previewFillPending = true;
            string compositeFileName = EditorUtils.GetCompositeName(composite);
            foreach (ListViewItem item in listView1.Items)
            {
                if (item.Tag is ListViewItemContent content
                    && !content.IsFolder
                    && content.Composite?.shortGUID == composite.shortGUID)
                {
                    item.Selected = true;
                    item.Focused = true;
                    item.EnsureVisible();
                    return;
                }
            }

            //The flat list gives every composite its own item, so one that is not there is hidden by the
            //search, and no other item stands in for it - a namesake from another folder least of all. The
            //folder browser lists one item per name, and there a namesake's item is the one to select.
            if (_mode == CompositeBrowserMode.TreeAndPreview)
            {
                listView1.SelectedItems.Clear();
                return;
            }

            foreach (ListViewItem item in listView1.Items)
            {
                if (item.Tag is ListViewItemContent content
                    && !content.IsFolder
                    && string.Equals(item.Text, compositeFileName, StringComparison.OrdinalIgnoreCase))
                {
                    item.Selected = true;
                    item.Focused = true;
                    item.EnsureVisible();
                    return;
                }
            }

            if (Content.Level.Commands.EntryPoints[0] == composite
                && string.IsNullOrWhiteSpace(composite.name))
            {
                foreach (ListViewItem item in listView1.Items)
                {
                    if (item.Tag is ListViewItemContent content
                        && !content.IsFolder
                        && content.Composite?.shortGUID == composite.shortGUID)
                    {
                        item.Selected = true;
                        item.Focused = true;
                        item.EnsureVisible();
                        return;
                    }
                }
            }
        }

        public void CloseAllChildTabs()
        {
            CompositeDisplay?.DepopulateUI();
        }

        public void ReloadAllEntities()
        {
            CompositeDisplay?.ReloadAllEntities();
        }

        public void Reload(bool alsoReloadEntities = true)
        {
            CompositeDisplay?.Reload(alsoReloadEntities);
        }

        protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg, System.Windows.Forms.Keys keyData)
        {
            if (OpenCAGE.Undo.UndoKeys.TryHandle(keyData))
                return true;
            if (OpenCAGE.UnityConnection.ViewerCreateModeKeys.TryHandle(keyData))
                return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public CompositeDisplay LoadComposite(string name)
        {
            return LoadComposite(Content.Level.Commands.GetComposite(name));
        }
        public CompositeDisplay LoadComposite(ShortGuid guid)
        {
            return LoadComposite(Content.Level.Commands.GetComposite(guid));
        }
        public CompositeDisplay LoadComposite(Composite composite, bool newDisplay = false)
        {
            if (composite == null)
                return null;

            CompositeDisplay display = Singleton.Editor.LoadComposite(composite, newDisplay);
            SelectComposite(composite);
            return display;
        }

        public void LoadCompositeAndEntity(ShortGuid compositeGUID, ShortGuid entityGUID)
        {
            Composite composite = Content.Level.Commands.GetComposite(compositeGUID);
            LoadCompositeAndEntity(composite, composite?.GetEntityByID(entityGUID));
        }
        public void LoadCompositeAndEntity(Composite composite, Entity entity)
        {
            if (composite == null || entity == null)
                return;

            CompositeDisplay panel = CompositeDisplay;
            if (panel == null || panel.IsDisposed || !panel.Populated)
            {
                panel = LoadComposite(composite);
            }
            else if (panel.Composite?.shortGUID != composite.shortGUID
                     || panel.Composite.GetEntityByID(entity.shortGUID) == null)
            {
                panel = LoadComposite(composite);
            }

            panel?.LoadEntity(entity, true);
        }

        public void DeleteComposite(Composite composite, bool prompt = true)
        {
            if (composite == null)
                return;

            if (ContainsEntryPoint(new List<Composite>() { composite }, out Composite _))
            {
                MessageBox.Show("Cannot delete a composite which is the root, global, or pause menu!", "Cannot delete.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (prompt && MessageBox.Show("Are you sure you want to remove " + Path.GetFileName(composite.name) + "?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            DeleteCompositesInternal(new List<Composite>() { composite });
        }

        /* Is one of these composites the root/global/pause menu? */
        private bool ContainsEntryPoint(List<Composite> composites, out Composite entryPoint)
        {
            entryPoint = null;
            foreach (Composite composite in composites)
            {
                for (int i = 0; i < Content.Level.Commands.EntryPoints.Count(); i++)
                {
                    if (Content.Level.Commands.EntryPoints[i] != null
                        && composite.shortGUID == Content.Level.Commands.EntryPoints[i].shortGUID)
                    {
                        entryPoint = composite;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Remove a set of composites, along with any entities, links and aliases that referenced them.
        /// Proxies into them are left as dead proxies (see CommandsUtils.IsDeadProxy), to be re-pointed.
        /// Done as a batch: a folder can hold well over a thousand composites, and the reference cleanup and UI
        /// refresh both walk the whole level, so doing this per composite would take minutes.
        /// </summary>
        private void DeleteCompositesInternal(List<Composite> composites)
        {
            if (composites == null || composites.Count == 0)
                return;

            //The removal lives in the edit, which keeps every entity, link, alias and proxy it takes out
            string label = composites.Count == 1
                ? "Delete " + EditorUtils.GetCompositeName(composites[0])
                : "Delete " + composites.Count + " composites";
            OpenCAGE.Undo.UndoStack.Current.Apply(new OpenCAGE.Undo.CompositeDeleteEdit(new List<Composite>(composites), label));
        }
        /// <summary>
        /// Delete a folder and everything inside it.
        /// </summary>
        private void DeleteFolder(string folderFullPath)
        {
            string folder = NormalisePath(folderFullPath);
            if (folder.Length == 0)
                return;

            List<Composite> toDelete = GetCompositesUnderFolder(folder);
            if (toDelete.Count == 0)
                return;

            if (ContainsEntryPoint(toDelete, out Composite entryPoint))
            {
                MessageBox.Show("This folder contains \"" + EditorUtils.GetCompositeName(entryPoint)
                    + "\", which is the root, global, or pause menu, so it can't be deleted.", "Cannot delete.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            //Folder placeholders aren't real content, so don't count them in the warning
            int compositeCount = toDelete.Count(o => !IsFolderPlaceholder(o));
            string message = compositeCount == 0
                ? "Are you sure you want to delete the empty folder '" + folder.Replace('/', '\\') + "'?"
                : "Are you sure you want to delete '" + folder.Replace('/', '\\') + "', including the "
                    + compositeCount + " composite" + (compositeCount == 1 ? "" : "s") + " it contains?"
                    + "\n\nAny entities, links and aliases referencing them will also be removed. Proxies into them are kept as unresolvable proxies, shown in red, to be re-pointed.";

            if (MessageBox.Show(message, "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            //Step out of the folder if we're browsing inside it
            if (IsPathInFolder(_currentDisplayFolderPath, folder))
            {
                string[] parts = folder.Split('/');
                _currentDisplayFolderPath = parts.Length > 1 ? string.Join("/", parts, 0, parts.Length - 1) : "";
            }

            DeleteCompositesInternal(toDelete);
            CompositeDisplay?.Reload();
        }

        private string _currentSearch = "";
        /* Searching as you type, the way the composite picker popup does. A keystroke here costs more
         * than it does there - the whole composite list is walked and the tree rebuilt, which on a big
         * level is thousands of entries - so it waits for a short pause rather than running per key. */
        private System.Windows.Forms.Timer _searchDebounce = null;

        private void entity_search_box_TextChanged(object sender, EventArgs e)
        {
            LayoutTreeSearchRow();      //shows or hides the clear button, and resizes the box round it

            if (_searchDebounce == null)
            {
                _searchDebounce = new System.Windows.Forms.Timer { Interval = 250 };
                _searchDebounce.Tick += (s, args) => { _searchDebounce.Stop(); RunCompositeSearch(); };
            }
            _searchDebounce.Stop();
            _searchDebounce.Start();
        }

        private void entity_search_clear_btn_Click(object sender, EventArgs e)
        {
            //Clearing the box searches for nothing, which is the whole tree back
            if (entity_search_box.Text.Length == 0) return;
            entity_search_box.Text = "";
        }

        private void RunCompositeSearch()
        {
            if (Content?.Level?.Commands == null) return;

            string newSearch = entity_search_box.Text.Replace('\\', '/').ToUpper().Replace(" ", "");
            if (newSearch == _currentSearch) return;

            _currentSearch = newSearch;
            bool namesOnly = SettingsManager.GetBool(Settings.CompNameOnlyOpt);
            List<string> filteredCompositeNames = new List<string>();
            foreach (Composite composite in Content.Level.Commands.Entries)
            {
                if (composite != null && MatchesSearch(composite, namesOnly))
                    filteredCompositeNames.Add(composite.name.Replace('\\', '/'));
            }

            _treeUtility.UpdateFileTree(filteredCompositeNames);

            if (entity_search_box.Text != "")
            {
                treeView1.ExpandAll();

                //The flat list is the search's other result: the same composites, captured
                if (_mode == CompositeBrowserMode.TreeAndPreview)
                    ReloadList(false);
            }
            else
            {
                ReloadList();
            }
        }

        /* The search box's rule, shared by the tree and the flat list: case does not matter, nor do spaces,
           and the folders count unless the option says only the composite's own name does */
        private bool MatchesSearch(Composite composite, bool namesOnly)
        {
            if (_currentSearch.Length == 0)
                return true;

            string name = (composite.name ?? "").Replace('\\', '/');
            if (namesOnly)
            {
                string[] nameSplit = name.Split('/');
                name = nameSplit[nameSplit.Length - 1];
            }
            return name.ToUpper().Replace(" ", "").Contains(_currentSearch);
        }

        /* File Browser Context Menu */
        private void FooListView_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var lv = sender as System.Windows.Forms.ListView;
                var item = lv.HitTest(e.Location).Item;

                ListViewItemContent content = item?.Tag as ListViewItemContent;
                bool isFolder = content != null && content.IsFolder;
                Composite comp = content != null && !content.IsFolder ? content.Composite : null;

                //Folders can be renamed/deleted too - that rewrites or removes everything inside them
                deleteFolderToolStripMenuItem.Enabled = isFolder || (comp != null && !Content.Level.Commands.EntryPoints.Contains(comp));
                renameToolStripMenuItem.Enabled = isFolder || (comp != null && (Content.Level.Commands.EntryPoints[0] == comp || !Content.Level.Commands.EntryPoints.Contains(comp)));
                findReferencesToolStripMenuItem.Enabled = comp != null;
                ApplyFindReferencesIcon(findReferencesToolStripMenuItem);

                if (item != null)
                    lv.FocusedItem = item;

                FileBrowserContextMenu.Show(lv, e.Location);
            }
        }
        TreeNode _rightClickedNode = null;
        private void FileTree_MouseDown(object sender, MouseEventArgs e)
        {
            //With the folder browser under it the tree only steers that: the list is what is right-clicked
            //and dragged from. The tree alone, or with the flat list, keeps its own menu and drag.
            if (_mode == CompositeBrowserMode.TreeAndBrowser)
                return;

            if (e.Button == MouseButtons.Left)
            {
                _treeDragStartPoint = e.Location;
                _treeDragInProgress = false;
            }

            if (e.Button == MouseButtons.Right)
            {
                var lv = sender as System.Windows.Forms.TreeView;
                _rightClickedNode = lv.HitTest(e.Location).Node;

                bool hasTreeItem = _rightClickedNode != null && _rightClickedNode.Tag is TreeItem;
                TreeItem rightClickedItem = hasTreeItem ? (TreeItem)_rightClickedNode.Tag : default(TreeItem);
                bool isDirectory = hasTreeItem && rightClickedItem.Item_Type == TreeItemType.DIRECTORY;
                Composite comp = hasTreeItem && !isDirectory ? Content.Level.Commands.GetComposite(rightClickedItem.String_Value) : null;

                //Folders can be renamed/deleted too - that rewrites or removes everything inside them
                toolStripMenuItem4.Enabled = isDirectory || (comp != null && !Content.Level.Commands.EntryPoints.Contains(comp));
                toolStripMenuItem5.Enabled = isDirectory || (comp != null && (Content.Level.Commands.EntryPoints[0] == comp || !Content.Level.Commands.EntryPoints.Contains(comp)));
                findReferencesViaTreeView.Enabled = comp != null;
                ApplyFindReferencesIcon(findReferencesViaTreeView);

                if (_rightClickedNode == null)
                {
                    _currentDisplayFolderPath = "";
                }
                else
                {
                    TreeItem item = (TreeItem)_rightClickedNode.Tag;
                    switch (item.Item_Type)
                    {
                        case TreeItemType.EXPORTABLE_FILE:
                            Composite c = Content.Level.Commands.GetComposite(item.String_Value);
                            int nameLength = EditorUtils.GetCompositeName(c).Length;
                            _currentDisplayFolderPath = (c.name.Length != nameLength ? c.name.Substring(0, c.name.Length - nameLength - 1) : "");
                            break;
                        case TreeItemType.DIRECTORY:
                            _currentDisplayFolderPath = item.String_Value;
                            break;
                    }
                }

                FileTreeContextMenuNew.Show(lv, e.Location);
            }
        }

        private void FileTree_MouseMove(object sender, MouseEventArgs e)
        {
            if (_mode == CompositeBrowserMode.TreeAndBrowser)
                return;
            if ((e.Button & MouseButtons.Left) != MouseButtons.Left || _treeDragInProgress)
                return;

            Size dragSize = SystemInformation.DragSize;
            if (Math.Abs(e.X - _treeDragStartPoint.X) < dragSize.Width
                && Math.Abs(e.Y - _treeDragStartPoint.Y) < dragSize.Height)
                return;

            TreeNode node = treeView1.GetNodeAt(_treeDragStartPoint);
            if (node?.Tag == null || !(node.Tag is TreeItem item))
                return;
            if (item.Item_Type != TreeItemType.EXPORTABLE_FILE || string.IsNullOrEmpty(item.String_Value))
                return;

            _treeSelectionDebounceTimer.Stop();
            _treeDragInProgress = true;

            DataObject data = new DataObject();
            data.SetData(CompositeDragFormat, item.String_Value);

            _viewportDropPoint = null;
            QueryContinueDrag += CompositeDrag_QueryContinueDrag;
            GiveFeedback += CompositeDrag_GiveFeedback;
            try
            {
                DoDragDrop(data, DragDropEffects.Copy);
            }
            finally
            {
                QueryContinueDrag -= CompositeDrag_QueryContinueDrag;
                GiveFeedback -= CompositeDrag_GiveFeedback;
                _treeDragInProgress = false;
            }

            if (_viewportDropPoint.HasValue)
            {
                Point droppedAt = _viewportDropPoint.Value;
                _viewportDropPoint = null;
                ViewerCompositeDrop.TryDrop(Content?.Level?.Commands?.GetComposite(item.String_Value), droppedAt);
            }
        }

        /* The viewport is another process's window sitting over its host panel, so a drop on it can't be
           relied on to come back to us as a DragDrop event the way the flowgraph's does. Watch the cursor
           for the rest of the drag instead, and take the drop ourselves if it lands there. */
        private void CompositeDrag_QueryContinueDrag(object sender, QueryContinueDragEventArgs e)
        {
            if (e.EscapePressed || e.Action != DragAction.Drop)
                return;
            if (!ViewerCompositeDrop.IsCursorOverViewport())
                return;

            _viewportDropPoint = Cursor.Position;
            e.Action = DragAction.Cancel;
        }

        /* The viewer's window has no idea what we're dragging, so its feedback is meaningless - say ourselves
           that the drop is on. */
        private void CompositeDrag_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            if (!ViewerCompositeDrop.IsCursorOverViewport())
                return;

            e.UseDefaultCursors = false;
            Cursor.Current = Cursors.Cross;
        }

        private void deleteFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count != 1) return;

            ListViewItem item = listView1.SelectedItems[0];
            ListViewItemContent content = (ListViewItemContent)item.Tag;
            if (content.IsFolder)
            {
                DeleteFolder(GetFolderPathForListItem(content));
                return; //DeleteFolder refreshes the UI itself
            }

            DeleteComposite(content.Composite);

            CompositeDisplay?.Reload();
            ReloadList();
        }
        private void deleteViaTreeView_Click(object sender, EventArgs e)
        {
            TreeItem item = (TreeItem)_rightClickedNode.Tag;
            switch (item.Item_Type)
            {
                case TreeItemType.EXPORTABLE_FILE:
                    DeleteComposite(Content.Level.Commands.GetComposite(item.String_Value));
                    break;
                case TreeItemType.DIRECTORY:
                    DeleteFolder(item.String_Value);
                    break;
            }
        }
        RenameComposite _renameComposite;
        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count != 1) return;

            ListViewItem item = listView1.SelectedItems[0];
            ListViewItemContent content = (ListViewItemContent)item.Tag;
            if (content.IsFolder)
            {
                RenameFolder(GetFolderPathForListItem(content));
            }
            else
            {
                RenameComposite(content.Composite);
            }
        }

        /* Full path of a folder shown in the file browser list (it only stores its own name) */
        private string GetFolderPathForListItem(ListViewItemContent content)
        {
            if (content == null || !content.IsFolder)
                return "";
            string current = NormalisePath(_currentDisplayFolderPath);
            return current.Length == 0 ? content.FolderName : current + "/" + content.FolderName;
        }
        private void renameViaTreeView_Click(object sender, EventArgs e)
        {
            TreeItem item = (TreeItem)_rightClickedNode.Tag;
            switch (item.Item_Type)
            {
                case TreeItemType.EXPORTABLE_FILE:
                    RenameComposite(Content.Level.Commands.GetComposite(item.String_Value));
                    break;
                case TreeItemType.DIRECTORY:
                    RenameFolder(item.String_Value);
                    break;
            }
        }

        private void findReferencesViaTreeView_Click(object sender, EventArgs e)
        {
            if (_rightClickedNode == null || _rightClickedNode.Tag == null)
                return;

            TreeItem item = (TreeItem)_rightClickedNode.Tag;
            if (item.Item_Type != TreeItemType.EXPORTABLE_FILE)
                return;

            FindReferencesForComposite(Content.Level.Commands.GetComposite(item.String_Value));
        }

        private void findReferencesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count != 1)
                return;

            ListViewItemContent content = (ListViewItemContent)listView1.SelectedItems[0].Tag;
            if (content == null || content.IsFolder)
                return;

            FindReferencesForComposite(content.Composite);
        }

        private void FindReferencesForComposite(Composite composite)
        {
            if (composite == null)
                return;

            Singleton.Editor?.EntitySearch?.SearchForComposite(composite);
        }

        private void ApplyFindReferencesIcon(ToolStripMenuItem item)
        {
            if (item == null || item.Image != null)
                return;

            item.Image = Singleton.Editor?.CompositeDisplay?.FindReferencesIcon;
        }
        private void RenameComposite(Composite composite)
        {
            if (_renameComposite != null)
                _renameComposite.Close();

            _renameComposite = new RenameComposite(composite);
            _renameComposite.Show();
            _renameComposite.FormClosed += _renameComposite_FormClosed;
        }

        #region Folder Rename & Reorganise
        //Folders aren't stored anywhere - they're implied by the '\' separated composite names, so renaming or
        //moving one means rewriting the path prefix of every composite inside it. Empty folders exist as a
        //placeholder composite whose name ends in a separator (see AddFolder).

        public const string BrowserMoveDragFormat = "OpenCAGE.CompositeBrowser.Move";

        private RenameGeneric _renameFolder;
        private TreeNode _dropHighlightNode = null;
        private ListViewItem _dropHighlightItem = null;

        /* Composite names use '\', but paths are compared as '/' with no leading/trailing separator */
        private static string NormalisePath(string path)
        {
            return (path ?? "").Replace('\\', '/').Trim('/');
        }

        /* Is this composite a placeholder representing an otherwise empty folder? */
        private static bool IsFolderPlaceholder(Composite composite)
        {
            string name = composite?.name ?? "";
            return name.EndsWith("\\") || name.EndsWith("/");
        }

        /* True if 'path' is the folder itself or sits somewhere inside it */
        private static bool IsPathInFolder(string path, string folder)
        {
            string normalisedPath = NormalisePath(path);
            string normalisedFolder = NormalisePath(folder);
            if (normalisedFolder.Length == 0)
                return true; //everything is inside the root
            if (string.Equals(normalisedPath, normalisedFolder, StringComparison.OrdinalIgnoreCase))
                return true;
            return normalisedPath.StartsWith(normalisedFolder + "/", StringComparison.OrdinalIgnoreCase);
        }

        /* Every composite inside a folder, including the placeholder for the folder itself */
        private List<Composite> GetCompositesUnderFolder(string folderPath)
        {
            List<Composite> results = new List<Composite>();
            string folder = NormalisePath(folderPath);
            if (folder.Length == 0)
                return results;

            foreach (Composite composite in Content.Level.Commands.Entries)
            {
                if (composite == null) continue;
                if (IsPathInFolder(composite.name, folder))
                    results.Add(composite);
            }
            return results;
        }

        /* Does anything already live at this path - a composite, or a folder containing composites? */
        private bool PathAlreadyExists(string path, List<Composite> ignoring = null)
        {
            string target = NormalisePath(path);
            if (target.Length == 0)
                return false;

            foreach (Composite composite in Content.Level.Commands.Entries)
            {
                if (composite == null) continue;
                if (ignoring != null && ignoring.Contains(composite)) continue;
                if (IsPathInFolder(composite.name, target))
                    return true;
            }
            return false;
        }

        /* Regenerate the cached list rows for every entity that instances one of these composites (single pass) */
        private void RefreshCachedCompositeInstances(List<Composite> renamed)
        {
            HashSet<ShortGuid> renamedIds = new HashSet<ShortGuid>();
            foreach (Composite composite in renamed)
                renamedIds.Add(composite.shortGUID);

            foreach (Composite composite in Content.Level.Commands.Entries)
            {
                if (composite?.functions_dictionary == null) continue;
                foreach (FunctionEntity function in composite.functions_dictionary.Values)
                {
                    if (renamedIds.Contains(function.function))
                        Content.GenerateListViewItem(function, composite, LevelContent.CacheMethod.IGNORE_AND_OVERWRITE_CACHE);
                }
            }
        }

        /// <summary>
        /// Rewrite the path prefix of everything inside a folder - used for both renaming and moving one.
        /// Nothing is changed unless the whole operation is safe.
        /// </summary>
        private bool MoveFolderContents(string oldFolder, string newFolder)
        {
            string from = NormalisePath(oldFolder);
            string to = NormalisePath(newFolder);
            if (from.Length == 0 || to.Length == 0 || string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
                return false;

            if (IsPathInFolder(to, from))
            {
                MessageBox.Show("Can't move a folder inside itself.", "Invalid destination", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            List<Composite> affected = GetCompositesUnderFolder(from);
            if (affected.Count == 0)
                return false;

            if (PathAlreadyExists(to, affected))
            {
                MessageBox.Show("Something already exists at:\n" + to.Replace('/', '\\'), "Destination in use", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            //Entry point composites must keep their names for the level to load
            foreach (Composite composite in affected)
            {
                if (Content.Level.Commands.EntryPoints.Contains(composite))
                {
                    MessageBox.Show("This folder contains \"" + EditorUtils.GetCompositeName(composite)
                        + "\", which the level needs at its current path, so it can't be moved.", "Can't move folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }

            Dictionary<ShortGuid, string> previousNames = affected.ToDictionary(o => o.shortGUID, o => o.name);

            //Rename everything first, without raising per-composite events: a big folder can hold well over a
            //thousand composites, and each OnCompositeRenamed rebuilds the whole browser tree. The editor is
            //told once at the end instead.
            foreach (Composite composite in affected)
            {
                string normalised = NormalisePath(composite.name);
                string newName = to + normalised.Substring(from.Length);
                composite.name = newName.Replace('/', '\\') + (IsFolderPlaceholder(composite) ? "\\" : "");
            }

            RefreshCachedCompositeInstances(affected);
            DirtyTracker.MarkLevelDataModified();
            OpenCAGE.Undo.UndoStack.Current.Record(new OpenCAGE.Undo.CompositeRenameEdit(
                affected.Select(o => new OpenCAGE.Undo.CompositeRenameEdit.Rename() { Composite = o.shortGUID, Before = previousNames[o.shortGUID], After = o.name }).ToList(),
                "Move folder " + from));

            //Follow the folder if we were browsing inside it
            if (IsPathInFolder(_currentDisplayFolderPath, from))
                _currentDisplayFolderPath = to + NormalisePath(_currentDisplayFolderPath).Substring(from.Length);

            //Refresh the open composite's entity rows (they may show instances of what just moved), then let
            //the rest of the editor update its titles/breadcrumbs from the loaded composite
            CompositeDisplay?.ReloadEntityListFromComposite();
            Composite openComposite = CompositeDisplay?.Populated == true ? CompositeDisplay.Composite : null;
            if (openComposite != null)
                Singleton.OnCompositeRenamed?.Invoke(openComposite, NormalisePath(openComposite.name));

            ReloadList();
            return true;
        }

        /* Rename the folder at this path (prompts for the new name) */
        private void RenameFolder(string folderFullPath)
        {
            string folder = NormalisePath(folderFullPath);
            if (folder.Length == 0)
                return;

            string[] parts = folder.Split('/');
            string leafName = parts[parts.Length - 1];
            string parentPath = parts.Length > 1 ? string.Join("/", parts, 0, parts.Length - 1) : "";

            if (_renameFolder != null)
                _renameFolder.Close();

            _renameFolder = new RenameGeneric(leafName, new RenameGeneric.RenameGenericContent()
            {
                Title = "Rename Folder",
                Description = "Folder name:",
                ButtonText = "Rename"
            });
            _renameFolder.OnRenamed += newName =>
            {
                string sanitised = NormalisePath(newName);
                if (sanitised.Length == 0)
                {
                    MessageBox.Show("Enter a folder name.", "Folder name invalid", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                foreach (string part in sanitised.Split('/'))
                {
                    if (part.Trim().Length == 0)
                    {
                        MessageBox.Show("A part of the folder path is blank.\nRemove trailing slashes and use complete folder names.", "Folder name invalid", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                MoveFolderContents(folder, (parentPath.Length == 0 ? "" : parentPath + "/") + sanitised);
            };
            _renameFolder.FormClosed += (s, e) => _renameFolder = null;
            _renameFolder.Show();
        }

        /* Move a composite into a different folder, keeping its own name */
        private bool MoveComposite(Composite composite, string targetFolder)
        {
            if (composite == null)
                return false;

            if (Content.Level.Commands.EntryPoints.Contains(composite) && Content.Level.Commands.EntryPoints[0] != composite)
            {
                MessageBox.Show("The level needs \"" + EditorUtils.GetCompositeName(composite) + "\" at its current path, so it can't be moved.", "Can't move composite", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            string previousName = composite.name;
            string leafName = EditorUtils.GetCompositeName(composite);
            string target = NormalisePath(targetFolder);
            string newName = (target.Length == 0 ? "" : target + "/") + leafName;

            if (string.Equals(NormalisePath(composite.name), newName, StringComparison.OrdinalIgnoreCase))
                return false; //already there

            if (PathAlreadyExists(newName))
            {
                MessageBox.Show("Something already exists at:\n" + newName.Replace('/', '\\'), "Destination in use", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            composite.name = newName.Replace('/', '\\');
            Singleton.OnCompositeRenamed?.Invoke(composite, newName);
            RefreshCachedCompositeInstances(new List<Composite>() { composite });
            OpenCAGE.Undo.UndoStack.Current.Record(new OpenCAGE.Undo.CompositeRenameEdit(
                new List<OpenCAGE.Undo.CompositeRenameEdit.Rename>() { new OpenCAGE.Undo.CompositeRenameEdit.Rename() { Composite = composite.shortGUID, Before = previousName, After = composite.name } },
                "Move " + leafName));
            ReloadList();
            return true;
        }

        /* Move a folder into a different folder, keeping its own name */
        private bool MoveFolder(string folderPath, string targetFolder)
        {
            string folder = NormalisePath(folderPath);
            string target = NormalisePath(targetFolder);
            if (folder.Length == 0)
                return false;

            string[] parts = folder.Split('/');
            string leafName = parts[parts.Length - 1];
            return MoveFolderContents(folder, (target.Length == 0 ? "" : target + "/") + leafName);
        }

        //--- Drag & drop reorganising (file browser mode) -------------------------------------------------
        //Drag composites/folders from the file list onto a folder in the list, or onto any folder in the tree.

        /* Wire up drag & drop - done in code so the designer files stay untouched */
        private void SetupReorganiseDragDrop()
        {
            listView1.AllowDrop = true;
            listView1.ItemDrag += ListView_ItemDrag;
            listView1.DragEnter += Browser_DragEnter;
            listView1.DragOver += ListView_DragOver;
            listView1.DragLeave += ListView_DragLeave;
            listView1.DragDrop += ListView_DragDrop;

            treeView1.AllowDrop = true;
            treeView1.DragEnter += Browser_DragEnter;
            treeView1.DragOver += TreeView_DragOver;
            treeView1.DragLeave += TreeView_DragLeave;
            treeView1.DragDrop += TreeView_DragDrop;
        }

        private void ListView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            if (_mode == CompositeBrowserMode.TreeOnly)
                return;
            if (!(e.Item is ListViewItem item) || !(item.Tag is ListViewItemContent content))
                return;

            string payload;
            if (content.IsFolder)
            {
                payload = "F|" + GetFolderPathForListItem(content);
            }
            else
            {
                if (content.Composite == null)
                    return;
                payload = "C|" + NormalisePath(content.Composite.name);
            }

            DataObject data = new DataObject();
            data.SetData(BrowserMoveDragFormat, payload);
            if (content.IsFolder)
            {
                listView1.DoDragDrop(data, DragDropEffects.Move);
                return;
            }

            //A composite also carries what the tree hands out, so as well as into another folder it can go
            //to the flowgraph (an instance where it lands) or the viewport, watched for the same way
            data.SetData(CompositeDragFormat, content.Composite.name);
            _viewportDropPoint = null;
            listView1.QueryContinueDrag += CompositeDrag_QueryContinueDrag;
            listView1.GiveFeedback += CompositeDrag_GiveFeedback;
            try
            {
                listView1.DoDragDrop(data, DragDropEffects.Move | DragDropEffects.Copy);
            }
            finally
            {
                listView1.QueryContinueDrag -= CompositeDrag_QueryContinueDrag;
                listView1.GiveFeedback -= CompositeDrag_GiveFeedback;
            }

            if (_viewportDropPoint.HasValue)
            {
                Point droppedAt = _viewportDropPoint.Value;
                _viewportDropPoint = null;
                ViewerCompositeDrop.TryDrop(content.Composite, droppedAt);
            }
        }

        private void Browser_DragEnter(object sender, DragEventArgs e)
        {
            if (PackageDropTarget.IsPackageDrag(e))
                return; //a package drop is the package handler's to answer
            e.Effect = e.Data.GetDataPresent(BrowserMoveDragFormat) ? DragDropEffects.Move : DragDropEffects.None;
        }

        /* The dragged item, or null if this isn't one of our drags */
        private bool TryGetDragPayload(IDataObject data, out bool isFolder, out string path)
        {
            isFolder = false;
            path = null;
            if (data == null || !data.GetDataPresent(BrowserMoveDragFormat))
                return false;

            string payload = data.GetData(BrowserMoveDragFormat) as string;
            if (string.IsNullOrEmpty(payload) || payload.Length < 2)
                return false;

            isFolder = payload[0] == 'F';
            path = payload.Substring(2);
            return path.Length != 0;
        }

        /* Would dropping the dragged item into this folder be a valid move? */
        private bool CanDropInto(bool isFolder, string sourcePath, string targetFolder)
        {
            string target = NormalisePath(targetFolder);
            if (isFolder)
            {
                //Not into itself, its own children, or back where it already is
                if (IsPathInFolder(target, sourcePath))
                    return false;
                string[] parts = NormalisePath(sourcePath).Split('/');
                string parent = parts.Length > 1 ? string.Join("/", parts, 0, parts.Length - 1) : "";
                return !string.Equals(parent, target, StringComparison.OrdinalIgnoreCase);
            }

            Composite composite = Content.Level.Commands.GetComposite(sourcePath.Replace('/', '\\'));
            if (composite == null)
                return false;
            string currentParent = NormalisePath(GetCompositeParentFolderPath(composite));
            return !string.Equals(currentParent, target, StringComparison.OrdinalIgnoreCase);
        }

        private void ListView_DragOver(object sender, DragEventArgs e)
        {
            if (PackageDropTarget.IsPackageDrag(e))
                return;
            e.Effect = DragDropEffects.None;
            SetListDropHighlight(null);

            if (!TryGetDragPayload(e.Data, out bool isFolder, out string sourcePath))
                return;

            ListViewItem item = listView1.GetItemAt(listView1.PointToClient(new Point(e.X, e.Y)).X, listView1.PointToClient(new Point(e.X, e.Y)).Y);
            if (item?.Tag is ListViewItemContent content && content.IsFolder)
            {
                string targetFolder = GetFolderPathForListItem(content);
                if (CanDropInto(isFolder, sourcePath, targetFolder))
                {
                    e.Effect = DragDropEffects.Move;
                    SetListDropHighlight(item);
                }
            }
        }

        private void ListView_DragLeave(object sender, EventArgs e)
        {
            SetListDropHighlight(null);
        }

        private void ListView_DragDrop(object sender, DragEventArgs e)
        {
            SetListDropHighlight(null);

            if (!TryGetDragPayload(e.Data, out bool isFolder, out string sourcePath))
                return;

            Point local = listView1.PointToClient(new Point(e.X, e.Y));
            ListViewItem item = listView1.GetItemAt(local.X, local.Y);
            if (!(item?.Tag is ListViewItemContent content) || !content.IsFolder)
                return;

            PerformDrop(isFolder, sourcePath, GetFolderPathForListItem(content));
        }

        private void TreeView_DragOver(object sender, DragEventArgs e)
        {
            if (PackageDropTarget.IsPackageDrag(e))
                return;
            e.Effect = DragDropEffects.None;
            SetTreeDropHighlight(null);

            if (!TryGetDragPayload(e.Data, out bool isFolder, out string sourcePath))
                return;

            Point local = treeView1.PointToClient(new Point(e.X, e.Y));
            TreeNode node = treeView1.GetNodeAt(local);
            if (!TryGetTreeFolderPath(node, out string targetFolder))
                return;

            if (CanDropInto(isFolder, sourcePath, targetFolder))
            {
                e.Effect = DragDropEffects.Move;
                SetTreeDropHighlight(node);
            }
        }

        private void TreeView_DragLeave(object sender, EventArgs e)
        {
            SetTreeDropHighlight(null);
        }

        private void TreeView_DragDrop(object sender, DragEventArgs e)
        {
            SetTreeDropHighlight(null);

            if (!TryGetDragPayload(e.Data, out bool isFolder, out string sourcePath))
                return;

            TreeNode node = treeView1.GetNodeAt(treeView1.PointToClient(new Point(e.X, e.Y)));
            if (!TryGetTreeFolderPath(node, out string targetFolder))
                return;

            PerformDrop(isFolder, sourcePath, targetFolder);
        }

        /* Folder path represented by a tree node - composites resolve to their containing folder */
        private bool TryGetTreeFolderPath(TreeNode node, out string folderPath)
        {
            folderPath = "";
            if (node?.Tag == null || !(node.Tag is TreeItem item))
                return false;

            switch (item.Item_Type)
            {
                case TreeItemType.DIRECTORY:
                    folderPath = NormalisePath(item.String_Value);
                    return true;
                case TreeItemType.EXPORTABLE_FILE:
                    Composite composite = Content.Level.Commands.GetComposite(item.String_Value);
                    if (composite == null)
                        return false;
                    folderPath = NormalisePath(GetCompositeParentFolderPath(composite));
                    return true;
            }
            return false;
        }

        private void PerformDrop(bool isFolder, string sourcePath, string targetFolder)
        {
            if (!CanDropInto(isFolder, sourcePath, targetFolder))
                return;

            if (isFolder)
            {
                MoveFolder(sourcePath, targetFolder);
            }
            else
            {
                Composite composite = Content.Level.Commands.GetComposite(sourcePath.Replace('/', '\\'));
                MoveComposite(composite, targetFolder);
            }
        }

        private void SetListDropHighlight(ListViewItem item)
        {
            if (_dropHighlightItem == item)
                return;

            if (_dropHighlightItem != null && !_dropHighlightItem.ListView.IsDisposed)
                _dropHighlightItem.BackColor = listView1.BackColor;

            _dropHighlightItem = item;
            if (_dropHighlightItem != null)
                _dropHighlightItem.BackColor = SystemColors.Highlight;
        }

        private void SetTreeDropHighlight(TreeNode node)
        {
            if (_dropHighlightNode == node)
                return;

            _dropHighlightNode = node;
            treeView1.SelectedNode = _dropHighlightNode ?? treeView1.SelectedNode;
        }
        #endregion
        private void _renameComposite_FormClosed(object sender, FormClosedEventArgs e)
        {
            _renameComposite = null;
        }
        private void compositeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_addCompositeDialog == null)
                _addCompositeDialog = new AddComposite(this, _currentDisplayFolderPath);

            _addCompositeDialog.Show();
            _addCompositeDialog.OnCompositeAdded += SelectCompositeAndReloadList;
            _addCompositeDialog.FormClosed += addCompositeDialogClosed;
        }
        private void addCompositeDialogClosed(object sender, FormClosedEventArgs e)
        {
            _addCompositeDialog = null;
        }
        private void folderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_addFolderDialog == null)
                _addFolderDialog = new AddFolder(this, _currentDisplayFolderPath);

            _addFolderDialog.Show();
            _addFolderDialog.OnFolderAdded += SelectCompositeAndReloadList;
            _addFolderDialog.FormClosed += addFolderDialogClosed;
        }
        private void addFolderDialogClosed(object sender, FormClosedEventArgs e)
        {
            _addFolderDialog = null;
        }

        private void largeIconsToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            SetViewMode(View.LargeIcon);
        }
        private void smallIconsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetViewMode(View.SmallIcon);
        }
        private void listToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetViewMode(View.List);
        }
        private void tileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetViewMode(View.Tile);
        }
        private void SetViewMode(View view, bool persist = true)
        {
            bool previewsBefore = UsingPreviews;
            listView1.View = view;

            if (persist)
                SettingsManager.SetString(Settings.FileBrowserViewOpt, view.ToString());

            largeIconsToolStripMenuItem.Checked = view == View.LargeIcon;
            listToolStripMenuItem.Checked = view == View.List;

            //Only the large-icon views draw previews; an item keyed to one has no small icon, so the list is rebuilt when that changes
            if (UsingPreviews != previewsBefore)
                ReloadList(false);
        }

        /// <summary>The flat preview list is what the panel shows, and it is in a view that draws from the large image list.</summary>
        private bool UsingPreviews => _mode == CompositeBrowserMode.TreeAndPreview && (listView1.View == View.LargeIcon || listView1.View == View.Tile);

        /* The list the large-icon views draw from: the stock icons, or a derived list of the same icons at
           preview size with the previews behind them. Called on every rebuild, which empties the derived
           list: the previews come back as the list scrolls to them. */
        private void ApplyBrowserPreviewList()
        {
            if (!UsingPreviews)
            {
                if (listView1.LargeImageList != FileBrowserImageListLarge)
                    listView1.LargeImageList = FileBrowserImageListLarge;
                return;
            }

            RecreatePreviewList();
        }

        /* A fresh derived list in place of the current one. Emptying a list preview by preview tells the
           ListView about each removal, and it answers every one by sending itself every item's image again,
           so with a whole level listed that took seconds; a new list is one notice. The ListView is moved
           onto the new one before the old goes: disposing a list it still draws from drops it to no list at
           all, and it lays its items out again for that. */
        private void RecreatePreviewList()
        {
            ImageList old = _previewImageList;
            _previewImageList = null;
            CompositePreviewImages.EnsurePreviewList(FileBrowserImageListLarge, BrowserPreviewSize, ref _previewImageList);
            listView1.LargeImageList = _previewImageList;
            if (old == null)
                return;
            CompositePreviewImages.Release(old);
            old.Dispose();
        }

        /// <summary>The tree preview option changed (menu, another instance, the settings file): the tree follows.</summary>
        public void ApplyCompositePreviewSettings()
        {
            if (IsDisposed)
                return;
            ReloadList();
        }

        /* New previews arrived for some composites: redraw if any of them is on screen */
        private void OnPreviewsChanged(IReadOnlyCollection<ShortGuid> ids)
        {
            if (IsDisposed || ids == null || ids.Count == 0)
                return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnPreviewsChanged(ids)));
                return;
            }

            //The tree shows every composite, so with previews on there it always has something to redraw
            if (CompositePreviewImages.TreesEnabled)
            {
                ReloadList();
                return;
            }
            if (!UsingPreviews)
                return;

            //The lists were put right before this was raised (CompositePreviewImages.Refresh): an item whose
            //preview was replaced shows the new one under the same key, and one whose preview went has lost
            //its key. Either way the item goes back on its icon and is looked at again when it is next on
            //screen, which the fill does in the same tick.
            HashSet<ShortGuid> changed = new HashSet<ShortGuid>(ids);
            bool touched = false;
            foreach (ListViewItem item in listView1.Items)
            {
                if (!(item.Tag is ListViewItemContent content) || content.IsFolder || content.Composite == null || !changed.Contains(content.Composite.shortGUID))
                    continue;
                if (!touched)
                {
                    listView1.BeginUpdate();
                    touched = true;
                }
                if (content.Preview == PreviewState.Shown)
                    item.ImageIndex = content.StockImageIndex;
                content.Preview = PreviewState.NotLooked;
            }
            if (!touched)
                return;
            listView1.EndUpdate();
            _previewFillPending = true;
        }

        #region Flat list previews
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetScrollPos(IntPtr hWnd, int nBar);
        private const int SB_HORZ = 0;
        private const int SB_VERT = 1;

        private void PreviewFillTimer_Tick(object sender, EventArgs e)
        {
            if (IsDisposed || !UsingPreviews || _previewImageList == null || listView1.Items.Count == 0 || !listView1.IsHandleCreated || !listView1.Visible)
                return;

            Point scroll = new Point(GetScrollPos(listView1.Handle, SB_HORZ), GetScrollPos(listView1.Handle, SB_VERT));
            Size client = listView1.ClientSize;
            if (!_previewFillPending && scroll == _previewFillScroll && client == _previewFillClientSize)
                return;

            _previewFillScroll = scroll;
            _previewFillClientSize = client;
            _previewFillPending = FillVisiblePreviews();
        }

        /* Preview the items on screen, then the screen below and the screen above, a few dozen per call so
           the window stays responsive; true when there are more to do. The icon views lay their items out
           in rows in index order, so the first one in range is found by bisection on the item tops rather
           than by hit-testing every item. */
        private bool FillVisiblePreviews()
        {
            int stockCount = FileBrowserImageListLarge.Images.Count;
            if (_previewImageList.Images.Count - stockCount > MaxListPreviews)
                ResetListPreviews();

            int count = listView1.Items.Count;
            int height = listView1.ClientSize.Height;
            int margin = height;   //a screen either side, so a small scroll lands on previews already in

            int first = FirstItemAtOrBelow(-margin);
            if (first < 0)
                return false;

            List<ListViewItem> wanted = new List<ListViewItem>();
            List<ListViewItem> below = new List<ListViewItem>();
            List<ListViewItem> above = new List<ListViewItem>();
            for (int i = first; i < count; i++)
            {
                ListViewItem item = listView1.Items[i];
                Rectangle bounds = item.Bounds;
                if (bounds.Top > height + margin)
                    break;
                if (!(item.Tag is ListViewItemContent content) || content.IsFolder || content.Composite == null || content.Preview != PreviewState.NotLooked)
                    continue;
                if (bounds.Bottom < 0) above.Add(item);
                else if (bounds.Top < height) wanted.Add(item);
                else below.Add(item);
            }
            wanted.AddRange(below);
            wanted.AddRange(above);
            if (wanted.Count == 0)
                return false;

            //One batch into the list, then the items pointed at their previews by index (see AddTransientPreviews)
            int decoded = Math.Min(PreviewDecodesPerTick, wanted.Count);
            List<ShortGuid> ids = new List<ShortGuid>(decoded);
            for (int i = 0; i < decoded; i++)
                ids.Add(((ListViewItemContent)wanted[i].Tag).Composite.shortGUID);
            int[] indices = CompositePreviewImages.AddTransientPreviews(_previewImageList, ids, BrowserPreviewSize);

            listView1.BeginUpdate();
            try
            {
                for (int i = 0; i < decoded; i++)
                {
                    ListViewItemContent content = (ListViewItemContent)wanted[i].Tag;
                    if (indices[i] < 0)
                    {
                        content.Preview = PreviewState.None;
                        continue;
                    }
                    wanted[i].ImageIndex = indices[i];
                    content.Preview = PreviewState.Shown;
                }
            }
            finally
            {
                listView1.EndUpdate();
            }
            return decoded < wanted.Count;
        }

        /* The index of the first item whose top is at or below this client y, or -1 when none is */
        private int FirstItemAtOrBelow(int y)
        {
            int low = 0, high = listView1.Items.Count - 1, found = -1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (listView1.Items[mid].Bounds.Top >= y)
                {
                    found = mid;
                    high = mid - 1;
                }
                else
                {
                    low = mid + 1;
                }
            }
            return found;
        }

        /* The derived list holds as many previews as it may: replace it with an empty one and put every
           captured item back on its icon, so the fill starts over with just what is on screen */
        private void ResetListPreviews()
        {
            RecreatePreviewList();
            listView1.BeginUpdate();
            try
            {
                foreach (ListViewItem item in listView1.Items)
                {
                    if (!(item.Tag is ListViewItemContent content) || content.Preview != PreviewState.Shown)
                        continue;
                    item.ImageIndex = content.StockImageIndex;
                    content.Preview = PreviewState.NotLooked;
                }
            }
            finally
            {
                listView1.EndUpdate();
            }
        }
        #endregion

        private void OnSettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            if (!e.ExternalChange || e.ChangedKeys.Count == 0 || IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ApplyExternalSettings(e.ChangedKeys)));
                return;
            }

            ApplyExternalSettings(e.ChangedKeys);
        }

        private void ApplyExternalSettings(IReadOnlyList<string> changedKeys)
        {
            foreach (string key in changedKeys)
            {
                switch (key)
                {
                    case Settings.CompositeBrowserMode:
                        UpdateDockState();
                        break;
                    case Settings.CompositeBrowserSplitter:
                        ApplySplitterDistance();
                        break;
                    case Settings.FileBrowserViewOpt:
                        if (Enum.TryParse<View>(SettingsManager.GetString(Settings.FileBrowserViewOpt), out View view))
                            SetViewMode(view, persist: false);
                        break;
                    case Settings.CompositePreviewsInTrees:
                        ApplyCompositePreviewSettings();
                        break;
                }
            }
        }

        private void createComposite_Click(object sender, EventArgs e)
        {
            compositeToolStripMenuItem_Click(null, null);
        }
        private void createCompositeViaTreeView_Click(object sender, EventArgs e)
        {
            compositeToolStripMenuItem_Click(null, null);
        }
        private void createFolder_Click(object sender, EventArgs e)
        {
            folderToolStripMenuItem_Click(null, null);
        }
        private void createFolderViaTreeView_Click(object sender, EventArgs e)
        {
            folderToolStripMenuItem_Click(null, null);
        }

        private void entity_search_box_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                //Don't wait out the debounce when someone has actually asked for it
                _searchDebounce?.Stop();
                RunCompositeSearch();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape && entity_search_box.Text.Length != 0)
            {
                entity_search_box.Text = "";
                e.SuppressKeyPress = true;
            }
        }
    }
}
