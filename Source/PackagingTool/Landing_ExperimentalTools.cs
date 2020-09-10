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
using AlienPAK;
using System.Diagnostics;

namespace Alien_Isolation_Mod_Tools
{
    public partial class Landing_ExperimentalTools : Form
    {
        ToolPaths Paths = new ToolPaths();

        public Landing_ExperimentalTools()
        {
            InitializeComponent();
        }

        private void Landing_ExperimentalTools_Load(object sender, EventArgs e)
        {
            //Load fonts
            PrivateFontCollection ModToolFont = new PrivateFontCollection();
            ModToolFont.AddFontFile(Paths.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES) + "Isolation.ttf");
            ModToolFont.AddFontFile(Paths.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES) + "Jixellation.ttf");
            ModToolFont.AddFontFile(Paths.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES) + "Nostromo.ttf");

            //Set fonts & parents
            HeaderText.Font = new Font(ModToolFont.Families[1], 80);
            HeaderText.Parent = HeaderImage;
            ScriptEditor.Font = new Font(ModToolFont.Families[0], 40);
            KeycodeEditor.Font = new Font(ModToolFont.Families[0], 40);
            LocalisationEditor.Font = new Font(ModToolFont.Families[0], 40);
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

        //SCRIPT EDITOR
        private void ScriptEditor_Click(object sender, EventArgs e)
        {
            ProcessStartInfo cathodeEditorProcess = new ProcessStartInfo();
            cathodeEditorProcess.WorkingDirectory = Paths.GetPath(ToolPaths.Paths.FOLDER_CATHODE_EDITOR_WORKING_FILES);
            cathodeEditorProcess.FileName = Paths.GetPath(ToolPaths.Paths.FILE_CATHODE_EDITOR_EXE);
            Process myProcess = Process.Start(cathodeEditorProcess);

            //AlienPAK_Imported textureTool = new AlienPAK_Imported(AlienPAK_Wrapper.AlienContentType.SCRIPT);
            //textureTool.Show();
        }
    }
}
