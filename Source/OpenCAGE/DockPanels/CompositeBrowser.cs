using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.Popups;
using DarkModeForms;
using OpenCAGE;
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

        private TreeUtility _treeUtility = null;
        private CancellationTokenSource _prevTaskToken = null;

        private string _currentDisplayFolderPath = "";

        public CompositeDisplay CompositeDisplay => Singleton.Editor?.CompositeDisplay;

        AddComposite _addCompositeDialog = null;
        AddFolder _addFolderDialog = null;

        private const int MinTreePanelSize = 100;
        private const int DefaultTreePanelSize = 160;
        private int _defaultSplitterDistance = DefaultTreePanelSize;
        private Panel _treeSearchPanel = null;

        private readonly System.Windows.Forms.Timer _treeSelectionDebounceTimer;
        private TreeNode _pendingTreeSelection = null;
        private Point _treeDragStartPoint;
        private bool _treeDragInProgress = false;
        private bool _suppressTreeSelectionDebounce = false;
        private bool _suppressSelectionRestore = false;

        public CompositeBrowser(string levelName)
        {
            InitializeComponent();

            SetupBrowserLayout();

            this.FormClosed += CompositeBrowser_FormClosed;
            this.Load += CompositeBrowser_Load;
            this.VisibleChanged += CompositeBrowser_VisibleChanged;
            this.DockStateChanged += CompositeBrowser_DockStateChanged;
            this.Resize += CompositeBrowser_Resize;

            _content = new LevelContent(levelName);
            _treeUtility = new TreeUtility(treeView1, TreeType.SCRIPTS);

            treeView1.MouseMove += FileTree_MouseMove;

            _treeSelectionDebounceTimer = new System.Windows.Forms.Timer(components) { Interval = 200 };
            _treeSelectionDebounceTimer.Tick += TreeSelectionDebounceTimer_Tick;

            Singleton.OnCompositeRenamed += OnCompositeRenamed;
            SettingsManager.SettingsChanged += OnSettingsChanged;
        }

        private void SetupBrowserLayout()
        {
            splitContainer1.Panel1.Controls.Remove(treeView1);
            splitContainer1.Panel1.Controls.Remove(entity_search_box);
            splitContainer1.Panel1.Controls.Remove(entity_search_btn);

            entity_search_box.Anchor = AnchorStyles.None;
            entity_search_btn.Anchor = AnchorStyles.None;

            _treeSearchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 24,
                Name = "treeSearchPanel",
            };
            _treeSearchPanel.Controls.Add(entity_search_box);
            _treeSearchPanel.Controls.Add(entity_search_btn);
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
            splitContainer1.Panel2.Controls.Remove(pathDisplay);
            splitContainer1.Panel2.Controls.Remove(listView1);

            goBackOnPath.Anchor = AnchorStyles.None;
            pathDisplay.Anchor = AnchorStyles.None;

            Panel pathPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 24,
                Name = "fileBrowserPathPanel",
            };
            pathPanel.Controls.Add(goBackOnPath);
            pathPanel.Controls.Add(pathDisplay);
            pathPanel.Resize += FileBrowserPathPanel_Resize;
            FileBrowserPathPanel_Resize(pathPanel, EventArgs.Empty);

            listView1.Anchor = AnchorStyles.None;
            listView1.Dock = DockStyle.Fill;

            splitContainer1.Panel2.Controls.Add(listView1);
            splitContainer1.Panel2.Controls.Add(pathPanel);
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
            pathDisplay.SetBounds(
                goBackOnPath.Width + 2,
                1,
                Math.Max(0, pathPanel.ClientSize.Width - goBackOnPath.Width - 2),
                22);
        }

        private void LayoutTreeSearchRow()
        {
            if (_treeSearchPanel == null)
                return;

            entity_search_btn.SetBounds(
                Math.Max(0, _treeSearchPanel.ClientSize.Width - entity_search_btn.Width),
                1,
                entity_search_btn.Width,
                20);

            entity_search_box.SetBounds(
                0,
                1,
                Math.Max(0, _treeSearchPanel.ClientSize.Width - entity_search_btn.Width - 2),
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

            _treeSelectionDebounceTimer.Stop();
            _treeSelectionDebounceTimer.Tick -= TreeSelectionDebounceTimer_Tick;
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
        }

        public void SelectCompositeAndReloadList(Composite composite)
        {
            Content.EnsureEditorUtils();
            Content.Level.Commands.Entries = Content.Level.Commands.Entries.OrderBy(o => o.name).ToList();
            ReloadList();
            LoadComposite(composite);
        }

        /* Reload the folder/composite display */
        private void ReloadList(bool updateListViewToo = true)
        {
            if (Content?.Level?.Commands == null || !Content.Level.Commands.Loaded)
                return;

            if (treeView1 == null || treeView1.IsDisposed || _treeUtility == null)
                return;

            if (updateListViewToo)
            {
                Content.EnsureEditorUtils();
                _treeUtility.UpdateFileTree(GetCompositeNamesForTree());
            }

            listView1.Items.Clear();
            pathDisplay.Text = _currentDisplayFolderPath.Replace("/", " > ");
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

            if (!_suppressSelectionRestore)
                RestoreSelectionForLoadedComposite(updateListViewToo);
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

            EditorUtils.CompositeType type = Content.EditorUtils.GetCompositeType(composite);

            ListViewItemContent content = new ListViewItemContent() { IsFolder = isFolder };
            if (isFolder) content.FolderName = folderName ?? text;
            else content.Composite = composite;

            ListViewItem newItem = new ListViewItem()
            {
                Text = text,
                ImageIndex = isFolder ? 1 : type == EditorUtils.CompositeType.IS_ROOT ? 2 : type == EditorUtils.CompositeType.IS_PAUSE_MENU || type == EditorUtils.CompositeType.IS_GLOBAL ? 3 : type == EditorUtils.CompositeType.IS_DISPLAY_MODEL ? 4 : 0,
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

            _suppressSelectionRestore = true;
            try
            {
                if (GetCompositeParentFolderPath(loadedComposite) != _currentDisplayFolderPath)
                {
                    SyncFileBrowserToComposite(loadedComposite);
                    if (updateTreeSelection)
                        SelectCompositeInTree(loadedComposite);
                    return;
                }

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

        /* Enable/disable the file browser UI */
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

            splitContainer1.Panel2Collapsed = !SettingsManager.GetBool(Settings.EnableFileBrowser);
            splitContainer1.FixedPanel = FixedPanel.None;

            ApplySplitterDistance();

            Singleton.Editor.DockPanel.ActiveAutoHideContent = null;
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

            _treeUtility.SelectNode(_currentDisplayFolderPath);
            treeView1.SelectedNode?.Expand();
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

            _currentDisplayFolderPath = GetCompositeParentFolderPath(composite);
            ReloadList(false);
            SelectCompositeInListView(composite);
        }

        private void SelectCompositeInListView(Composite composite)
        {
            if (composite == null)
                return;

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
            for (int i = 0; i < Content.Level.Commands.EntryPoints.Count(); i++)
            {
                if (composite.shortGUID == Content.Level.Commands.EntryPoints[i].shortGUID)
                {
                    MessageBox.Show("Cannot delete a composite which is the root, global, or pause menu!", "Cannot delete.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            if (prompt && MessageBox.Show("Are you sure you want to remove " + Path.GetFileName(composite.name) + "?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            if (CompositeDisplay != null && CompositeDisplay.Composite == composite)
                CloseAllChildTabs();

            //Remove any entities or links that reference this composite
            for (int i = 0; i < Content.Level.Commands.Entries.Count; i++)
            {
                var compositeEntry = Content.Level.Commands.Entries[i];
                var functionsToKeep = new List<FunctionEntity>();
                
                // Collect functions that should be kept (not referencing the deleted composite)
                foreach (var function in compositeEntry.functions)
                {
                    if (function.function == composite.shortGUID) continue;
                    
                    // Prune child links that reference the deleted composite
                    var prunedEntityLinks = new List<EntityConnector>();
                    foreach (var link in function.childLinks)
                    {
                        Entity linkedEntity = compositeEntry.GetEntityByID(link.linkedEntityID);
                        if (linkedEntity != null && linkedEntity.variant == EntityVariant.FUNCTION) 
                        {
                            if (((FunctionEntity)linkedEntity).function == composite.shortGUID) continue;
                        }
                        prunedEntityLinks.Add(link);
                    }
                    function.childLinks = prunedEntityLinks;
                    functionsToKeep.Add(function);
                }
                
                // Clear the functions dictionary and add back only the functions to keep
                compositeEntry.functions_dictionary.Clear();
                foreach (var function in functionsToKeep)
                {
                    compositeEntry.functions_dictionary[function.shortGUID] = function;
                }
            }

            // Remove aliases and proxies that reference the deleted composite
            for (int i = 0; i < Content.Level.Commands.Entries.Count; i++)
            {
                var compositeEntry = Content.Level.Commands.Entries[i];
                var aliasesToRemove = new List<ShortGuid>();
                var proxiesToRemove = new List<ShortGuid>();

                // Check aliases - remove those that can't be resolved after the composite deletion
                foreach (var alias in compositeEntry.aliases)
                {
                    if (!Content.Level.Commands.Utils.CouldResolve(Content.Level.Commands.Utils.ResolveAlias(alias, compositeEntry)))
                    {
                        aliasesToRemove.Add(alias.shortGUID);
                    }
                }

                // Check proxies - remove those that can't be resolved after the composite deletion
                foreach (var proxy in compositeEntry.proxies)
                {
                    if (!Content.Level.Commands.Utils.CouldResolve(Content.Level.Commands.Utils.ResolveProxy(proxy)))
                    {
                        proxiesToRemove.Add(proxy.shortGUID);
                    }
                }

                // Remove the invalid aliases and proxies
                foreach (var aliasGuid in aliasesToRemove)
                {
                    compositeEntry.aliases_dictionary.Remove(aliasGuid);
                }
                foreach (var proxyGuid in proxiesToRemove)
                {
                    compositeEntry.proxies_dictionary.Remove(proxyGuid);
                }
            }

            //Remove the composite
            Content.Level.Commands.Entries.Remove(composite);
            Content.Level.Commands.Utils.PurgedComposites.purged.Clear(); //TODO: we should smartly remove from this list, rather than removing all

            //Refresh UI
            ReloadList();
            Content.EditorUtils.GenerateCompositeInstances(Content.Level.Commands);

            Singleton.OnCompositeDeleted?.Invoke(composite);
        }

        private string _currentSearch = "";
        private void entity_search_btn_Click(object sender, EventArgs e)
        {
            string newSearch = entity_search_box.Text.Replace('\\', '/').ToUpper().Replace(" ", "");
            if (newSearch == _currentSearch) return;

            List<string> filteredCompositeNames = new List<string>();
            List<Composite> filteredComposites = new List<Composite>();
            _currentSearch = newSearch;
            for (int i = 0; i < Content.Level.Commands.Entries.Count; i++)
            {
                string name = Content.Level.Commands.Entries[i].name.Replace('\\', '/');

                if (SettingsManager.GetBool(Settings.CompNameOnlyOpt) == true)
                {
                    string[] nameSplit = name.Split('/');
                    name = nameSplit[nameSplit.Length - 1];
                }

                if (!name.ToUpper().Replace(" ", "").Contains(_currentSearch)) continue;

                filteredCompositeNames.Add(Content.Level.Commands.Entries[i].name.Replace('\\', '/'));
                filteredComposites.Add(Content.Level.Commands.Entries[i]);
            }

            _treeUtility.UpdateFileTree(filteredCompositeNames);

            if (entity_search_box.Text != "")
            {
                treeView1.ExpandAll();

                /*
                listView1.Items.Clear();
                pathDisplay.Text = "";
                foreach (Composite composite in filteredComposites)
                {
                    bool isRoot = Content.Level.Commands.EntryPoints[0] == composite;
                    listView1.Items.Add(new ListViewItem()
                    {
                        Text = Path.GetFileName(composite.name),
                        ImageIndex = isRoot ? 2 : 0,
                        Tag = new ListViewItemContent() { IsFolder = false, Composite = composite }
                    });
                }
                */
            }
            else
            {
                ReloadList();
            }
        }

        /* File Browser Context Menu */
        private void FooListView_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var lv = sender as System.Windows.Forms.ListView;
                var item = lv.HitTest(e.Location).Item;

                Composite comp = item != null && item.Tag != null ? ((ListViewItemContent)item.Tag).Composite : null;
                deleteFolderToolStripMenuItem.Enabled = comp != null && !Content.Level.Commands.EntryPoints.Contains(comp);
                renameToolStripMenuItem.Enabled = comp != null && (Content.Level.Commands.EntryPoints[0] == comp || !Content.Level.Commands.EntryPoints.Contains(comp));
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
            if (SettingsManager.GetBool(Settings.EnableFileBrowser))
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

                Composite comp = _rightClickedNode != null && _rightClickedNode.Tag != null ? Content.Level.Commands.GetComposite(((TreeItem)_rightClickedNode.Tag).String_Value) : null;
                toolStripMenuItem4.Enabled = comp != null && !Content.Level.Commands.EntryPoints.Contains(comp);
                toolStripMenuItem5.Enabled = comp != null && (Content.Level.Commands.EntryPoints[0] == comp || !Content.Level.Commands.EntryPoints.Contains(comp));
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
            if (SettingsManager.GetBool(Settings.EnableFileBrowser))
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

            try
            {
                DoDragDrop(data, DragDropEffects.Copy);
            }
            finally
            {
                _treeDragInProgress = false;
            }
        }

        private void deleteFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count != 1) return;

            ListViewItem item = listView1.SelectedItems[0];
            ListViewItemContent content = (ListViewItemContent)item.Tag;
            if (content.IsFolder)
            {
                //TODO
                MessageBox.Show("Support for deleting folders is coming soon.");
                return;

                string folderFullPath = "";
                if (_currentDisplayFolderPath == "") folderFullPath = content.FolderName;
                else folderFullPath = _currentDisplayFolderPath + "/" + content.FolderName;

                List<Composite> toDelete = new List<Composite>();
                for (int i = 0; i < Content.Level.Commands.Entries.Count; i++)
                    if (Content.Level.Commands.Entries[i].name.Length >= folderFullPath.Length && Content.Level.Commands.Entries[i].name.Substring(0, folderFullPath.Length) == folderFullPath)
                        toDelete.Add(Content.Level.Commands.Entries[i]);

                if (MessageBox.Show("Are you sure you want to delete this folder, including the " + toDelete.Count + " composites it contains?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                for (int i = 0; i < toDelete.Count; i++)
                    DeleteComposite(toDelete[i], false);
            }
            else
            {
                DeleteComposite(content.Composite);
            }

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
                    //TODO
                    MessageBox.Show("Support for deleting folders is coming soon.");
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
                //TODO
                MessageBox.Show("Support for renaming folders is coming soon.");
            }
            else
            {
                RenameComposite(content.Composite);
            }
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
                    //TODO
                    MessageBox.Show("Support for renaming folders is coming soon.");
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
            listView1.View = view;

            if (persist)
                SettingsManager.SetString(Settings.FileBrowserViewOpt, view.ToString());

            largeIconsToolStripMenuItem.Checked = view == View.LargeIcon;
            listToolStripMenuItem.Checked = view == View.List;
        }

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
                    case Settings.EnableFileBrowser:
                        UpdateDockState();
                        break;
                    case Settings.CompositeBrowserSplitter:
                        ApplySplitterDistance();
                        break;
                    case Settings.FileBrowserViewOpt:
                        if (Enum.TryParse<View>(SettingsManager.GetString(Settings.FileBrowserViewOpt), out View view))
                            SetViewMode(view, persist: false);
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
                entity_search_btn.PerformClick();
                e.SuppressKeyPress = true;
            }
        }
    }
}
