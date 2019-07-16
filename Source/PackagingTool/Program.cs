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
                    //Update required, show progress bar
                    VersionChecker.Show();
                }
                else
                {
                    //Up-to-date
                    if (args.Length > 0 && File.Exists(args[0]))
                    {
                        //Work out what type of file we're launching with
                        AlienPAK_Wrapper.AlienContentType launchingWithContentType = AlienPAK_Wrapper.AlienContentType.UNKNOWN;
                        switch (Path.GetFileName(args[0]))
                        {
                            case "UI.PAK":
                                launchingWithContentType = AlienPAK_Wrapper.AlienContentType.UI;
                                break;
                            case "LEVEL_MODELS.PAK":
                                launchingWithContentType = AlienPAK_Wrapper.AlienContentType.MODEL;
                                break;
                            case "LEVEL_TEXTURES.ALL.PAK":
                                launchingWithContentType = AlienPAK_Wrapper.AlienContentType.TEXTURE;
                                break;
                        }

                        //Launch AlienPAK with the file
                        AlienPAK_Imported AlienPAK = new AlienPAK.AlienPAK_Imported(args, launchingWithContentType);
                        AlienPAK.Show();
                    }
                    else
                    {
                        //Not launching with a file (normal)
                        Landing_Main mainLandingPage = new Landing_Main();
                        mainLandingPage.Show();
                    }
                }
            }

            Application.Run();
        }
    }
}
