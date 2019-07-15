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
using PackagingTool;

namespace Alien_Isolation_Mod_Tools
{
    public partial class Landing_ConfigTools : Form
    {
        Directories AlienDirectories = new Directories();

        public Landing_ConfigTools()
        {
            InitializeComponent();
        }

        private void Landing_ConfigTools_Load(object sender, EventArgs e)
        {
            //Load fonts
            PrivateFontCollection ModToolFont = new PrivateFontCollection();
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Isolation.ttf");
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Jixellation.ttf");
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Nostromo.ttf");

            //Set fonts & parents
            HeaderText.Font = new Font(ModToolFont.Families[1], 80);
            HeaderText.Parent = HeaderImage;
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            Landing_Main LandingForm = new Landing_Main();
            LandingForm.Show();
            this.Close();
        }

        //EDIT CONFIGS
        private void EditConfigs_Click(object sender, EventArgs e)
        {
            ConfigTools_Editor editConfigs = new ConfigTools_Editor();
            editConfigs.Show();
            this.Close();
        }

        //RESET CONFIGS
        private void ResetConfigs_Click(object sender, EventArgs e)
        {
            Filemanager_ResetMod resetFiles = new Filemanager_ResetMod();
            resetFiles.Show();
            this.Close();
        }

        //LOAD PREVIOUS CONFIGS
        private void LoadPrevious_Click(object sender, EventArgs e)
        {
            Filemanager_ImportMod importPrevious = new Filemanager_ImportMod();
            importPrevious.Show();
            this.Close();
        }

        //EXPORT CURRENT CONFIGS
        private void ExportChanges_Click(object sender, EventArgs e)
        {
            Filemanager_ExportMod exportChanges = new Filemanager_ExportMod();
            exportChanges.Show();
            this.Close();
        }
    }
}
