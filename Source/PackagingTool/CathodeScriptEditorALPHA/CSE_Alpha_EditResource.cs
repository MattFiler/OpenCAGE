using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AlienPAK;

namespace Alien_Isolation_Mod_Tools
{
    public delegate void FinishedEditingIndexes(List<RenderableElement> updated_indexes, bool did_update);

    public partial class CSE_Alpha_EditResource : Form
    {
        private List<CS2> complete_model_list;
        private List<RenderableElement> reds_list;
        private List<RenderableElement> reds_list_ORIGINAL;

        public event FinishedEditingIndexes EditComplete;

        /* Initialise */
        public CSE_Alpha_EditResource(List<CS2> model_list, List<RenderableElement> indexes)
        {
            complete_model_list = model_list;
            reds_list = indexes;

            RenderableElement[] origRedsList = new RenderableElement[reds_list.Count];
            indexes.CopyTo(origRedsList);
            reds_list_ORIGINAL = origRedsList.ToList<RenderableElement>();

            InitializeComponent();

            submesh_count.Value = reds_list.Count;
            for (int i = 0; i < reds_list.Count; i++)
            {
                AddNewDropdown();
            }
            submesh_count.ValueChanged += new EventHandler(submesh_count_ValueChanged);
        }

        /* Add/remove a submesh */
        private void submesh_count_ValueChanged(object sender, EventArgs e)
        {
            if (submesh_count.Value > reds_list.Count)
            {
                reds_list.Add(new RenderableElement());
                AddNewDropdown();
            }
            else
            {
                reds_list.RemoveAt(reds_list.Count - 1);
                RemoveDropdown();
            }
        }

        /* Send the changed resources back */
        private void save_changes_Click(object sender, EventArgs e)
        {
            bool did_change = (reds_list_ORIGINAL.Count != reds_list.Count);
            reds_list.Clear();

            int i = -1;
            foreach (Control control in submesh_list.Controls) {
                if (!(control is ComboBox)) continue;
                i++;

                RenderableElement newRed = new RenderableElement();
                newRed.model_index = ((ComboBox)control).SelectedIndex;
                newRed.material_index = complete_model_list[newRed.model_index].MaterialLibaryIndex;
                reds_list.Add(newRed);

                if (did_change) continue;
                if (i >= reds_list_ORIGINAL.Count) did_change = true;
                else if (reds_list[i] != reds_list_ORIGINAL[i]) did_change = true;
            }

            EditComplete?.Invoke(reds_list, did_change);
            this.Close();
        }

        /* Add a new dropdown box for submesh */
        private void AddNewDropdown()
        {
            ComboBox input_box = new ComboBox();
            for (int x = 0; x < complete_model_list.Count; x++)
            {
                input_box.Items.Add("MODEL: " + complete_model_list[x].Filename + " | SUBMESH: " + complete_model_list[x].ModelPartName + " | MATERIAL: " + complete_model_list[x].MaterialName + " | VERTS: " + complete_model_list[x].VertCount + "");
            }
            input_box.SelectedIndex = reds_list[submesh_list.Controls.Count].model_index;
            input_box.Width = 1452;
            input_box.Location = new Point(15, 29 * (submesh_list.Controls.Count + 1));
            input_box.DropDownStyle = ComboBoxStyle.DropDownList;
            submesh_list.Controls.Add(input_box);
            AdjustHeight();
        }

        /* Remove a dropdown from the list */
        private void RemoveDropdown()
        {
            submesh_list.Controls.RemoveAt(submesh_list.Controls.Count - 1);
            AdjustHeight();
        }

        /* Fix the height of the form/groupbox */
        private void AdjustHeight()
        { 
            submesh_list.Height = 20 + (reds_list.Count * 32);
            this.Height = 90 + submesh_list.Height;
        }
    }
}
