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
using Alien_Isolation_Mod_Tools.Content_Tools;

namespace Alien_Isolation_Mod_Tools
{
    public partial class Landing_ContentTools : Form
    {
        Directories AlienDirectories = new Directories();

        public Landing_ContentTools()
        {
            InitializeComponent();
        }

        private void Landing_ContentTools_Load(object sender, EventArgs e)
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

        //UI IMPORT/EXPORT
        private void InterfaceTools_Click(object sender, EventArgs e)
        {
            AlienPAK_Imported interfaceTool = new AlienPAK_Imported(AlienPAK_Wrapper.AlienContentType.UI);
            interfaceTool.Show();
        }

        //MODEL IMPORT/EXPORT
        private void ModelTools_Click(object sender, EventArgs e)
        {
            //coming "soon"
        }

        //TEXTURE IMPORT/EXPORT
        private void TextureTools_Click(object sender, EventArgs e)
        {
            AlienPAK_Imported textureTool = new AlienPAK_Imported(AlienPAK_Wrapper.AlienContentType.TEXTURE);
            textureTool.Show();
        }
    }
}
