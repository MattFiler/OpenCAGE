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
            SettingsManager.IsOfflineMode = args.Contains("-offline_mode") && Directory.Exists("Assets");

#if !DEBUG
            if (File.Exists("steam_api64.dll") && Directory.Exists("Assets"))
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
            else
            {
                if ((File.Exists("steam_api64.dll") && !Directory.Exists("Assets")) || (!File.Exists("steam_api64.dll") && Directory.Exists("Assets")))
                {
                    MessageBox.Show("Please verify your OpenCAGE installation through the Steam client.", "Missing files", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                    Environment.Exit(0);
                    return;
                }
            }
#endif

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(true);
            Application.Run(new Landing());
        }
    }
}
