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
        string behaviourDirectory = Directory.GetCurrentDirectory() + @"\Behaviour Tree Directory\"; //Our working dir for BehaviourPacker
        string attributeDirectory = Directory.GetCurrentDirectory() + @"\Attribute Editor Directory\"; //Our working dir for CharEd
        string gameDirectory = ""; //Our game's dir, set on form load

        public Landing_Main()
        {
            InitializeComponent();

            //Initialise resources for mod tools
            if (!Directory.Exists("Modtool Resources"))
            {
                Directory.CreateDirectory("Modtool Resources");
                File.WriteAllBytes("Modtool Resources/Isolation.ttf", Properties.Resources.Isolation_Isolation);
                File.WriteAllBytes("Modtool Resources/Jixellation.ttf", Properties.Resources.JixellationBold_Jixellation);
                File.WriteAllBytes("Modtool Resources/Nostromo.ttf", Properties.Resources.NostromoBoldCond_Nostromo_Cond);
            }

            //Load fonts
            PrivateFontCollection ModToolFont = new PrivateFontCollection();
            ModToolFont.AddFontFile("Modtool Resources/Isolation.ttf");
            ModToolFont.AddFontFile("Modtool Resources/Jixellation.ttf");
            ModToolFont.AddFontFile("Modtool Resources/Nostromo.ttf");

            //Set fonts & parents
            MakeMod.Font = new Font(ModToolFont.Families[0], 40);
            MakeMod.Parent = pictureBox1;
            LoadMod.Font = new Font(ModToolFont.Families[0], 40);
            LoadMod.Parent = pictureBox1;
            DeleteMod.Font = new Font(ModToolFont.Families[0], 40);
            DeleteMod.Parent = pictureBox1;
            label1.Font = new Font(ModToolFont.Families[1], 15);
            label1.Parent = pictureBox1;
        }

        private void Landing_Main_Load(object sender, EventArgs e)
        {
            /* CREATE REQUIRED FOLDERS */
            Directory.CreateDirectory(behaviourDirectory);
            Directory.CreateDirectory(attributeDirectory);

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
            gameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz"); //Set our game's dir

            /* VALIDATE GAME DIRECTORY */
            if (!File.Exists(gameDirectory + @"\DATA\BINARY_BEHAVIOR\_DIRECTORY_CONTENTS.BML") || hasThrownError)
            {
                MessageBox.Show("Please ensure you have selected the correct game install location. Missing files!");
                File.Delete(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz");
                MakeMod.Enabled = false;
                LoadMod.Enabled = false;
                DeleteMod.Enabled = false;
            }

            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        //Make Mod
        private void MakeMod_Click(object sender, EventArgs e)
        {
            Landing loadForm = new Landing();
            loadForm.Show();
            this.Close();
        }
        
        //Load Mod
        private void LoadMod_Click(object sender, EventArgs e)
        {
            Filemanager_ImportMod loadForm = new Filemanager_ImportMod();
            loadForm.Show();
            this.Close();
        }

        //Delete Mod
        private void DeleteMod_Click(object sender, EventArgs e)
        {
            Filemanager_ResetMod loadForm = new Filemanager_ResetMod();
            loadForm.Show();
            this.Close();
        }
    }
}
