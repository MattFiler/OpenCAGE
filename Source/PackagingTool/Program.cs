/*
 * 
 * Created by Matt Filer
 * www.mattfiler.co.uk
 * 
 */

using Alien_Isolation_Mod_Tools;
using AlienPAK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PackagingTool
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            //Perform a version check
            VersionCheck VersionChecker = new VersionCheck();

            if (VersionChecker.updateCheckFailed)
            {
                //Update check failed
                Landing_Main mainLandingPage = new Landing_Main();
                mainLandingPage.Show();
            }
            else
            {
                //Update check succeeded
                if (VersionChecker.updateRequired)
                {
                    VersionChecker.Show();
                }
                else
                {
                    //In V0.7.6.0 the updater changed which means assets might not be extracted by older updaters
                    //If this is the case, re-run OUR updater to fix that
                    ToolPaths Paths = new ToolPaths();
                    if (!Directory.Exists(Paths.GetPath(ToolPaths.Paths.FOLDER_ASSETS)))
                    {
                        VersionChecker.RunUpdater();
                    }

                    Landing_Main mainLandingPage = new Landing_Main();
                    mainLandingPage.Show();
                }
            }

            Application.Run();
        }
    }
}
