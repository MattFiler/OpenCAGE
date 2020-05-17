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
using AlienPAK;

namespace Alien_Isolation_Mod_Tools
{
    public partial class Landing_ContentTools : Form
    {
        ToolPaths Paths = new ToolPaths();

        public Landing_ContentTools()
        {
            InitializeComponent();
        }

        private void Landing_ContentTools_Load(object sender, EventArgs e)
        {
            //Load fonts
            PrivateFontCollection ModToolFont = new PrivateFontCollection();
            ModToolFont.AddFontFile(Paths.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES) + "Isolation.ttf");
            ModToolFont.AddFontFile(Paths.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES) + "Jixellation.ttf");
            ModToolFont.AddFontFile(Paths.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES) + "Nostromo.ttf");

            //Set fonts & parents
            HeaderText.Font = new Font(ModToolFont.Families[1], 80);
            HeaderText.Parent = HeaderImage;
            InterfaceTools.Font = new Font(ModToolFont.Families[0], 40);
            ModelTools.Font = new Font(ModToolFont.Families[0], 40);
            TextureTools.Font = new Font(ModToolFont.Families[0], 40);
        }

        bool closedManually = false;
        private void CloseButton_Click(object sender, EventArgs e)
        {
            closedManually = true;
            Landing_Main LandingForm = new Landing_Main();
            LandingForm.Show();
            this.Close();
        }

        //When closing, check to see if we were manually closed
        //If not, halt the whole process to avoid lingering in background
        private void FormClosingEvent(object sender, FormClosingEventArgs e)
        {
            if (!closedManually)
            {
                Application.Exit();
                Environment.Exit(0);
            }
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
            MessageBox.Show("The model import/export tool is currently a work in progress!\nAll models are listed, but none can currently be imported/exported.\nStay tuned: this functionality is coming soon!", "Work In Progress!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            AlienPAK_Imported interfaceTool = new AlienPAK_Imported(AlienPAK_Wrapper.AlienContentType.MODEL);
            interfaceTool.Show();
        }

        //TEXTURE IMPORT/EXPORT
        private void TextureTools_Click(object sender, EventArgs e)
        {
            AlienPAK_Imported textureTool = new AlienPAK_Imported(AlienPAK_Wrapper.AlienContentType.TEXTURE);
            textureTool.Show();
        }
    }
}
