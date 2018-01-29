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
        //Main Directories
        string gameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz"); //Our game's dir

        public Filemanager_ImportMod()
        {
            InitializeComponent();

            //Load fonts
            PrivateFontCollection ModToolFont = new PrivateFontCollection();
            ModToolFont.AddFontFile("Modtool Resources/Isolation.ttf");
            ModToolFont.AddFontFile("Modtool Resources/Jixellation.ttf");
            ModToolFont.AddFontFile("Modtool Resources/Nostromo.ttf");

            //Set fonts & parents
            HeaderText.Font = new Font(ModToolFont.Families[1], 80);
            HeaderText.Parent = HeaderImage;
            Title1.Font = new Font(ModToolFont.Families[0], 20);
        }

        //Load all mod names on start
        private void Filemanager_ImportMod_Load(object sender, EventArgs e)
        {
            //Read in all mod names
            foreach (string directory in Directory.GetDirectories(gameDirectory + "/DATA/MODS/"))
            {
                InstalledMods.Items.Add(Path.GetFileName(directory));
            }
        }

        //Load mod info
        private void SelectMod_Click(object sender, EventArgs e)
        {
            //TODO: Add a popup here to confirm selection before actually loading.

            if (InstalledMods.SelectedIndex != -1)
            {
                AlienModPack AlienPacker = new AlienModPack();
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
