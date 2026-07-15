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
        private List<ListViewItem> _items = new List<ListViewItem>();
        private ListViewColumnSorter _sorter = new ListViewColumnSorter();
        private ListViewGroup _functionsGroup;
        private ListViewGroup _variablesGroup;
        private bool _includeVariables;

        public ListViewItem SelectedItem => functionTypes.SelectedItems.Count == 0 ? null : functionTypes.SelectedItems[0];
        public ListView FunctionTypes => functionTypes;
        public ImageList EntityListIcons => entityListIcons;
        public Action SelectedItemChanged;

        public FunctionTypeList()
        {
            InitializeComponent();
            EnsureEntityListIcons();
            _functionsGroup = new ListViewGroup("Functions", HorizontalAlignment.Left);
            _variablesGroup = new ListViewGroup("Variables", HorizontalAlignment.Left);
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

            functionTypes.ListViewItemSorter = _sorter;
            _sorter.SortColumn = 0;
            _sorter.Order = SortOrder.Ascending;

            functionTypes.Groups.Clear();
            functionTypes.Groups.Add(_functionsGroup);
            if (_includeVariables)
                functionTypes.Groups.Add(_variablesGroup);
            functionTypes.ShowGroups = _includeVariables;
            functionTypes.Columns[0].Text = _includeVariables ? "Entity" : "Function";

            _items.Clear();
            foreach (FunctionType function in Enum.GetValues(typeof(FunctionType)).Cast<FunctionType>().OrderBy(f => f.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                FunctionType? inherited = Singleton.Editor.CompositeBrowser.Content.Level.Commands.Utils.GetInheritedFunction(function);

                ListViewItem item = new ListViewItem(function.ToString());
                item.ImageIndex = 1; // function icon — matches CompositeEntityList
                item.SubItems.Add(inherited == null ? "" : inherited.Value.ToString());
                item.Tag = function;
                item.Group = _functionsGroup;
                _items.Add(item);
            }

            if (_includeVariables)
            {
                foreach (CompositePinType pinType in EnumExtensions.GetValuesInDeclarationOrder<CompositePinType>())
                {
                    if (pinType == CompositePinType.CompositeInputVariablePin || pinType == CompositePinType.CompositeOutputVariablePin)
                        continue;

                    ListViewItem item = new ListViewItem(pinType.ToUIString());
                    item.ImageIndex = EditorUtils.GetImageIndexForCompositePinType(pinType);
                    item.SubItems.Add("Variable");
                    item.Tag = pinType;
                    item.Group = _variablesGroup;
                    _items.Add(item);
                }
            }

            searchText.Text = SettingsManager.GetString(Settings.PreviouslySearchedFunctionType);
            Search();

            SelectFuncType(SettingsManager.GetString(Settings.PreviouslySelectedFunctionType));
        }

        private void SelectFuncType(string type)
        {
            if (type == "")
                return;

            for (int i = 0; i < functionTypes.Items.Count; i++)
            {
                if (functionTypes.Items[i].Text == type)
                {
                    functionTypes.Items[i].Selected = true;
                    break;
                }
            }
        }

        private void Search()
        {
            string selected = functionTypes.SelectedItems.Count > 0 ? functionTypes.SelectedItems[0].Text : "";

            functionTypes.BeginUpdate();
            functionTypes.Items.Clear();
            string normalizedSearch = searchText.Text.ToUpper().Replace(" ", "");
            ListViewItem[] filtered = _items
                .Where(o => o.Text.ToUpper().Replace(" ", "").Contains(normalizedSearch)
                    || o.SubItems.Cast<ListViewItem.ListViewSubItem>().Any(s => s.Text.ToUpper().Replace(" ", "").Contains(normalizedSearch)))
                .OrderBy(o => o.Text, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            functionTypes.Items.AddRange(filtered);
            functionTypes.EndUpdate();
            functionTypes.Sort();

            SelectFuncType(selected);

            SettingsManager.SetString(Settings.PreviouslySearchedFunctionType, searchText.Text);
        }

        private void clearSearchBtn_Click(object sender, EventArgs e)
        {
            searchText.Text = "";
            Search();
        }

        private void searchText_TextChanged(object sender, EventArgs e)
        {
            Search();
        }

        private void functionTypes_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectedItemChanged?.Invoke();
        }

        private void functionTypes_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column == _sorter.SortColumn)
            {
                if (_sorter.Order == SortOrder.Ascending)
                    _sorter.Order = SortOrder.Descending;
                else
                    _sorter.Order = SortOrder.Ascending;
            }
            else
            {
                _sorter.SortColumn = e.Column;
                _sorter.Order = SortOrder.Ascending;
            }

            functionTypes.Sort();
        }
    }
}
