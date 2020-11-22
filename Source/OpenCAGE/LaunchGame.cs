using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class LaunchGame : Form
    {
        /* On init, if we are trying to launch to a map, skip GUI */
        public LaunchGame(string MapToLaunchTo = "")
        {
            if (MapToLaunchTo != "")
            {
                LaunchToMap(MapToLaunchTo);
                return;
            }

            InitializeComponent();

            UIMOD_DebugCheckpoints.Checked = SettingsManager.GetBool("UIOPT_PAUSEMENU");
            UIMOD_MapName.Checked = SettingsManager.GetBool("UIOPT_LOADINGSCREEN");
            UIMOD_MapSelection.Checked = SettingsManager.GetBool("UIOPT_NEWFRONTENDMENU");
            UIMOD_ReturnFrontend.Checked = SettingsManager.GetBool("UIOPT_GAMEOVERMENU");
        }

        /* Load game with given map name */
        private void LaunchToMap(string MapName)
        {
            //Original exe bytes to overwrite
            byte[] mapStringByteArray = { 0x54, 0x45, 0x43, 0x48, 0x5F, 0x52, 0x4E, 0x44, 0x5F, 0x48, 0x5A, 0x44, 0x4C, 0x41, 0x42, 0x00, 0x00, 0x65, 0x6E, 0x67, 0x69, 0x6E, 0x65, 0x5F, 0x73, 0x65, 0x74, 0x74, 0x69, 0x6E, 0x67, 0x73 };
            bool loadAsBenchmark = true;

            //IF LOADING AS FRONTEND, WE ACT AS A RESET
            if (MapName == "FRONTEND")
            {
                MapName = "TECH_RND_HZDLAB";
                loadAsBenchmark = false;
            }

            //Update vanilla byte array with selection (strings over 16 will probably break stuff later down the line, but hey, it's "experimental") 
            for (int i = 0; i < MapName.Length; i++)
            {
                mapStringByteArray[i] = (byte)MapName[i];
            }
            mapStringByteArray[MapName.Length] = 0x00;

            //Edit game EXE with selected option
            byte[] alienIsolationBinary = File.ReadAllBytes(SettingsManager.GetString("PATH_GameRoot") + "/AI.exe");
            for (int i = 0; i < mapStringByteArray.Length; i++)
            {
                alienIsolationBinary[15676275 + i] = mapStringByteArray[i]; //MAGIC NUMBERS :)
            }

            //Write back out the edit
            BinaryWriter alienWriter = new BinaryWriter(File.OpenWrite(SettingsManager.GetString("PATH_GameRoot") + "/AI.exe"));
            alienWriter.BaseStream.SetLength(0);
            alienWriter.Write(alienIsolationBinary); //Four years of development in a few milliseconds...
            alienWriter.Close();

            //Start game process
            ProcessStartInfo alienProcess = new ProcessStartInfo();
            alienProcess.WorkingDirectory = SettingsManager.GetString("PATH_GameRoot");
            alienProcess.FileName = SettingsManager.GetString("PATH_GameRoot") + "/AI.exe";
            if (loadAsBenchmark) { alienProcess.Arguments = "-benchmark"; }
            Process.Start(alienProcess);
        }

        /* Load game from GUI map selection */
        private void LaunchGame_Click(object sender, EventArgs e)
        {
            //Work out what option was selected and launch to it
            RadioButton selectedMap = MapToLoad.Controls.OfType<RadioButton>().FirstOrDefault(r => r.Checked);
            LaunchToMap(selectedMap.Text);
            this.Close();
        }

        /* Show/hide appropriate GUI options on load */
        private void Landing_OpenGame_Load(object sender, EventArgs e)
        {
            //Select default frontend on load
            radioButton8.Checked = true;

            /* -- Enable/Disable options based on DLC ownership -- */

            //LAST SURVIVOR
            EnableOptionIfHasDLC(radioButton30);

            //CREW EXPENDABLE
            EnableOptionIfHasDLC(radioButton29);

            //THE TRIGGER
            EnableOptionIfHasDLC(radioButton28);
            EnableOptionIfHasDLC(radioButton1);
            EnableOptionIfHasDLC(radioButton40);

            //CORPORATE LOCKDOWN
            EnableOptionIfHasDLC(radioButton26);
            EnableOptionIfHasDLC(radioButton25);
            EnableOptionIfHasDLC(radioButton22);

            //TRAUMA
            EnableOptionIfHasDLC(radioButton27);
            EnableOptionIfHasDLC(radioButton24);
            EnableOptionIfHasDLC(radioButton23);

            //SAFE HAVEN
            EnableOptionIfHasDLC(radioButton38);

            //LOST CONTACT
            EnableOptionIfHasDLC(radioButton37);
        }

        /* Enable/disable GUI inputs based on DLC ownership */
        private void EnableOptionIfHasDLC(RadioButton UiOption)
        {
            UiOption.Enabled = File.Exists(SettingsManager.GetString("PATH_GameRoot") + "/DATA/ENV/PRODUCTION/" + UiOption.Text + "/WORLD/COMMANDS.PAK");
        }

        /* UI Modifications */
        CathodeLib.PAK AlienPAK = new CathodeLib.PAK();
        private void UIMOD_DebugCheckpoints_CheckedChanged(object sender, EventArgs e)
        {
            UpdateUI("PAUSEMENU", UIMOD_DebugCheckpoints.Checked);
        }
        private void UIMOD_MapName_CheckedChanged(object sender, EventArgs e)
        {
            UpdateUI("LOADINGSCREEN", UIMOD_MapName.Checked);
        }
        private void UIMOD_MapSelection_CheckedChanged(object sender, EventArgs e)
        {
            UpdateUI("NEWFRONTENDMENU", UIMOD_MapSelection.Checked);
        }
        private void UIMOD_ReturnFrontend_CheckedChanged(object sender, EventArgs e)
        {
            UpdateUI("GAMEOVERMENU", UIMOD_ReturnFrontend.Checked);
        }
        private void UpdateUI(string file, bool modded)
        {
            if (AlienPAK.Format != CATHODE.PAKType.PAK2) AlienPAK.Open(SettingsManager.GetString("PATH_GameRoot") + "/DATA/UI.PAK");

            FileStream stream = File.Create(file);
            GetResourceStream((modded) ? "UI_Mods/" + file + "_MOD.GFX" : "UI_Mods/" + file + ".GFX").CopyTo(stream);
            stream.Close();

            AlienPAK.ImportFile("DATA/UI/" + file + ".GFX", file);
            File.Delete(file);

            SettingsManager.SetBool("UIOPT_" + file, modded);
        }
        protected static Stream GetResourceStream(string resourcePath)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            List<string> resourceNames = new List<string>(assembly.GetManifestResourceNames());

            resourcePath = resourcePath.Replace(@"/", ".");
            resourcePath = resourceNames.FirstOrDefault(r => r.Contains(resourcePath));

            if (resourcePath == null)
                throw new FileNotFoundException("Resource not found");

            return assembly.GetManifestResourceStream(resourcePath);
        }
    }
}
