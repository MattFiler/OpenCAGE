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
            SettingsManager.IsOfflineMode = args.Contains("-offline_mode") && File.Exists("Assets/assets.manifest");

            if (File.Exists("steam_api64.dll") && File.Exists("Assets/assets.manifest"))
            {
                try
                {
                    SettingsManager.IsOfflineMode = true;
                    Steamworks.SteamAPI.Init();
                    if (Steamworks.SteamAPI.RestartAppIfNecessary((Steamworks.AppId_t)3367530))
                    {
                        Application.Exit();
                        Environment.Exit(0);
                        return;
                    }
                    SettingsManager.IsSteamworks = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Steamworks Exception: " + e.ToString());
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(true);
            Application.Run(new Landing());
        }
    }
}
