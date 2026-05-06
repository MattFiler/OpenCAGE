using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CommandsEditor
{
    public class ListViewColumnSorter : System.Collections.IComparer
    {
        public int SortColumn { get; set; } // Specifies the column to be sorted
        public SortOrder Order { get; set; } // Specifies the order in which to sort (i.e. 'Ascending').

        public ListViewColumnSorter()
        {
            SortColumn = 0;
            Order = SortOrder.None;
        }

        public int Compare(object x, object y)
        {
            int compareResult;
            ListViewItem listviewX = (ListViewItem)x;
            ListViewItem listviewY = (ListViewItem)y;

            // Compare the two items
            compareResult = String.Compare(listviewX.SubItems[SortColumn].Text, listviewY.SubItems[SortColumn].Text);

            // Calculate the correct return value based on the object comparison
            if (Order == SortOrder.Ascending)
            {
                return compareResult;
            }
            else if (Order == SortOrder.Descending)
            {
                return (-compareResult);
            }
            else
            {
                return 0;
            }
        }
    }
}
