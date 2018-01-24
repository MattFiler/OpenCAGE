/*
 * 
 * Created by Matt Filer
 * www.mattfiler.co.uk
 * 
 */
 
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools
{
    public partial class BlueprintEditorPopup : Form
    {
        int formType = 0;

        public BlueprintEditorPopup(int formLoadType)
        {
            InitializeComponent();

            formType = formLoadType;

            if (formLoadType == 1)
            {
                //NEW ITEM REQUIRED TO CRAFT
                RESOURCES_NEW.Items.Clear();
                RESOURCES_NEW.Items.AddRange(new object[] {
                "gasoline",
                "high_voltage_battery",
                "explosive",
                "pipe",
                "sharp_blade",
                "gel",
                "adhesive",
                "scrap"});
                labelToChange.Text = "Core Resource";
            }

            if (formLoadType == 2)
            {
                //NEW ITEM THAT WILL BE OUTPUT
                RESOURCES_NEW.Items.Clear();
                RESOURCES_NEW.Items.AddRange(new object[] {
                //"flare",
                //"flare_box",
                "pipe_bomb",
                "molotov_cocktail",
                "emp_mine",
                "smoke_bomb",
                "flashbang",
                "noisemaker",
                //Everything below here other than small_medikit is labelled as depreciated and may not work correctly.
                "wide_area_chem_light",
                "explosive_mine",
                "incendiary_mine",
                "chem_light",
                "small_medikit"});
                //"large_medikit"});
                labelToChange.Text = "Craftable Item";
            }
        }

        //Send to other form
        private void btnSave_Click(object sender, EventArgs e)
        {
            BlueprintEditor blueprintForm = (BlueprintEditor)Application.OpenForms["BlueprintEditor"];
            blueprintForm.getDataFromPopup(QUANTITY_NEW.Text, RESOURCES_NEW.Text, formType);

            this.Close();
        }

        //Open docs
        private void openItemDocs_Click(object sender, EventArgs e)
        {
            BlueprintEditorDocs openDocs = new BlueprintEditorDocs();
            openDocs.Show();
        }
    }
}
