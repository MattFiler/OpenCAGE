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
            byte[] mapStringByteArray = { 0x54, 0x45, 0x43, 0x48, 0x5F, 0x52, 0x4E, 0x44, 0x5F, 0x48, 0x5A, 0x44, 0x4C, 0x41, 0x42, 0x00, 0x00, 0x65, 0x6E, 0x67, 0x69, 0x6E, 0x65, 0x5F, 0x73, 0x65, 0x74, 0x74, 0x69, 0x6E, 0x67, 0x73 };
            bool loadAsBenchmark = true;

            //IF LOADING AS FRONTEND, WE ACT AS A RESET
            if (mapToLoadAsString == "FRONTEND")
            {
                mapToLoadAsString = "TECH_RND_HZDLAB";
                loadAsBenchmark = false;
            }

            //Update vanilla byte array with selection (strings over 16 will probably break stuff later down the line, but hey, it's "experimental") 
            for (int i = 0; i < mapToLoadAsString.Length; i++)
            {
                mapStringByteArray[i] = (byte)mapToLoadAsString[i];
            }
            mapStringByteArray[mapToLoadAsString.Length] = 0x00;

            //Edit game EXE with selected option
            byte[] alienIsolationBinary = File.ReadAllBytes(AlienDirectories.GameDirectoryRoot() + "/AI.exe");
            for (int i = 0; i < mapStringByteArray.Length; i++)
            {
                alienIsolationBinary[15676275 + i] = mapStringByteArray[i]; //MAGIC NUMBERS :)
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
