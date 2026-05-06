using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CommandsEditor.Popups;
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
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Xml.Linq;
using WebSocketSharp;
using WeifenLuo.WinFormsUI.Docking;
using ListViewItem = System.Windows.Forms.ListViewItem;

namespace CommandsEditor.DockPanels
{
    public partial class CommandsDisplay : DockContent
    {
        private LevelContent _content;
        public LevelContent Content => _content;

        private TreeUtility _treeUtility = null;
        private CancellationTokenSource _prevTaskToken = null;

        private string _currentDisplayFolderPath = "";

        private CompositeDisplay _compositeDisplay = null;
        public CompositeDisplay CompositeDisplay => _compositeDisplay;

        private Composite3D _renderer = null;

        AddComposite _addCompositeDialog = null;
        AddFolder _addFolderDialog = null;

        private int _defaultSplitterDistance = 324;

        public CommandsDisplay(string levelName)
        {
            InitializeComponent();

            //this.Text = levelName;
            this.FormClosed += CommandsDisplay_FormClosed;
            this.Load += CommandsDisplay_Load;

            _content = new LevelContent(levelName);
            _treeUtility = new TreeUtility(treeView1, TreeType.SCRIPTS);

            Singleton.OnCompositeRenamed += OnCompositeRenamed;
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

        private void CommandsDisplay_Load(object sender, EventArgs e)
        {
            if (Enum.TryParse<View>(SettingsManager.GetString(Singleton.Settings.FileBrowserViewOpt), out View view))
                SetViewMode(view);

            //TODO: these utils should be moved into LevelContent and made less generic. makes no sense anymore.
            _content.EditorUtils = new EditorUtils(_content);
            Task.Factory.StartNew(() => _content.EditorUtils.GenerateEntityNameCache(Singleton.Editor));
            Content.EditorUtils.GenerateCompositeInstances(Content.Level.Commands);

            SelectCompositeAndReloadList(Content.Level.Commands.EntryPoints[0]);
            //Singleton.OnCompositeSelected?.Invoke(Content.Level.Commands.EntryPoints[0]); //need to call this again b/c the activation event doesn't fire here

            Task.Factory.StartNew(() => EnumStringListViewItems.PopulateGlobalEntries());
            Task.Factory.StartNew(() => EnumStringListViewItems.PopulateLevelSpecificEntries());
        }

        private void CommandsDisplay_FormClosed(object sender, FormClosedEventArgs e)
        {
            this.FormClosed -= CommandsDisplay_FormClosed;
            this.Load -= CommandsDisplay_Load;
            Singleton.OnCompositeRenamed -= OnCompositeRenamed;

            if (_compositeDisplay != null)
                _compositeDisplay.FormClosing -= CompositeDisplay_FormClosing;
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
            if (_globalEntitySearch != null)
            {
                _globalEntitySearch.OnEntitySelected -= LoadCompositeAndEntity;
                _globalEntitySearch.FormClosed -= _globalEntitySearch_FormClosed;
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

            _compositeDisplay?.Close();
            _renderer?.Close();

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
            Content.Level.Commands.Entries = Content.Level.Commands.Entries.OrderBy(o => o.name).ToList();
            ReloadList();
            SelectComposite(composite);
        }

        /* Reload the folder/composite display */
        private void ReloadList(bool updateListViewToo = true)
        {
            if (updateListViewToo)
            {
                _treeUtility.UpdateFileTree(Content.Level.Commands.GetCompositeNames().ToList());
            }

            listView1.Items.Clear();
            pathDisplay.Text = _currentDisplayFolderPath.Replace("/", " > ");
            string[] currentPathSplit = _currentDisplayFolderPath.Split('/');
            bool currentPathIsRoot = currentPathSplit.Length == 1 && currentPathSplit[0] == "";

            List<string> items = new List<string>();
            foreach (Composite composite in Content.Level.Commands.Entries)
            {
                //Make sure this folder/composite should be visible at the current folder path
                string name = composite.name.Replace('\\', '/');
                string[] nameSplit = name.Split('/');
                bool shouldAdd = true;
                if (!currentPathIsRoot)
                {
                    for (int i = 0; i < currentPathSplit.Length; i++)
                    {
                        if (i >= nameSplit.Length) break;
                        if (currentPathSplit[i] != nameSplit[i])
                        {
                            shouldAdd = false;
                            break;
                        }
                    }
                }
                if (!shouldAdd) continue;

                //Get formatting
                bool isFolder = nameSplit.Length > (currentPathIsRoot ? currentPathSplit.Length : currentPathSplit.Length + 1);
                string text = nameSplit[currentPathIsRoot ? 0 : currentPathSplit.Length];
                if (text == "") continue;

                EditorUtils.CompositeType type = Content.EditorUtils.GetCompositeType(composite);

                //Make sure this hasn't already been added
                if (items.Contains(text)) continue;
                items.Add(text);

                //Add it to the view
                ListViewItemContent content = new ListViewItemContent() { IsFolder = isFolder };
                if (isFolder) content.FolderName = text;
                else content.Composite = composite;
                listView1.Items.Add(new ListViewItem()
                {
                    Text = text,
                    ImageIndex = isFolder ? 1 : type == EditorUtils.CompositeType.IS_ROOT ? 2 : type == EditorUtils.CompositeType.IS_PAUSE_MENU || type == EditorUtils.CompositeType.IS_GLOBAL ? 3 : type == EditorUtils.CompositeType.IS_DISPLAY_MODEL ? 4 : 0,
                    Tag = content
                });
            }
        }

        /* Enable/disable the file browser UI */
        public void UpdateDockState()
        {
            DockAreas = DockAreas.DockBottom | DockAreas.DockLeft;
            if (SettingsManager.GetBool(Singleton.Settings.AutoHideCompositeDisplay))
                Show(Singleton.Editor.DockPanel, SettingsManager.GetBool(Singleton.Settings.EnableFileBrowser) ? DockState.DockBottomAutoHide : DockState.DockLeftAutoHide);
            else
                Show(Singleton.Editor.DockPanel, SettingsManager.GetBool(Singleton.Settings.EnableFileBrowser) ? DockState.DockBottom : DockState.DockLeft);
            DockAreas = SettingsManager.GetBool(Singleton.Settings.EnableFileBrowser) ? DockAreas.DockBottom : DockAreas.DockLeft;

            splitContainer1.Panel2Collapsed = !SettingsManager.GetBool(Singleton.Settings.EnableFileBrowser);
            splitContainer1.FixedPanel = FixedPanel.Panel1;

            try
            {
                splitContainer1.SplitterDistance = SettingsManager.GetInteger(Singleton.Settings.CompositeSplitWidth, _defaultSplitterDistance);
            }
            catch { }

            if (SettingsManager.GetBool(Singleton.Settings.AutoHideCompositeDisplay))
                Singleton.Editor.DockPanel.ActiveAutoHideContent = this;
            else
                Singleton.Editor.DockPanel.ActiveAutoHideContent = null;
        }

        public void ResetSplitter()
        {
            splitContainer1.SplitterDistance = _defaultSplitterDistance;
        }

        //UI: handle saving split container width between commands/runs 
        private void treeView1_Resize(object sender, EventArgs e)
        {
            SettingsManager.SetInteger(Singleton.Settings.CompositeSplitWidth, splitContainer1.SplitterDistance);
        }

        /* File browser: select folder/composite */
        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
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
            if (treeView1.SelectedNode == null) return;

            TreeItem item = (TreeItem)treeView1.SelectedNode.Tag;
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
            _treeUtility.SelectNode(composite.name);

            //TODO: select in file viewer too
            //_currentDisplayFolderPath = composite.name;

            this.BringToFront();
            this.Focus();
        }

        public void CloseAllChildTabs()
        {
            _compositeDisplay?.DepopulateUI();
        }

        public void ReloadAllEntities()
        {
            _compositeDisplay?.ReloadAllEntities();
        }

        public void Reload(bool alsoReloadEntities = true)
        {
            _compositeDisplay?.Reload(alsoReloadEntities);
        }

        public CompositeDisplay LoadComposite(string name)
        {
            return LoadComposite(Content.Level.Commands.GetComposite(name));
        }
        public CompositeDisplay LoadComposite(ShortGuid guid)
        {
            return LoadComposite(Content.Level.Commands.GetComposite(guid));
        }
        public CompositeDisplay LoadComposite(Composite composite)
        {
            if (composite == null)
                return null;

            if (_compositeDisplay?.Composite == composite)
                return _compositeDisplay;

            if (_compositeDisplay == null)
            {
                _compositeDisplay = new CompositeDisplay(this);
                _compositeDisplay.Show(Singleton.Editor.DockPanel, DockState.Document);
                _compositeDisplay.FormClosing += CompositeDisplay_FormClosing;
            }
            _compositeDisplay.PopulateUI(composite);

#if DEBUG
            //if (_renderer != null) _renderer.Close();
            //_renderer = new Composite3D(_compositeDisplay);
            //_renderer.Show(Singleton.Editor.DockPanel, DockState.DockRight);
#endif

            SelectComposite(composite);
            return _compositeDisplay;
        }
        private void CompositeDisplay_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            CompositeDisplay display = (CompositeDisplay)sender;
            display.DepopulateUI();
        }

        public void LoadCompositeAndEntity(ShortGuid compositeGUID, ShortGuid entityGUID)
        {
            Composite composite = Content.Level.Commands.GetComposite(compositeGUID);
            LoadCompositeAndEntity(composite, composite?.GetEntityByID(entityGUID));
        }
        public void LoadCompositeAndEntity(Composite composite, Entity entity)
        {
            CompositeDisplay panel = LoadComposite(composite);
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

            if (_compositeDisplay != null && _compositeDisplay.Composite == composite)
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

                if (SettingsManager.GetBool(Singleton.Settings.CompNameOnlyOpt) == true)
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
                //ReloadList();
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

                if (item != null)
                    lv.FocusedItem = item;

                FileBrowserContextMenu.Show(lv, e.Location);
            }
        }
        TreeNode _rightClickedNode = null;
        private void FileTree_MouseDown(object sender, MouseEventArgs e)
        {
            if (SettingsManager.GetBool(Singleton.Settings.EnableFileBrowser))
                return;

            if (e.Button == MouseButtons.Right)
            {
                var lv = sender as System.Windows.Forms.TreeView;
                _rightClickedNode = lv.HitTest(e.Location).Node;

                Composite comp = _rightClickedNode != null && _rightClickedNode.Tag != null ? Content.Level.Commands.GetComposite(((TreeItem)_rightClickedNode.Tag).String_Value) : null;
                toolStripMenuItem4.Enabled = comp != null && !Content.Level.Commands.EntryPoints.Contains(comp);
                toolStripMenuItem5.Enabled = comp != null && (Content.Level.Commands.EntryPoints[0] == comp || !Content.Level.Commands.EntryPoints.Contains(comp));

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

            _compositeDisplay?.Reload();
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
        private void SetViewMode(View view)
        {
            listView1.View = view;

            SettingsManager.SetString(Singleton.Settings.FileBrowserViewOpt, view.ToString());

            largeIconsToolStripMenuItem.Checked = view == View.LargeIcon;
            listToolStripMenuItem.Checked = view == View.List;
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

        private void findFunctionUses_Click(object sender, EventArgs e)
        {
            ShowSearchWindow(GlobalEntitySearcher.SearchMode.BY_FUNCTION);
        }
        private void findNameUses_Click(object sender, EventArgs e)
        {
            ShowSearchWindow(GlobalEntitySearcher.SearchMode.BY_NAME);
        }

        GlobalEntitySearcher _globalEntitySearch = null;
        private void ShowSearchWindow(GlobalEntitySearcher.SearchMode mode)
        {
            if (_globalEntitySearch != null)
            {
                _globalEntitySearch.Focus();
                _globalEntitySearch.BringToFront();
                return;
            }

            _globalEntitySearch = new GlobalEntitySearcher(mode);
            _globalEntitySearch.Show();
            _globalEntitySearch.OnEntitySelected += LoadCompositeAndEntity;
            _globalEntitySearch.FormClosed += _globalEntitySearch_FormClosed;
        }
        private void _globalEntitySearch_FormClosed(object sender, FormClosedEventArgs e)
        {
            _globalEntitySearch = null;
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
