using CATHODE.Scripting;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Popups.UserControls
{
    public partial class FunctionTypeList : UserControl
    {
        private List<ListViewItem> _items = new List<ListViewItem>();
        private ListViewColumnSorter _sorter = new ListViewColumnSorter();

        public ListViewItem SelectedItem => functionTypes.SelectedItems.Count == 0 ? null : functionTypes.SelectedItems[0];
        public Action SelectedItemChanged;

        public FunctionTypeList()
        {
            InitializeComponent(); 
        }

        public void Setup()
        {
            functionTypes.ListViewItemSorter = _sorter;

            foreach (FunctionType function in Enum.GetValues(typeof(FunctionType)))
            {
                FunctionType? inherited = Singleton.Editor.CommandsDisplay.Content.Level.Commands.Utils.GetInheritedFunction(function);

                ListViewItem item = new ListViewItem(function.ToString());
                item.ImageIndex = 0;
                item.SubItems.Add(inherited == null ? "" : inherited.Value.ToString());

                _items.Add(item);
            }

            searchText.Text = SettingsManager.GetString(Singleton.Settings.PreviouslySearchedFunctionType);
            Search();

            SelectFuncType(SettingsManager.GetString(Singleton.Settings.PreviouslySelectedFunctionType));
        }

        private void SelectFuncType(string type)
        {
            if (type == "")
            {
                return;
            }

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
            functionTypes.Items.AddRange(_items.Where(o => o.Text.ToUpper().Replace(" ", "").Contains(searchText.Text.ToUpper().Replace(" ", ""))).ToList().ToArray());
            functionTypes.EndUpdate();

            SelectFuncType(selected);

            typesCount.Text = "Showing " + functionTypes.Items.Count;
            SettingsManager.SetString(Singleton.Settings.PreviouslySearchedFunctionType, searchText.Text);
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

        private void functionTypes_ColumnClick(object sender, System.Windows.Forms.ColumnClickEventArgs e)
        {
            // Determine if the clicked column is already the column that is being sorted.
            if (e.Column == _sorter.SortColumn)
            {
                // Reverse the current sort direction for this column.
                if (_sorter.Order == SortOrder.Ascending)
                {
                    _sorter.Order = SortOrder.Descending;
                }
                else
                {
                    _sorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                _sorter.SortColumn = e.Column;
                _sorter.Order = SortOrder.Ascending;
            }

            // Perform the sort with these new sort options.
            this.functionTypes.Sort();
        }
    }
}
