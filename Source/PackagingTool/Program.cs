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
                    Landing_Main mainLandingPage = new Landing_Main();
                    mainLandingPage.Show();
                }
            }

            Application.Run();
        }
    }
}
