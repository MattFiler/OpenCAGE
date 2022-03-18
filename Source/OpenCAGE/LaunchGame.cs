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
        string loaderFinalPath = "";
        string cinetoolsFinalPath = "";

        /* On init, if we are trying to launch to a map, skip GUI */
        public LaunchGame(string MapToLaunchTo = "")
        {
            if (MapToLaunchTo != "")
            {
                LaunchToMap(MapToLaunchTo);
                return;
            }

            InitializeComponent();

            loaderFinalPath = SettingsManager.GetString("PATH_GameRoot") + "/winmm.dll";
            cinetoolsFinalPath = SettingsManager.GetString("PATH_GameRoot") + "cinematictools.asi";

            enableCinematicTools.Checked = File.Exists(cinetoolsFinalPath) && File.Exists(loaderFinalPath);
            enableCinematicTools.Enabled = SettingsManager.GetString("META_GameVersion") == GameBuild.STEAM.ToString();

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
            if (MapName == "Frontend")
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
                //MAGIC NUMBERS :)
                if (SettingsManager.GetString("META_GameVersion") == GameBuild.STEAM.ToString()) alienIsolationBinary[15676275 + i] = mapStringByteArray[i];
                if (SettingsManager.GetString("META_GameVersion") == GameBuild.EPIC_GAMES_STORE.ToString()) alienIsolationBinary[15773411 + i] = mapStringByteArray[i];
            }

            //Write back out the edit
            BinaryWriter alienWriter = new BinaryWriter(File.OpenWrite(SettingsManager.GetString("PATH_GameRoot") + "/AI.exe"));
            alienWriter.BaseStream.SetLength(0);
            alienWriter.Write(alienIsolationBinary); 
            alienWriter.Close();

            //Start game process (includes a lot of hacky fixes for EGS)
            ProcessStartInfo alienProcess = new ProcessStartInfo();
            alienProcess.WorkingDirectory = SettingsManager.GetString("PATH_GameRoot");
            alienProcess.FileName = SettingsManager.GetString("PATH_GameRoot") + "/AI.exe";
            #region EPIC_GAMES_OVERRIDES
            if (SettingsManager.GetString("META_GameVersion") == GameBuild.EPIC_GAMES_STORE.ToString())
            {
                foreach (var process in Process.GetProcessesByName("EpicGamesLauncher")) process.Kill();
                string epicConfigPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\EpicGamesLauncher\\Saved\\Config\\Windows\\GameUserSettings.ini";
                string alienEpicID = "8935bb3e1420443a9789fe01758039a5";
                List<string> epicConfig = File.ReadAllLines(epicConfigPath).ToList<string>();
                List<string> trimmedEpicConfig = new List<string>();
                for (int i = 0; i < epicConfig.Count; i++)
                {
                    if (epicConfig[i].Length > alienEpicID.Length + 26 && epicConfig[i].Substring(0, alienEpicID.Length + 26) == alienEpicID + "_AdditionalCommandsEnabled") continue;
                    else if (epicConfig[i].Length > alienEpicID.Length + 19 && epicConfig[i].Substring(0, alienEpicID.Length + 19) == alienEpicID + "_AdditionalCommands") continue;
                    trimmedEpicConfig.Add(epicConfig[i]);
                }
                epicConfig = trimmedEpicConfig;
                int insertIndex = -1;
                for (int i = 0; i < epicConfig.Count; i++)
                {
                    if (epicConfig[i].Contains(alienEpicID + "_AutoUpdate"))
                    {
                        insertIndex = i;
                        break;
                    }
                }
                if (loadAsBenchmark)
                {
                    if (insertIndex != -1)
                    {
                        epicConfig.Insert(insertIndex + 1, alienEpicID + "_AdditionalCommands=-benchmark");
                        epicConfig.Insert(insertIndex + 1, alienEpicID + "_AdditionalCommandsEnabled=True");
                    }
                }
                File.WriteAllLines(epicConfigPath, epicConfig);
            }
            #endregion
            else if (loadAsBenchmark) alienProcess.Arguments = "-benchmark";
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

        /* Enable/disable the Cinematic Tools */
        private void enableCinematicTools_CheckedChanged(object sender, EventArgs e)
        {
            //Add/remove resources into the game directory
            if (enableCinematicTools.Checked)
            {
                if (!File.Exists(loaderFinalPath))
                {
                    FileStream stream = File.Create(loaderFinalPath);
                    GetResourceStream("ASILoader/Ultimate-ASI-Loader-x86.dll").CopyTo(stream);
                    stream.Close();
                }
                if (!File.Exists(cinetoolsFinalPath)) File.Copy(SettingsManager.GetString("PATH_GameRoot") + "/DATA/MODTOOLS/REMOTE_ASSETS/cinematictools/CT_AlienIsolation.dll", cinetoolsFinalPath);
            }
            else
            {
                if (File.Exists(loaderFinalPath)) File.Delete(loaderFinalPath);
                if (File.Exists(cinetoolsFinalPath)) File.Delete(cinetoolsFinalPath);
            }

            //Update the AI exe to disable ASLR
            try
            {
                if (!SettingsManager.GetBool("PATCH_PatchedForASLR"))
                {
                    BinaryWriter writer = new BinaryWriter(File.OpenWrite(SettingsManager.GetString("PATH_GameRoot") + "/AI.exe"));
                    writer.BaseStream.Position = 408;
                    writer.Write(new byte[] { 0xbf, 0x7e });
                    writer.BaseStream.Position += 4;
                    writer.Write((byte)0x00);
                    writer.Close();
                    SettingsManager.SetBool("PATCH_PatchedForASLR", true);
                }
            }
            catch (Exception ex) 
            {
                MessageBox.Show("Failed to disable ASLR in AI.exe!\nCinematic tools may not work.", "Failed!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
