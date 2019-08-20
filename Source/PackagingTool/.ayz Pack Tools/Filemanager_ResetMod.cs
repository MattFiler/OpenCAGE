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
    public partial class Filemanager_ResetMod : Form
    {
        ToolPaths Paths = new ToolPaths();
        AlienModPack AlienPacker = new AlienModPack();

        public Filemanager_ResetMod()
        {
            InitializeComponent();

            //Load fonts
            PrivateFontCollection ModToolFont = new PrivateFontCollection();
            ModToolFont.AddFontFile(Paths.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES) + "Isolation.ttf");
            ModToolFont.AddFontFile(Paths.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES) + "Jixellation.ttf");
            ModToolFont.AddFontFile(Paths.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES) + "Nostromo.ttf");

            //Set fonts & parents
            HeaderText.Font = new Font(ModToolFont.Families[1], 80);
            HeaderText.Parent = HeaderImage;
            Title1.Font = new Font(ModToolFont.Families[0], 20);
            Title2.Font = new Font(ModToolFont.Families[0], 20);
        }

        //Close form
        private void CloseButton_Click(object sender, EventArgs e)
        {
            try { GC.Collect(); GC.WaitForPendingFinalizers(); } catch { }
            Landing_ConfigTools LandingForm = new Landing_ConfigTools();
            LandingForm.Show();
            this.Close();
        }

        //reset all
        private void SelectMod_Click(object sender, EventArgs e)
        {
            AlienPacker.ResetFiles("ALL", false);
        }

        //reset individual
        private void ResetGraphics_Click(object sender, EventArgs e)
        {
            AlienPacker.ResetFiles("GRAPHICS", false);
        }
        private void ResetLighting_Click(object sender, EventArgs e)
        {
            AlienPacker.ResetFiles("LIGHTING", false);
        }
        private void ResetAlienConfigs_Click(object sender, EventArgs e)
        {
            AlienPacker.ResetFiles("ALIENCONFIGS", false);
        }
        private void ResetTrees_Click(object sender, EventArgs e)
        {
            AlienPacker.ResetFiles("BEHAVIOURS", false);
        }
        private void ResetDifficulties_Click(object sender, EventArgs e)
        {
            AlienPacker.ResetFiles("DIFFICULTIES", false);
        }
        private void ResetViewconesets_Click(object sender, EventArgs e)
        {
            AlienPacker.ResetFiles("VIEWCONES", false);
        }
        private void ResetAmmo_Click(object sender, EventArgs e)
        {
            AlienPacker.ResetFiles("AMMO", false);
        }
        private void ResetGblItem_Click(object sender, EventArgs e)
        {
            AlienPacker.ResetFiles("GBL_ITEM", false);
        }
        private void ResetChrInfo_Click(object sender, EventArgs e)
        {
            AlienPacker.ResetFiles("CHR_INFO", false);
        }
    }
}
