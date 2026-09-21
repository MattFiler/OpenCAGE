using CATHODE.Scripting;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.Popups.UserControls
{
    public partial class FunctionTypeList : UserControl
    {
        /// <summary>Flat list of every leaf entry (function types and optionally variable pins).</summary>
        private List<LeafEntry> _allLeaves = new List<LeafEntry>();
        private bool _includeVariables;

        /// <summary>
        /// Returns a shim ListViewItem whose .Text and .Tag match the old ListView shape,
        /// so callers (AddEntity_Function, SelectFunctionType) keep working unchanged.
        /// </summary>
        public ListViewItem SelectedItem
        {
            get
            {
                TreeNode node = functionTree.SelectedNode;
                if (node == null || node.Tag == null)
                    return null;
                ListViewItem shim = new ListViewItem(node.Text);
                shim.Tag = node.Tag;
                return shim;
            }
        }

        /// <summary>Expose the tree so EntityBrowser can hook ItemDrag / NodeMouseDoubleClick.</summary>
        public TreeView FunctionTree => functionTree;

        public ImageList EntityListIcons => entityListIcons;
        public Action SelectedItemChanged;

        public FunctionTypeList()
        {
            InitializeComponent();
            EnsureEntityListIcons();
        }

        private void EnsureEntityListIcons()
        {
            if (entityListIcons.Images.Count >= 7)
                return;

            // Reuse the same icon strip as CompositeEntityList (includes input/output variable pins)
            ComponentResourceManager resources = new ComponentResourceManager(typeof(CompositeEntityList));
            object imageStream = resources.GetObject("entityListIcons.ImageStream");
            if (imageStream is ImageListStreamer streamer)
            {
                entityListIcons.ImageStream = streamer;
                entityListIcons.TransparentColor = Color.Transparent;
                entityListIcons.Images.SetKeyName(0, "AnimatorController Icon.png");
                entityListIcons.Images.SetKeyName(1, "d_ScriptableObject Icon braces only.png");
                entityListIcons.Images.SetKeyName(2, "d_PrefabVariant Icon.png");
                entityListIcons.Images.SetKeyName(3, "d_ScriptableObject Icon.png");
                entityListIcons.Images.SetKeyName(4, "AreaEffector2D Icon.ico");
                entityListIcons.Images.SetKeyName(5, "variable left.png");
                entityListIcons.Images.SetKeyName(6, "variable right.png");
            }
        }

        public void Setup(bool includeVariables = false)
        {
            _includeVariables = includeVariables;
            EnsureEntityListIcons();

            _allLeaves.Clear();
            foreach (FunctionType function in Enum.GetValues(typeof(FunctionType)).Cast<FunctionType>().OrderBy(f => f.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                string categoryName = FlowgraphLayoutManager.GetCategoryForFunctionType(function);
                _allLeaves.Add(new LeafEntry
                {
                    Name = function.ToString(),
                    Category = categoryName,
                    Tag = function,
                    ImageIndex = 1,
                });
            }

            if (_includeVariables)
            {
                foreach (CompositePinType pinType in EnumExtensions.GetValuesInDeclarationOrder<CompositePinType>())
                {
                    if (pinType == CompositePinType.CompositeInputVariablePin || pinType == CompositePinType.CompositeOutputVariablePin)
                        continue;

                    _allLeaves.Add(new LeafEntry
                    {
                        Name = pinType.ToUIString(),
                        Category = "Composite Interface",
                        Tag = pinType,
                        ImageIndex = 0,
                    });
                }
            }

            searchText.Text = SettingsManager.GetString(Settings.PreviouslySearchedFunctionType);
            Search();

            SelectByName(SettingsManager.GetString(Settings.PreviouslySelectedFunctionType));
        }

        #region Tree building

        private void Search()
        {
            string previousSelection = functionTree.SelectedNode?.Text ?? "";

            functionTree.BeginUpdate();
            functionTree.Nodes.Clear();

            string normalizedSearch = searchText.Text.ToUpper().Replace(" ", "");
            bool hasFilter = normalizedSearch.Length > 0;

            // Filter leaves — match on function name OR any segment of the category path
            List<LeafEntry> visible = hasFilter ? _allLeaves.Where(e => e.Name.ToUpper().Replace(" ", "").Contains(normalizedSearch) || e.Category.ToUpper().Replace(" ", "").Contains(normalizedSearch)).ToList() : _allLeaves;

            // Group by category path, then build tree
            // Category "Rendering/Lights" becomes two levels: Rendering → Lights
            Dictionary<string, TreeNode> categoryNodes = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);

            foreach (LeafEntry entry in visible.OrderBy(e => e.Category, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            {
                TreeNode parent = GetOrCreateCategoryNode(entry.Category, categoryNodes);
                TreeNode leaf = new TreeNode(entry.Name);
                leaf.Tag = entry.Tag;
                leaf.ImageIndex = entry.ImageIndex;
                leaf.SelectedImageIndex = entry.ImageIndex;
                InsertSorted(parent.Nodes, leaf);
            }

            functionTree.EndUpdate();
            functionTree.ExpandAll();

            // Restore previous selection if it is still visible
            SelectByName(previousSelection);

            SettingsManager.SetString(Settings.PreviouslySearchedFunctionType, searchText.Text);
        }

        /// <summary>
        /// Walk the category path (split on '/') and create or reuse tree nodes at each level.
        /// Category nodes get no Tag (so they are not selectable as results), a folder-style
        /// appearance (bold), and no icon.
        /// </summary>
        private TreeNode GetOrCreateCategoryNode(string categoryPath, Dictionary<string, TreeNode> cache)
        {
            if (cache.TryGetValue(categoryPath, out TreeNode existing))
                return existing;

            string[] parts = categoryPath.Split('/');
            TreeNode current = null;
            string builtPath = "";

            for (int i = 0; i < parts.Length; i++)
            {
                string segment = parts[i].Trim();
                builtPath = i == 0 ? segment : builtPath + "/" + segment;

                if (cache.TryGetValue(builtPath, out TreeNode node))
                {
                    current = node;
                    continue;
                }

                TreeNode newNode = new TreeNode(segment);
                newNode.NodeFont = new Font(functionTree.Font, FontStyle.Bold);
                // No icon for category nodes
                newNode.ImageIndex = -1;
                newNode.SelectedImageIndex = -1;

                if (current == null)
                    InsertSorted(functionTree.Nodes, newNode);
                else
                    InsertSorted(current.Nodes, newNode);

                cache[builtPath] = newNode;
                current = newNode;
            }

            return current;
        }

        /// <summary>Insert a node alphabetically among its siblings.</summary>
        private static void InsertSorted(TreeNodeCollection siblings, TreeNode node)
        {
            int index = 0;
            while (index < siblings.Count && string.Compare(siblings[index].Text, node.Text, StringComparison.OrdinalIgnoreCase) < 0)
                index++;
            siblings.Insert(index, node);
        }

        #endregion

        #region Selection

        private void SelectByName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            TreeNode found = FindLeafByName(functionTree.Nodes, name);
            if (found != null)
            {
                functionTree.SelectedNode = found;
                found.EnsureVisible();
            }
        }

        private static TreeNode FindLeafByName(TreeNodeCollection nodes, string name)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag != null && node.Text == name)
                    return node;
                TreeNode child = FindLeafByName(node.Nodes, name);
                if (child != null)
                    return child;
            }
            return null;
        }

        #endregion

        #region Events

        private void clearSearchBtn_Click(object sender, EventArgs e)
        {
            searchText.Text = "";
            Search();
        }

        private void searchText_TextChanged(object sender, EventArgs e)
        {
            Search();
        }

        private void functionTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            // Only fire for leaf nodes (ones with a Tag)
            if (e.Node?.Tag != null)
                SelectedItemChanged?.Invoke();
        }

        #endregion

        /// <summary>Internal data for one leaf (function type or variable pin).</summary>
        private class LeafEntry
        {
            public string Name;
            public string Category;
            public object Tag;
            public int ImageIndex;
        }
    }
}
