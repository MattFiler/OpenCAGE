using PackagingTool;
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
    public partial class Landing_Main : Form
    {
        Directories AlienDirectories = new Directories();

        public Landing_Main()
        {
            InitializeComponent();
        }

        private void Landing_Main_Load(object sender, EventArgs e)
        {
            /* CREATE SETTINGS FILE */
            if (!File.Exists(Directory.GetCurrentDirectory() + @"\modtools_settings.ayz"))
            {
                File.WriteAllText(Directory.GetCurrentDirectory() + @"\modtools_settings.ayz", "1\n0\n1"); //Write default settings
            }

            /* SET GAME FOLDER */
            bool hasThrownError = false;
            if (!File.Exists(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz"))
            {
                //Check if user has followed tutorial
                if (File.Exists(Directory.GetCurrentDirectory() + @"\AI.exe"))
                {
                    File.WriteAllText(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz", Directory.GetCurrentDirectory()); //Write new file
                }
                else
                {
                    MessageBox.Show("Please locate your Alien: Isolation executable (AI.exe).");
                    OpenFileDialog selectGameFile = new OpenFileDialog();
                    if (selectGameFile.ShowDialog() == DialogResult.OK)
                    {
                        File.WriteAllText(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz", Path.GetDirectoryName(selectGameFile.FileName)); //Write new file
                    }
                    else
                    {
                        hasThrownError = true;
                    }
                }
            }
            AlienDirectories = new Directories();

            /* VALIDATE GAME DIRECTORY */
            if (!File.Exists(AlienDirectories.GameDirectoryRoot() + @"\DATA\BINARY_BEHAVIOR\_DIRECTORY_CONTENTS.BML") || hasThrownError)
            {
                MessageBox.Show("Please ensure you have selected the correct game install location. Missing files!");
                File.Delete(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz");
                this.Close();
            }

            /* CREATE REQUIRED FOLDERS */
            Directory.CreateDirectory(AlienDirectories.ToolTreeDirectory());
            Directory.CreateDirectory(AlienDirectories.ToolWorkingDirectory());
            Directory.CreateDirectory(AlienDirectories.ToolModInstallDirectory());

            //Initialise resources for mod tools
            if (!Directory.Exists(AlienDirectories.ToolResourceDirectory()))
            {
                Directory.CreateDirectory(AlienDirectories.ToolResourceDirectory());
                File.WriteAllBytes(AlienDirectories.ToolResourceDirectory() + "Isolation.ttf", Properties.Resources.Isolation_Isolation);
                File.WriteAllBytes(AlienDirectories.ToolResourceDirectory() + "Jixellation.ttf", Properties.Resources.JixellationBold_Jixellation);
                File.WriteAllBytes(AlienDirectories.ToolResourceDirectory() + "Nostromo.ttf", Properties.Resources.NostromoBoldCond_Nostromo_Cond);
            }

            //Load fonts
            PrivateFontCollection ModToolFont = new PrivateFontCollection();
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Isolation.ttf");
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Jixellation.ttf");
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Nostromo.ttf");

            //Set fonts & parents
            MakeMod.Font = new Font(ModToolFont.Families[0], 40);
            MakeMod.Parent = LandingBackground;
            SaveMod.Font = new Font(ModToolFont.Families[0], 40);
            SaveMod.Parent = LandingBackground;
            LoadMod.Font = new Font(ModToolFont.Families[0], 40);
            LoadMod.Parent = LandingBackground;
            DeleteMod.Font = new Font(ModToolFont.Families[0], 40);
            DeleteMod.Parent = LandingBackground;
            VersionText.Font = new Font(ModToolFont.Families[1], 15);
            VersionText.Parent = LandingBackground;
            Title1.Font = new Font(ModToolFont.Families[1], 20);
            Title1.Parent = LandingBackground;
            Title2.Font = new Font(ModToolFont.Families[1], 20);
            Title2.Parent = LandingBackground;

            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        //Make Mod
        private void MakeMod_Click(object sender, EventArgs e)
        {
            Landing loadForm = new Landing();
            loadForm.Show();
            this.Hide();
        }
        
        //Save Mod
        private void LoadMod_Click(object sender, EventArgs e)
        {
            Filemanager_ExportMod loadForm = new Filemanager_ExportMod();
            loadForm.Show();
            this.Hide();
        }

        //Load Mod
        private void DeleteMod_Click(object sender, EventArgs e)
        {
            Filemanager_ImportMod loadForm = new Filemanager_ImportMod();
            loadForm.Show();
            this.Hide();
        }

        //Delete Mod
        private void DeleteMod_Click_1(object sender, EventArgs e)
        {
            Filemanager_ResetMod loadForm = new Filemanager_ResetMod();
            loadForm.Show();
            this.Hide();
        }
    }
}
