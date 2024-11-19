using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            SettingsManager.IsOfflineMode = args.Contains("-offline_mode");

            if (args.Contains("-steam"))
            {
                if (!File.Exists("steam_api64.dll"))
                    File.WriteAllBytes("steam_api64.dll", Properties.Resources.steam_api64);

                if (Steamworks.SteamAPI.Init())
                    Steamworks.SteamAPI.RestartAppIfNecessary((Steamworks.AppId_t)3352270);
                SettingsManager.IsSteamworks = true;
                SettingsManager.IsOfflineMode = true; //We force offline mode for Steam to pull Steam depot contents, rather than using GitHub updater
            } 

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(true);
            Application.Run(new Landing());
        }
    }
}
