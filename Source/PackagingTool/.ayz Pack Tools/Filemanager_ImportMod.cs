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
        private void SelectMod_Click(object sender, EventArgs e)
        {
            if (InstalledMods.SelectedIndex != -1)
            {
                AlienModPack AlienPacker = new AlienModPack();

                //Show mod info first
                string[] ModInfo = AlienPacker.GetModInfo(InstalledMods.SelectedItem.ToString());

                DialogResult ModConfirmation = MessageBox.Show(ModInfo[1], "Load '" + ModInfo[0] + "' by " + ModInfo[2] + "?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (ModConfirmation == DialogResult.Yes)
                {
                    if (AlienPacker.LoadModPack(InstalledMods.SelectedItem.ToString()))
                    {
                        MessageBox.Show("Successfully loaded mod!");
                    }
                    else
                    {
                        MessageBox.Show("An unknown error occured.");
                    }
                }
                else
                {
                    MessageBox.Show("Mod was not installed.");
                }
            }
            else
            {
                MessageBox.Show("Please select a downloaded mod from the list.");
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
