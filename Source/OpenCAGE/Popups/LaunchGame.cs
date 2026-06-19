using CATHODE;
using CathodeLib;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

//Imported from LaunchGame tool

namespace OpenCAGE
{
    public partial class LaunchGame : Form
    {
        string _cinematicToolDLL = "";
        string _utilPath = "";

        public LaunchGame()
        {
            InitializeComponent();

            //Close the game down before we do anything
            EditorUtils.CloseAI(new List<string>(new string[] { "CinematicToolsInjector" }));

            PatchManager.PerformRecommendedPatches(Singleton.Platform, Singleton.PathToAI);

            _cinematicToolDLL = Singleton.PathToAI + "/DATA/MODTOOLS/REMOTE_ASSETS/cinematictools/CT_AlienIsolation.dll";
            _utilPath = Singleton.PathToAI + "/DATA/MODTOOLS/REMOTE_ASSETS/runtimeutils";

            enableCinematicTools.Checked = SettingsManager.GetBool(Settings.CinematicTools);
            enableRuntimeUtils.Checked = SettingsManager.GetBool(Settings.RuntimeUtils);
            disableUI.Checked = SettingsManager.GetBool(Settings.HudDisabled);
            skipFrontend.Checked = SettingsManager.GetBool(Settings.SkipFrontend);
            enableUIPerf.Checked = SettingsManager.GetBool(Settings.UiEnabledUiPerf);
            enableMemReplayLogs.Checked = SettingsManager.GetBool(Settings.MemReplayLogs);
            patchCurrentGen.Checked = SettingsManager.GetBool(Settings.PatchCurrentGen);
            renderConstantAmbient.Checked = SettingsManager.GetBool(Settings.RenderConstantAmbient);
            UIMOD_DebugCheckpoints.Checked = SettingsManager.GetBool(Settings.UiModPauseMenu);
            UIMOD_MapName.Checked = SettingsManager.GetBool(Settings.UiModLoadingScreen);
            UIMOD_MapSelection.Checked = SettingsManager.GetBool(Settings.UiModNewFrontendMenu);
            UIMOD_ReturnFrontend.Checked = SettingsManager.GetBool(Settings.UiModGameOverMenu);

            enableCinematicTools.Enabled = Singleton.Platform == PatchManager.Platform.STEAM && File.Exists(_cinematicToolDLL);
            enableRuntimeUtils.Enabled = Singleton.Platform == PatchManager.Platform.STEAM && Directory.Exists(_utilPath);

            EditorUtils.PopulateLevelDropdown(levelList);
        }

        /* Load game with given map name */
        private bool LaunchToMap(string MapName)
        {
            if (MapName.Length > 32)
            {
                MessageBox.Show("The name of the selected level is too long!\nPlease rename it.", "Level name too long.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            bool patchLaunch = PatchManager.PatchLaunchMode(Singleton.Platform, Singleton.PathToAI, MapName);
            bool patchIntegrity = PatchManager.PatchFileIntegrityCheck(Singleton.Platform, Singleton.PathToAI);
            bool patchMsg = PatchManager.PatchPopupMessage(Singleton.Platform, Singleton.PathToAI);
            if (!patchLaunch || !patchIntegrity || !patchMsg)
                MessageBox.Show("Failed to set level loading values in AI.exe!\nIs the game already open?", "Failed to patch binary.", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            PatchManager.UpdateLevelListInPackages(Singleton.Platform, Singleton.PathToAI);

            //Start game process 
            if (Singleton.Platform == PatchManager.Platform.STEAM)
            {
                Process.Start("steam://rungameid/214490");
            }
            else
            {
                ProcessStartInfo alienProcess = new ProcessStartInfo();
                alienProcess.WorkingDirectory = Singleton.PathToAI;
                alienProcess.FileName = Singleton.PathToAI + "/AI.exe";
                Process.Start(alienProcess);
            }
            Steam.UnlockAchievement(Steam.Achievements.LAUNCHED_GAME);
            return true;
        }

        /* Load game from GUI map selection */
        private void LaunchGame_Click(object sender, EventArgs e)
        {
            //Copy/delete runtime utils as requested
            string rtUtilASI = Singleton.PathToAI + "OpenCAGE_Utils.asi";
            string rtUtilDLL = Singleton.PathToAI + "d3d11.dll";
            if (SettingsManager.GetBool(Settings.RuntimeUtils))
            {
                try
                {
                    File.Copy(_utilPath + "/OpenCAGE_Utils.asi", rtUtilASI, true);
                    File.Copy(_utilPath + "/winmm.dll", rtUtilDLL, true);
                }
                catch
                {
                    if (!File.Exists(rtUtilASI) && !File.Exists(rtUtilDLL))
                        MessageBox.Show("Failed to enable hot reloading.", "Hot reload error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                try
                {
                    if (File.Exists(rtUtilASI)) File.Delete(rtUtilASI);
                    //if (File.Exists(rtUtilDLL)) File.Delete(rtUtilDLL);
                }
                catch { }
            }

            //Work out what option was selected and launch to it
            if (!LaunchToMap(levelList.Items[levelList.SelectedIndex].ToString()))
                return;

            //Enable Cinematic Tools if requested
            if (SettingsManager.GetBool(Settings.CinematicTools))
            {
                string injectorExe = Singleton.PathToAI + "/DATA/MODTOOLS/CinematicTools.exe";
                try
                {
                    File.WriteAllBytes(injectorExe, Properties.Resources.CinematicToolsInjector);
                }
                catch
                {
                    Debug.Log("Cinematic Tools", "Failed to write executable!");
                }

                if (!File.Exists(injectorExe))
                {
                    Debug.Log("Cinematic Tools", "Executable doesn't exist!");
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                        {
                            FileName = injectorExe,
                            Arguments = "-CinematicToolsDLL=\"" + _cinematicToolDLL + "\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    );
                }
            }
            this.Close();
        }

        /* Remember selected level */
        private void levelList_SelectedIndexChanged(object sender, EventArgs e)
        {
            SettingsManager.SetString(Settings.LastSelectedLevel, levelList.Items[levelList.SelectedIndex].ToString());
        }

        /* Enable/disable the Cinematic Tools */
        private void enableCinematicTools_CheckedChanged(object sender, EventArgs e)
        {
            SettingsManager.SetBool(Settings.CinematicTools, enableCinematicTools.Checked);
        }

        /* Enable/disable cUI rendering for UI perf stats (Cathode debug render) */ 
        private void enableUIPerf_CheckedChanged(object sender, EventArgs e)
        {
            SettingsManager.SetBool(Settings.UiEnabledUiPerf, enableUIPerf.Checked);
            if (!PatchManager.PatchUIPerfFlag(Singleton.Platform, Singleton.PathToAI, enableUIPerf.Checked))
                MessageBox.Show("Failed to set cUI UI perf option.\nIs Alien: Isolation open?", "Couldn't write!", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /* Enable/disable Mem_Replay_Logs */
        private void enableMemReplayLogs_CheckedChanged(object sender, EventArgs e)
        {
            SettingsManager.SetBool(Settings.MemReplayLogs, enableMemReplayLogs.Checked);
            if (!PatchManager.PatchMemReplayLogFlag(Singleton.Platform, Singleton.PathToAI, enableMemReplayLogs.Checked))
                MessageBox.Show("Failed to set memory logging option.\nIs Alien: Isolation open?", "Couldn't write!", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /* Enable/disable runtime utils */
        private void enableRuntimeUtils_CheckedChanged(object sender, EventArgs e)
        {
            SettingsManager.SetBool(Settings.RuntimeUtils, enableRuntimeUtils.Checked);
        }

        /* Enable/disable in-game HUD */
        private void disableUI_CheckedChanged(object sender, EventArgs e)
        {
            SettingsManager.SetBool(Settings.HudDisabled, disableUI.Checked);
            if (!PatchManager.PatchNoUIFlag(Singleton.Platform, Singleton.PathToAI, disableUI.Checked))
                MessageBox.Show("Failed to set HUD disabled option.\nIs Alien: Isolation open?", "Couldn't write!", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /* Skip Frontend (WARNING: Causes issues when returning to main menu - duh) */
        private void skipFrontend_CheckedChanged(object sender, EventArgs e)
        {
            SettingsManager.SetBool(Settings.SkipFrontend, skipFrontend.Checked);
            if (!PatchManager.PatchSkipFrontendFlag(Singleton.Platform, Singleton.PathToAI, skipFrontend.Checked))
                MessageBox.Show("Failed to set skip frontend option.\nIs Alien: Isolation open?", "Couldn't write!", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /* Current-gen script optimiser patch */
        private void patchCurrentGen_CheckedChanged(object sender, EventArgs e)
        {
            SettingsManager.SetBool(Settings.PatchCurrentGen, patchCurrentGen.Checked);
            if (!PatchManager.DisableCurrentGenOptimisations(Singleton.Platform, Singleton.PathToAI, patchCurrentGen.Checked))
                MessageBox.Show("Failed to set optimisation patch option.\nIs Alien: Isolation open?", "Couldn't write!", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /* Render constant ambient */
        private void renderConstantAmbient_CheckedChanged(object sender, EventArgs e)
        {
            SettingsManager.SetBool(Settings.RenderConstantAmbient, renderConstantAmbient.Checked);
            if (!PatchManager.PatchRenderConstantAmbientFlag(Singleton.Platform, Singleton.PathToAI, renderConstantAmbient.Checked))
                MessageBox.Show("Failed to set constant ambient patch option.\nIs Alien: Isolation open?", "Couldn't write!", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /* UI Modifications */
        PAK2 uiPAK = null;
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
            if (uiPAK == null)
                uiPAK = new PAK2(Singleton.PathToAI + "/DATA/UI.PAK");

            using (MemoryStream stream = new MemoryStream())
            using (BinaryReader reader = new BinaryReader(stream))
            {
                GetResourceStream((modded) ? "UI_Mods/" + file + "_MOD.GFX" : "UI_Mods/" + file + ".GFX").CopyTo(stream);
                reader.BaseStream.Position = 0;
                PAK2.File pakFile = uiPAK.Entries.FirstOrDefault(o => o.Filename == "DATA/UI/" + file + ".GFX");
                if (pakFile != null)
                    pakFile.Content = reader.ReadBytes((int)reader.BaseStream.Length);
            }

            uiPAK.Save();
            SettingsManager.SetBool(Settings.UiMod(file), modded);
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
