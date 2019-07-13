using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools
{
    public partial class Landing_OpenGame : Form
    {
        Directories AlienDirectories = new Directories();

        public Landing_OpenGame()
        {
            InitializeComponent();
        }

        private void LaunchGame_Click(object sender, EventArgs e)
        {
            //Work out what option was selected
            RadioButton selectedMap = MapToLoad.Controls.OfType<RadioButton>().FirstOrDefault(r => r.Checked);
            string mapToLoadAsString = selectedMap.Text;
            bool loadAsBenchmark = true;

            //IF LOADING AS FRONTEND, WE ACT AS A RESET
            if (mapToLoadAsString == "FRONTEND")
            {
                mapToLoadAsString = "TECH_RND_HZDLAB";
                loadAsBenchmark = false;
            }

            //Edit game EXE with selected option
            byte[] alienIsolationBinary = File.ReadAllBytes(AlienDirectories.GameDirectoryRoot() + "/AI.exe");
            for (int i = 0; i < 16; i++)
            // ^^ We can technically go above 16 here and allow any map to load, but that requires overwriting "engine_setttings" which I think is important for saving configs.
            {
                byte newByte = 0x00;
                if (i < mapToLoadAsString.Length)
                {
                    newByte = (byte)mapToLoadAsString[i];
                }
                alienIsolationBinary[15676275 + i] = newByte; //MAGIC NUMBERS :)
            }

            //Write back out the edit
            BinaryWriter alienWriter = new BinaryWriter(File.OpenWrite(AlienDirectories.GameDirectoryRoot() + "/AI.exe"));
            alienWriter.BaseStream.SetLength(0);
            alienWriter.Write(alienIsolationBinary); //Four years of development in a few milliseconds...
            alienWriter.Close();

            //Start game process
            ProcessStartInfo alienProcess = new ProcessStartInfo();
            alienProcess.WorkingDirectory = AlienDirectories.GameDirectoryRoot();
            alienProcess.FileName = "AI.exe";
            if (loadAsBenchmark) { alienProcess.Arguments = "-benchmark"; }
            Process myProcess = Process.Start(alienProcess);

            //Goodbye
            this.Close();
        }

        private void Landing_OpenGame_Load(object sender, EventArgs e)
        {
            //Select default frontend on load
            radioButton8.Checked = true;
        }
    }
}
