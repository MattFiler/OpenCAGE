using Alien_Isolation_Mod_Tools.ayz_Pack_Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools
{
    public partial class Filemanager_ImportMod : Form
    {
        Directories AlienDirectories = new Directories();
        AlienModPack AlienPacker = new AlienModPack();

        //Count installed mods
        int ModCounter = 0;

        public Filemanager_ImportMod()
        {
            InitializeComponent();

            //Load fonts
            PrivateFontCollection ModToolFont = new PrivateFontCollection();
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Isolation.ttf");
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Jixellation.ttf");
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Nostromo.ttf");

            //Set fonts & parents
            HeaderText.Font = new Font(ModToolFont.Families[1], 80);
            HeaderText.Parent = HeaderImage;
            Title1.Font = new Font(ModToolFont.Families[0], 20);

            //Mod preview setup
            SELECTED_MOD_TITLE.Font = new Font(ModToolFont.Families[0], 16);
            SELECTED_MOD_TITLE.Text = "";
            SELECTED_MOD_DESCRIPTION.Font = new Font(ModToolFont.Families[2], 12);
            SELECTED_MOD_DESCRIPTION.Text = "";
        }

        //Load all mod names on start
        private void Filemanager_ImportMod_Load(object sender, EventArgs e)
        {
            //Read in all mod names
            foreach (string directory in Directory.GetDirectories(AlienDirectories.ToolModInstallDirectory()))
            {
                InstalledMods.Items.Add(Path.GetFileName(directory));
                ModCounter++;
            }

            Title1.Text = "Showing " + ModCounter + " Available Mods";
        }

        //Load mod info
        private void InstalledMods_SelectedIndexChanged(object sender, EventArgs e)
        {
            //Get info
            string[] ModInfo = AlienPacker.GetModInfo(InstalledMods.SelectedItem.ToString());

            //Load mod info into form
            SELECTED_MOD_TITLE.Text = ModInfo[0];
            SELECTED_MOD_DESCRIPTION.Text = ModInfo[1];
            foreach (string file in Directory.GetFiles(AlienDirectories.ToolModInstallDirectory() + InstalledMods.SelectedItem.ToString()))
            {
                if (Path.GetExtension(file) != ".ayz")
                {
                    MOD_PREVIEW.BackgroundImage = Image.FromFile(file);
                }
            }

            //Enable button
            SelectMod.Enabled = true;
        }

        //Install mod
        private void SelectMod_Click(object sender, EventArgs e)
        {
            if (InstalledMods.SelectedIndex != -1)
            {
                //Show mod info first
                string[] ModInfo = AlienPacker.GetModInfo(InstalledMods.SelectedItem.ToString());

                DialogResult ModConfirmation = MessageBox.Show("Are you sure you wish to install mod \"" + ModInfo[0] + "\"?" + Environment.NewLine + "This will overwrite any existing changes.", "Install mod?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (ModConfirmation == DialogResult.Yes)
                {
                    if (AlienPacker.LoadModPack(InstalledMods.SelectedItem.ToString()))
                    {
                        MessageBox.Show("Successfully loaded mod!", "Operation completed.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("An unknown error occured." + Environment.NewLine + "Make sure Alien: Isolation is not open.", "Unable to install mod.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("Mod was not installed.", "Operation cancelled.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                MessageBox.Show("Please select a downloaded mod from the list.", "Unable to load mod.", MessageBoxButtons.OK, MessageBoxIcon.Warning); //shouldn't get here.
            }
        }

        //Close form
        private void CloseButton_Click(object sender, EventArgs e)
        {
            this.Hide();
            Landing_Main LandingForm = new Landing_Main();
            LandingForm.Show();
        }
    }
}
