using Alien_Isolation_Mod_Tools.ayz_Pack_Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools
{
    public partial class Filemanager_ResetMod : Form
    {
        Directories AlienDirectories = new Directories();
        AlienModPack AlienPacker = new AlienModPack();

        public Filemanager_ResetMod()
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
            Title2.Font = new Font(ModToolFont.Families[0], 20);
        }

        //Close form
        private void CloseButton_Click(object sender, EventArgs e)
        {
            try { GC.Collect(); GC.WaitForPendingFinalizers(); } catch { }
            Landing_Main LandingForm = new Landing_Main();
            LandingForm.Show();
            this.Hide();
        }

        //reset all
        private void SelectMod_Click(object sender, EventArgs e)
        {

        }

        //reset individual
        private void ResetGraphics_Click(object sender, EventArgs e)
        {
            RequestReset("ENGINE_SETTINGS");
        }
        private void ResetLighting_Click(object sender, EventArgs e)
        {
            RequestReset("LIGHTING");
        }
        private void ResetAlienConfigs_Click(object sender, EventArgs e)
        {
            RequestReset("ALIENCONFIGS");
        }
        private void ResetTrees_Click(object sender, EventArgs e)
        {
            RequestReset("BEHAVIOR");
        }
        private void ResetDifficulties_Click(object sender, EventArgs e)
        {
            RequestReset("DIFFICULTYSETTINGS");
        }
        private void ResetViewconesets_Click(object sender, EventArgs e)
        {
            RequestReset("VIEW_CONE_SETS");
        }
        private void ResetAmmo_Click(object sender, EventArgs e)
        {
            RequestReset("WEAPON_INFO");
        }
        private void ResetGblItem_Click(object sender, EventArgs e)
        {
            RequestReset("GBL_ITEMS");
        }
        private void ResetChrInfo_Click(object sender, EventArgs e)
        {
            RequestReset("CHR_INFO");
        }
        private void RequestReset(string resetIdentifier)
        {
            MessageBox.Show(resetIdentifier);
        }
    }
}
