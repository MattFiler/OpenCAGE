using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace OpenCAGE.Popups
{
    /// <summary>
    /// Interaction logic for AboutWPF.xaml
    /// </summary>
    public partial class AboutWPF : UserControl
    {
        public AboutWPF()
        {
            InitializeComponent();

            if (SettingsManager.GetString(Singleton.Settings.RemoteBranch) == "")
            {
                if (SettingsManager.GetBool(Singleton.Settings.UseStagingBranch))
                    SettingsManager.SetString(Singleton.Settings.RemoteBranch, "staging");
                else
                    SettingsManager.SetString(Singleton.Settings.RemoteBranch, "master");
            }

            string branchText = ((Singleton.IsOfflineMode) ? Singleton.Platform.ToString() : SettingsManager.GetString(Singleton.Settings.RemoteBranch));
            try
            {
                if (Singleton.IsSteamworks)
                {
                    Steamworks.SteamApps.GetCurrentBetaName(out string betaname, 100);
                    if (betaname != "")
                        branchText += " " + betaname.ToUpper();
                }
            }
            catch { }

            VersionText.Content = "[" + branchText + "] Version " + Singleton.Version;
        }

        private void GithubButtonClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/MattFiler/OpenCAGE");
        }
        private void DocsButtonClick(object sender, RoutedEventArgs e)
        {
            Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
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

    }
}
