using CATHODE.Scripting;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.Popups.UserControls
{
    /// <summary>
    /// Composites in a TreeView, laid out as the folders their names spell out, with a checkbox on
    /// each. Ticking a composite also ticks every composite it instances, all the way down, greyed
    /// and locked: a port always brings those along, so the picker shows what will actually move and
    /// counts it. Ticking a folder ticks what is in it. With <see cref="ReadOnly"/> set there are no
    /// checkboxes and the tree just shows what is there. Icons are the composite browser's.
    /// </summary>
    /// <remarks>
    /// The selection is two sets: <see cref="Ticked"/>, what the user chose, and <see cref="Implied"/>,
    /// what those instance (transitively) and that is not itself ticked. Callers port <b>Ticked</b> with
    /// recursion on - the porter finds the rest for itself - and show both in their summaries.
    /// </remarks>
    public class CompositeTree
    {
        public enum ItemKind
        {
            Composite,
            Root,
            /// <summary>GLOBAL and PAUSEMENU.</summary>
            System,
            DisplayModel,
        }

        public class Item
        {
            public ShortGuid Id;
            public string Name;
            public ItemKind Kind = ItemKind.Composite;
            /// <summary>Shown after the name, e.g. "(already in this level)".</summary>
            public string Note;
        }

        //The browser's tree icons, in its order
        private const int IconFolder = 0, IconComposite = 1, IconFolderOpen = 2, IconRoot = 3, IconSystem = 4, IconDisplayModel = 5;
        private static ImageList _icons;

        private static readonly ShortGuid GlobalId = new ShortGuid("1D-2E-CE-E5");
        private static readonly ShortGuid PauseMenuId = new ShortGuid("FE-7B-FE-B3");

        private readonly TreeView _view;
        private readonly DoubleClickGuard _guard;
        private readonly Dictionary<ShortGuid, Item> _items = new Dictionary<ShortGuid, Item>();
        private readonly Dictionary<ShortGuid, TreeNode> _leaves = new Dictionary<ShortGuid, TreeNode>();
        private readonly HashSet<ShortGuid> _ticked = new HashSet<ShortGuid>();
        private HashSet<ShortGuid> _implied = new HashSet<ShortGuid>();
        //Ticked or implied the last time the tree opened folders for the selection
        private HashSet<ShortGuid> _revealed = new HashSet<ShortGuid>();
        private Func<ShortGuid, IEnumerable<ShortGuid>> _nested = id => Enumerable.Empty<ShortGuid>();
        private string _filter = "";
        private bool _updating;
        private bool _readOnly;

        public event Action SelectionChanged;

        public CompositeTree(TreeView view)
        {
            _view = view;
            _view.CheckBoxes = true;
            _view.HideSelection = false;
            _view.ShowNodeToolTips = false;
            _view.PathSeparator = "\\";
            _view.ImageList = Icons;
            _view.BeforeCheck += View_BeforeCheck;
            _view.AfterCheck += View_AfterCheck;
            _view.AfterExpand += (s, e) => SetFolderIcon(e.Node, true);
            _view.AfterCollapse += (s, e) => SetFolderIcon(e.Node, false);
            //Belt and braces behind the guard: whatever a double-click did, the display follows the selection
            _view.NodeMouseDoubleClick += (s, e) => { if (!_readOnly) ApplyStates(); };
            _guard = new DoubleClickGuard(_view);
        }

        /// <summary>The composite browser's icon set: folder, composite, open folder, root, GLOBAL/PAUSEMENU, DisplayModel.</summary>
        public static ImageList Icons
        {
            get
            {
                if (_icons == null)
                {
                    _icons = new ImageList() { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(16, 16) };
                    _icons.Images.Add(Properties.Resources.Folder_Icon.ToBitmap());
                    _icons.Images.Add(Properties.Resources.d_Prefab_Icon.ToBitmap());
                    _icons.Images.Add(Properties.Resources.FolderOpened_Icon.ToBitmap());
                    _icons.Images.Add(Properties.Resources.globe.ToBitmap());
                    _icons.Images.Add(Properties.Resources.cog.ToBitmap());
                    _icons.Images.Add(Properties.Resources.Avatar_Icon.ToBitmap());
                }
                return _icons;
            }
        }

        /// <summary>What a composite is, from what a picker can know without the level loaded.</summary>
        public static ItemKind KindOf(ShortGuid id, string name, bool isRoot)
        {
            if (isRoot) return ItemKind.Root;
            if (id == GlobalId || id == PauseMenuId) return ItemKind.System;
            string n = (name ?? "").Replace('/', '\\');
            if (n.StartsWith("DisplayModel:", StringComparison.OrdinalIgnoreCase)) return ItemKind.DisplayModel;
            return ItemKind.Composite;
        }

        public static ItemKind KindOf(EditorUtils.CompositeType type)
        {
            switch (type)
            {
                case EditorUtils.CompositeType.IS_ROOT: return ItemKind.Root;
                case EditorUtils.CompositeType.IS_GLOBAL:
                case EditorUtils.CompositeType.IS_PAUSE_MENU: return ItemKind.System;
                case EditorUtils.CompositeType.IS_DISPLAY_MODEL: return ItemKind.DisplayModel;
                default: return ItemKind.Composite;
            }
        }

        /// <summary>No checkboxes: the tree just shows what is there, and nothing is selected.</summary>
        public bool ReadOnly
        {
            get => _readOnly;
            set
            {
                _readOnly = value;
                _view.CheckBoxes = !value;
            }
        }

        /// <summary>The composites the user ticked.</summary>
        public IReadOnlyCollection<ShortGuid> Ticked => _ticked;
        /// <summary>The composites those instance (all the way down) that are not themselves ticked.</summary>
        public IReadOnlyCollection<ShortGuid> Implied => _implied;
        public int Count => _items.Count;

        /// <summary>
        /// Show these composites. <paramref name="nested"/> answers which composites one instances
        /// directly; the tree follows it as far as it goes. <paramref name="ticked"/> starts ticked.
        /// </summary>
        public void Load(IEnumerable<Item> items, Func<ShortGuid, IEnumerable<ShortGuid>> nested, IEnumerable<ShortGuid> ticked = null)
        {
            _items.Clear();
            if (items != null)
            {
                foreach (Item item in items)
                    if (item != null && !_items.ContainsKey(item.Id))
                        _items[item.Id] = item;
            }
            _nested = nested ?? (id => Enumerable.Empty<ShortGuid>());
            _ticked.Clear();
            if (ticked != null)
            {
                foreach (ShortGuid id in ticked)
                    if (_items.ContainsKey(id))
                        _ticked.Add(id);
            }
            ComputeImplied();
            Rebuild();
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Only composites whose name contains this are shown (folders kept for what matches). Applied
        /// a moment after the last change, since a rebuild of a big level's tree is not free and the
        /// value usually arrives one keystroke at a time.
        /// </summary>
        public string Filter
        {
            get => _filter;
            set
            {
                _pendingFilter = (value ?? "").Trim();
                if (_pendingFilter == _filter)
                {
                    _filterTimer?.Stop();
                    return;
                }
                if (_filterTimer == null)
                {
                    _filterTimer = new Timer() { Interval = 200 };
                    _filterTimer.Tick += (s, e) => ApplyPendingFilter();
                }
                _filterTimer.Stop();
                _filterTimer.Start();
            }
        }

        private string _pendingFilter;
        private Timer _filterTimer;

        /// <summary>Apply a filter that is still waiting on the timer.</summary>
        public void ApplyPendingFilter()
        {
            _filterTimer?.Stop();
            if (_pendingFilter == null || _pendingFilter == _filter)
                return;
            _filter = _pendingFilter;
            Rebuild();
        }

        /// <summary>Tick or untick one composite, as a click on its checkbox would.</summary>
        public void SetTicked(ShortGuid id, bool check)
        {
            if (_readOnly || !_items.ContainsKey(id))
                return;
            if (check) _ticked.Add(id);
            else _ticked.Remove(id);
            ComputeImplied();
            ApplyStates();
            SelectionChanged?.Invoke();
        }

        /// <summary>Untick everything, whether the filter is showing it or not.</summary>
        public void UntickAll()
        {
            _ticked.Clear();
            ComputeImplied();
            ApplyStates();
            SelectionChanged?.Invoke();
        }

        /// <summary>Tick or untick every composite the tree is currently showing (with a filter typed, just the matches).</summary>
        public void SetShown(bool check)
        {
            foreach (TreeNode leaf in _leaves.Values)
            {
                ShortGuid id = ((Item)leaf.Tag).Id;
                if (check) _ticked.Add(id);
                else _ticked.Remove(id);
            }
            ComputeImplied();
            ApplyStates();
            SelectionChanged?.Invoke();
        }

        public string Summary()
        {
            if (_readOnly)
                return Count + " composite" + (Count == 1 ? "" : "s");
            if (_ticked.Count == 0)
                return "Nothing selected";
            int total = _ticked.Count + _implied.Count;
            string text = total + " composite" + (total == 1 ? "" : "s") + " selected";
            if (_implied.Count != 0)
                text += " (" + _ticked.Count + " ticked, " + _implied.Count + " instanced by those)";
            return text;
        }

        /// <summary>The name of a composite the tree knows, or null.</summary>
        public string NameOf(ShortGuid id)
        {
            return _items.TryGetValue(id, out Item item) ? item.Name : null;
        }

        #region SELECTION
        /* Everything the ticked composites instance, however deep, that is not ticked itself */
        private void ComputeImplied()
        {
            HashSet<ShortGuid> implied = new HashSet<ShortGuid>();
            HashSet<ShortGuid> seen = new HashSet<ShortGuid>(_ticked);
            Stack<ShortGuid> pending = new Stack<ShortGuid>(_ticked);
            while (pending.Count > 0)
            {
                ShortGuid id = pending.Pop();
                IEnumerable<ShortGuid> children;
                try { children = _nested(id); }
                catch { children = null; }
                if (children == null)
                    continue;
                foreach (ShortGuid child in children)
                {
                    if (!_items.ContainsKey(child) || !seen.Add(child))
                        continue;
                    implied.Add(child);
                    pending.Push(child);
                }
            }
            _implied = implied;
        }

        private void View_BeforeCheck(object sender, TreeViewCancelEventArgs e)
        {
            if (_updating)
                return;
            if (_readOnly)
            {
                e.Cancel = true;
                return;
            }
            //Something a ticked composite instances comes along regardless: it cannot be unticked
            if (e.Node.Checked && e.Node.Tag is Item item && _implied.Contains(item.Id) && !_ticked.Contains(item.Id))
                e.Cancel = true;
        }

        private void View_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_updating || _readOnly)
                return;

            if (e.Node.Tag is Item item)
            {
                if (e.Node.Checked) _ticked.Add(item.Id);
                else _ticked.Remove(item.Id);
            }
            else
            {
                foreach (TreeNode leaf in LeavesUnder(e.Node))
                {
                    ShortGuid id = ((Item)leaf.Tag).Id;
                    if (e.Node.Checked) _ticked.Add(id);
                    else _ticked.Remove(id);
                    //The user ticked the folder as they see it, open or closed: nothing under it needs revealing
                    _revealed.Add(id);
                }
            }

            ComputeImplied();
            ApplyStates();
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// The TreeView flips a checkbox on the second click of a double-click without asking
        /// BeforeCheck, so a locked (implied) composite could be unticked that way. The double-click
        /// never reaches the control when it lands on a checkbox.
        /// </summary>
        private sealed class DoubleClickGuard : NativeWindow
        {
            private const int WM_LBUTTONDBLCLK = 0x0203;
            private readonly TreeView _tree;

            public DoubleClickGuard(TreeView tree)
            {
                _tree = tree;
                if (tree.IsHandleCreated)
                    Attach();
                tree.HandleCreated += (s, e) => Attach();
                tree.HandleDestroyed += (s, e) => ReleaseHandle();
            }

            private void Attach()
            {
                if (Handle != IntPtr.Zero)
                    ReleaseHandle();
                AssignHandle(_tree.Handle);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_LBUTTONDBLCLK && _tree.CheckBoxes)
                {
                    long lParam = m.LParam.ToInt64();
                    int x = (short)(lParam & 0xFFFF);
                    int y = (short)((lParam >> 16) & 0xFFFF);
                    TreeViewHitTestInfo hit = _tree.HitTest(x, y);
                    if (hit.Location == TreeViewHitTestLocations.StateImage)
                    {
                        m.Result = IntPtr.Zero;
                        return;
                    }
                }
                base.WndProc(ref m);
            }
        }
        #endregion

        #region TREE
        private void Rebuild()
        {
            _updating = true;
            _view.BeginUpdate();
            try
            {
                _view.Nodes.Clear();
                _leaves.Clear();

                //Previews on: the tree draws from a taller list derived from the icons, and a composite with a preview uses it
                bool previews = CompositePreviewImages.TreesEnabled;
                CompositePreviewImages.ApplyToTree(_view, previews);
                //Every preview the leaves will ask for, into the list as one batch (see EnsurePreviews)
                if (previews)
                    CompositePreviewImages.EnsurePreviews(_view.ImageList, _items.Keys, CompositePreviewImages.TreeSize);

                Dictionary<string, TreeNode> folders = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);
                foreach (Item item in _items.Values.OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase))
                {
                    string name = item.Name ?? "";
                    if (_filter.Length != 0)
                    {
                        bool match = true;
                        string[] filterParts = _filter.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string filterPart in filterParts)
                        {
                            if (name.IndexOf(filterPart, StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                match = false;
                                break;
                            }
                        }
                        if (!match) continue;
                    }

                    string[] parts = name.Replace('/', '\\').Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0)
                        parts = new[] { name };

                    TreeNodeCollection level = _view.Nodes;
                    string path = "";
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        path = path.Length == 0 ? parts[i] : path + "\\" + parts[i];
                        if (!folders.TryGetValue(path, out TreeNode folder))
                        {
                            folder = new TreeNode(parts[i], IconFolder, IconFolder);
                            folders[path] = folder;
                            level.Add(folder);
                        }
                        level = folder.Nodes;
                    }

                    string leafText = parts[parts.Length - 1];
                    if (!string.IsNullOrEmpty(item.Note))
                        leafText += "   " + item.Note;
                    int icon = IconFor(item.Kind);
                    string previewKey = previews ? CompositePreviewImages.EnsurePreview(_view.ImageList, item.Id, CompositePreviewImages.TreeSize) : null;
                    TreeNode leaf = previewKey != null
                        ? new TreeNode(leafText) { ImageKey = previewKey, SelectedImageKey = previewKey, Tag = item }
                        : new TreeNode(leafText, icon, icon) { Tag = item };
                    level.Add(leaf);
                    _leaves[item.Id] = leaf;
                }

                SortFoldersFirst(_view.Nodes);
                ApplyStatesCore();

                //Everything open, and the list back at the top (ExpandAll leaves it scrolled to the end)
                _view.ExpandAll();
                if (_view.Nodes.Count > 0)
                    _view.Nodes[0].EnsureVisible();
                _revealed = new HashSet<ShortGuid>(_ticked);
                _revealed.UnionWith(_implied);
                SyncFolderIcons();
            }
            finally
            {
                _view.EndUpdate();
                _updating = false;
            }
        }

        private static int IconFor(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Root: return IconRoot;
                case ItemKind.System: return IconSystem;
                case ItemKind.DisplayModel: return IconDisplayModel;
                default: return IconComposite;
            }
        }

        private static void SetFolderIcon(TreeNode node, bool open)
        {
            if (node == null || node.Tag != null)
                return;
            int icon = open ? IconFolderOpen : IconFolder;
            if (node.ImageIndex != icon)
            {
                node.ImageIndex = icon;
                node.SelectedImageIndex = icon;
            }
        }

        /* Folder icons follow the expanded state however a folder came to be open: a programmatic
           expand need not raise AfterExpand */
        private void SyncFolderIcons()
        {
            foreach (TreeNode folder in AllFolders(_view.Nodes))
                SetFolderIcon(folder, folder.IsExpanded);
        }

        private static IEnumerable<TreeNode> AllFolders(TreeNodeCollection nodes)
        {
            foreach (TreeNode child in nodes)
            {
                if (child.Tag != null)
                    continue;
                yield return child;
                foreach (TreeNode inner in AllFolders(child.Nodes))
                    yield return inner;
            }
        }

        /* Folders above composites, each alphabetical - the way a file browser reads */
        private static void SortFoldersFirst(TreeNodeCollection nodes)
        {
            if (nodes.Count == 0)
                return;
            List<TreeNode> ordered = nodes.Cast<TreeNode>()
                .OrderBy(o => o.Tag != null)
                .ThenBy(o => o.Text, StringComparer.OrdinalIgnoreCase)
                .ToList();
            nodes.Clear();
            nodes.AddRange(ordered.ToArray());
            foreach (TreeNode node in ordered)
                if (node.Tag == null)
                    SortFoldersFirst(node.Nodes);
        }

        private void ApplyStates()
        {
            _updating = true;
            _view.BeginUpdate();
            try
            {
                ApplyStatesCore();
                ExpandSelected();
                SyncFolderIcons();
            }
            finally
            {
                _view.EndUpdate();
                _updating = false;
            }
        }

        /* Leaves: checked when ticked or implied, grey when only implied. Folders: checked when every
           composite in them is. Must run with _updating set, since setting Checked raises AfterCheck. */
        private void ApplyStatesCore()
        {
            if (_readOnly)
                return;
            foreach (KeyValuePair<ShortGuid, TreeNode> pair in _leaves)
            {
                bool ticked = _ticked.Contains(pair.Key);
                bool implied = _implied.Contains(pair.Key);
                bool on = ticked || implied;
                if (pair.Value.Checked != on)
                    pair.Value.Checked = on;
                Color colour = implied && !ticked ? SystemColors.GrayText : Color.Empty;
                if (pair.Value.ForeColor != colour)
                    pair.Value.ForeColor = colour;
            }
            foreach (TreeNode root in _view.Nodes)
                ApplyFolderState(root);
        }

        /* True when every composite under the node is checked (and there is at least one) */
        private bool ApplyFolderState(TreeNode node)
        {
            if (node.Tag != null)
                return node.Checked;
            bool all = node.Nodes.Count > 0;
            foreach (TreeNode child in node.Nodes)
            {
                if (!ApplyFolderState(child))
                    all = false;
            }
            if (node.Checked != all)
                node.Checked = all;
            return all;
        }

        /* Open the folders that hold something the selection has just reached - a folder the user
           closed stays closed otherwise - without scrolling anywhere */
        private void ExpandSelected()
        {
            HashSet<ShortGuid> now = new HashSet<ShortGuid>(_ticked);
            now.UnionWith(_implied);
            foreach (ShortGuid id in now)
            {
                if (_revealed.Contains(id) || !_leaves.TryGetValue(id, out TreeNode leaf))
                    continue;
                for (TreeNode parent = leaf.Parent; parent != null; parent = parent.Parent)
                    if (!parent.IsExpanded)
                        parent.Expand();
            }
            _revealed = now;
        }

        private static IEnumerable<TreeNode> LeavesUnder(TreeNode node)
        {
            foreach (TreeNode child in node.Nodes)
            {
                if (child.Tag != null)
                    yield return child;
                else
                    foreach (TreeNode leaf in LeavesUnder(child))
                        yield return leaf;
            }
        }
        #endregion
    }
}
