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
    public partial class CSE_Alpha_EditResource : Form
    {
        private List<CS2> complete_model_list;
        private List<int> model_list_indexes;

        /* Initialise */
        public CSE_Alpha_EditResource(List<CS2> model_list, List<int> indexes)
        {
            complete_model_list = model_list;
            model_list_indexes = indexes;

            InitializeComponent();

            submesh_count.Value = model_list_indexes.Count;
            for (int i = 0; i < model_list_indexes.Count; i++)
            {
                AddNewDropdown();
            }
            submesh_count.ValueChanged += new EventHandler(submesh_count_ValueChanged);
        }

        /* Add/remove a submesh */
        private void submesh_count_ValueChanged(object sender, EventArgs e)
        {
            if (submesh_count.Value > model_list_indexes.Count)
            {
                model_list_indexes.Add(0);
                AddNewDropdown();
            }
            else
            {
                model_list_indexes.RemoveAt(model_list_indexes.Count - 1);
                RemoveDropdown();
            }
        }

        /* Save the changed resources */
        private void save_changes_Click(object sender, EventArgs e)
        {
            //todo: pass info back
        }

        /* Add a new dropdown box for submesh */
        private void AddNewDropdown()
        {
            ComboBox input_box = new ComboBox();
            for (int x = 0; x < complete_model_list.Count; x++)
            {
                input_box.Items.Add("MODEL: " + complete_model_list[x].Filename + " | SUBMESH: " + complete_model_list[x].ModelPartName + " | MATERIAL: " + complete_model_list[x].MaterialName + " | VERTS: " + complete_model_list[x].VertCount + "");
            }
            input_box.SelectedIndex = model_list_indexes[submesh_list.Controls.Count];
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
            submesh_list.Height = 20 + (model_list_indexes.Count * 32);
            this.Height = 90 + submesh_list.Height;
        }
    }
}
