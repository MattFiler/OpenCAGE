using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.ConfigEditors
{
    public partial class BlueprintEditorPopup : BaseWindow
    {
        int formType = 0;

        public BlueprintEditorPopup(int formLoadType) : base()
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

            else if (formLoadType == 2)
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

            else
            {
                throw new Exception("");
            }

            RESOURCES_NEW.SelectedIndex = 0;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            BlueprintEditor blueprintForm = (BlueprintEditor)Application.OpenForms["BlueprintEditor"];
            blueprintForm.getDataFromPopup(QUANTITY_NEW.Text, RESOURCES_NEW.Text, formType);

            this.Close();
        }

        private void openItemDocs_Click(object sender, EventArgs e)
        {
            BlueprintEditorDocs openDocs = new BlueprintEditorDocs();
            openDocs.Show();
        }
    }
}
