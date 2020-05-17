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
            //Need DLLs in directory for image previews to work in content tool :(
            if (!File.Exists("DirectXTexNet.dll")) File.WriteAllBytes("DirectXTexNet.dll", Alien_Isolation_Mod_Tools.Properties.Resources.DirectXTexNet);
            Directory.CreateDirectory("x64");
            if (!File.Exists("x64/DirectXTexNetImpl.dll")) File.WriteAllBytes("x64/DirectXTexNetImpl.dll", Alien_Isolation_Mod_Tools.Properties.Resources.DirectXTexNetImpl_64);

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
