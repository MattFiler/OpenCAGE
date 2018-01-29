using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Alien_Isolation_Mod_Tools.ayz_Pack_Tools;
using System.Drawing.Text;
using System.IO;
using System.Diagnostics;

namespace Alien_Isolation_Mod_Tools
{
    public partial class Filemanager_ExportMod : Form
    {
        Directories AlienDirectories = new Directories();

        public Filemanager_ExportMod()
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
            Title3.Font = new Font(ModToolFont.Families[0], 20);
            Title5.Font = new Font(ModToolFont.Families[0], 20);
            Title2.Font = new Font(ModToolFont.Families[0], 11);
            Title4.Font = new Font(ModToolFont.Families[0], 11);
            Title6.Font = new Font(ModToolFont.Families[0], 11);
        }

        //Save mod
        private void SaveMod_Click(object sender, EventArgs e)
        {
            if (ModNameInput.Text != "" &&
                ModDescInput.Text != "" &&
                ModAuthorInput.Text != "")
            {
                AlienModPack AlienPacker = new AlienModPack();
                if (AlienPacker.SaveModPack(ModNameInput.Text, ModDescInput.Text, ModAuthorInput.Text))
                {
                    MessageBox.Show("Successfully saved mod!");
                    Process.Start(AlienDirectories.ToolModInstallDirectory()); //Open mods folder.
                }
                else
                {
                    MessageBox.Show("An unknown error occured.");
                }
            }
            else
            {
                MessageBox.Show("Please fill out all fields first.");
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
