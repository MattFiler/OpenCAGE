﻿using Steamworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace OpenCAGE
{
    /// <summary>
    /// Interaction logic for LandingWPF.xaml
    /// </summary>
    public partial class LandingWPF : UserControl
    {
        public Action<Process> OnToolOpened;
        public Action<Process> OnToolClosed;

        public Action OnUpdateRequest;
        public Action OnSettingsRequest;

        public LandingWPF()
        {
            InitializeComponent();
        }
        public void SetVersionInfo(string version)
        {
            string branchText = ((SettingsManager.IsOfflineMode) ? SettingsManager.GetString("META_GameVersion") : SettingsManager.GetString("CONFIG_RemoteBranch"));
            try
            {
                if (SettingsManager.IsSteamworks)
                {
                    Steamworks.SteamApps.GetCurrentBetaName(out string betaname, 100);
                    if (betaname != "")
                        branchText += " " + betaname.ToUpper();
                }
            }
            catch { }

            BranchText.Content = " [" + branchText + "]";
            VersionText.Content = "Version " + version;
        }

        /* App launch buttons */
        private void OpenAssetEditor(object sender, RoutedEventArgs e)
        {
            StartProcess("asseteditor/AlienPAK.exe");
            UnlockAchievement(SteamAchievements.ASSET_EDITOR_LAUNCH);
        }
        private void OpenConfigurationEditor(object sender, RoutedEventArgs e)
        {
            StartProcess("configeditor/AlienConfigEditor.exe");
            UnlockAchievement(SteamAchievements.CONFIG_EDITOR_LAUNCH);
        }
        private void OpenScriptEditor(object sender, RoutedEventArgs e)
        {
            StartProcess("scripteditor/CommandsEditor.exe");
            UnlockAchievement(SteamAchievements.SCRIPT_EDITOR_LAUNCH);
        }
        private void OpenBehaviourEditor(object sender, RoutedEventArgs e)
        {
            StartProcess("legendplugin/BehaviourTreeEditor.exe");
            UnlockAchievement(SteamAchievements.BEHAVIOUR_EDITOR_LAUNCH);
        }
        private void OpenGameLauncher(object sender, RoutedEventArgs e)
        {
            StartProcess("launchgame/LaunchGame.exe");
        }

        /* Toolbar buttons */
        private void SettingsButtonClick(object sender, RoutedEventArgs e)
        {
            OnSettingsRequest?.Invoke();
        }
        private void BackupButtonClick(object sender, RoutedEventArgs e)
        {
            StartProcess("levelbackup/LevelBackup.exe");
        }
        private void GithubButtonClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/MattFiler/OpenCAGE");
        }
        private void DocsButtonClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://opencage.co.uk/docs/");
        }
        private void DiscordButtonClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://discord.gg/JJ4ECu9hpY");
        }
        private void TwitterButtonClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://twitter.com/MattFiler");
        }
        private void BlueskyButtonClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://bsky.app/profile/mattfiler.co.uk");
        }

        /* Start a process from the remote directory */
        private void StartProcess(string path)
        {
            string pathToExe = SettingsManager.GetString("PATH_GameRoot") + "/DATA/MODTOOLS/REMOTE_ASSETS/" + path;
            if (!File.Exists(pathToExe))
            {
                OnUpdateRequest?.Invoke();
                return;
            }

            Process process = new Process();
            process.Exited += Process_Exited;
            process.StartInfo.FileName = pathToExe;
            process.StartInfo.Arguments = "-pathToAI=\"" + SettingsManager.GetString("PATH_GameRoot") + "\"";
            process.StartInfo.WorkingDirectory = pathToExe.Substring(0, pathToExe.Length - System.IO.Path.GetFileName(pathToExe).Length);
            process.Start();

            OnToolOpened?.Invoke(process);
        }
        private void Process_Exited(object sender, EventArgs e)
        {
            OnToolClosed?.Invoke((Process)sender);
        }

        /* Unlock a Steam achievement */
        public enum SteamAchievements { SCRIPT_EDITOR_LAUNCH, ASSET_EDITOR_LAUNCH, CONFIG_EDITOR_LAUNCH, BEHAVIOUR_EDITOR_LAUNCH }
        public void UnlockAchievement(SteamAchievements achievement)
        {
            if (!SettingsManager.IsSteamworks)
                return;

            bool result = SteamUserStats.SetAchievement(achievement.ToString());
            if (result)
                SteamUserStats.StoreStats();
            else
                Console.WriteLine("Failed to unlock achievement: " + achievement.ToString());
        }

    }
}
