using Alien_Isolation_Mod_Tools.Attribute_Editors.Misc;
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
    public partial class Landing_ExperimentalTools : Form
    {
        Directories AlienDirectories = new Directories();

        public Landing_ExperimentalTools()
        {
            InitializeComponent();
        }

        private void Landing_ExperimentalTools_Load(object sender, EventArgs e)
        {
            //Load fonts
            PrivateFontCollection ModToolFont = new PrivateFontCollection();
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Isolation.ttf");
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Jixellation.ttf");
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Nostromo.ttf");

            //Set fonts & parents
            HeaderText.Font = new Font(ModToolFont.Families[1], 80);
            HeaderText.Parent = HeaderImage;
            Title1.Font = new Font(ModToolFont.Families[0], 20);
            KeycodeEditor.Font = new Font(ModToolFont.Families[0], 40);
            LocalisationEditor.Font = new Font(ModToolFont.Families[0], 40);
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            Landing_Main LandingForm = new Landing_Main();
            LandingForm.Show();
            this.Close();
        }

        //LOCALISATION EDITOR
        private void LocalisationEditor_Click(object sender, EventArgs e)
        {
            LocalisationEditor openLocalisationEditor = new LocalisationEditor();
            openLocalisationEditor.Show();
        }

        //KEYCODE EDITOR
        private void KeycodeEditor_Click(object sender, EventArgs e)
        {
            KeycodeEditor openKeycodeEditor = new KeycodeEditor();
            openKeycodeEditor.Show();
        }
    }
}
