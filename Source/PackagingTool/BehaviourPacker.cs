/*
 * 
 * PackagingTool was created by Matt Filer
 * www.mattfiler.co.uk
 * 
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace PackagingTool
{
    public partial class BehaviourPacker : Form
    {
        string workingDirectory = Directory.GetCurrentDirectory() + @"\Behaviour Tree Directory\"; //Our working dir
        string gameDirectory = ""; //Our game's dir, set on form load

        //Settings
        string openFolderOnExport = "1";
        string openGameOnImport = "0";
        string showMessageBoxes = "1";

        /* ONLOAD */
        public BehaviourPacker()
        {
            InitializeComponent();
            
            //Get settings values
            getSettings();

            //Set game location
            gameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz"); //Set our game's dir

            //Bring to front
            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;

            //Set button states
            if (File.Exists(workingDirectory + "Alien_Breakout.xml") && File.Exists(workingDirectory + "android_aggressive_search_check.xml"))
            {
                /* ALREADY UNPACKED */
                unpackButton.Enabled = false;
                repackButton.Enabled = true;
                resetTrees.Enabled = false;
            }
            else
            {
                /* NOT UNPACKED YET */
                unpackButton.Enabled = true;
                repackButton.Enabled = false;
                resetTrees.Enabled = true;
            }
        }

        /* UNPACK */
        private void unpackButton_Click(object sender, EventArgs e)
        {
            /* STARTING */
            unpackButton.Enabled = false;
            repackButton.Enabled = false;
            resetTrees.Enabled = false;
            Cursor.Current = Cursors.WaitCursor;

            /* COPY _BINARY_BEHAVIOUR TO WORKING DIRECTORY */
            File.Copy(gameDirectory + @"\DATA\BINARY_BEHAVIOR\_DIRECTORY_CONTENTS.BML", workingDirectory + "_DIRECTORY_CONTENTS.BML");
            
            /* CONVERT _BINARY_BEHAVIOUR TO XML */
            new AlienConverter(workingDirectory + "_DIRECTORY_CONTENTS.BML", workingDirectory + "_DIRECTORY_CONTENTS.xml").Run();

            /* EXTRACT XML TO SEPARATE FILES */
            string directoryContentsXML = File.ReadAllText(workingDirectory + "_DIRECTORY_CONTENTS.xml"); //Get contents from newly converted _DIRECTORY_CONTENTS
            string fileHeader = "<?xml version='1.0' encoding='utf-8'?>\n<Behavior>"; //Premade file header
            int count = 0;

            foreach (string currentFile in Regex.Split(directoryContentsXML, "<File name="))
            {
                count += 1;
                if (count != 1)
                {
                    string[] extractedContents = Regex.Split(currentFile, "<Behavior>"); //Split filename and contents
                    string[] extractedContentsMain = Regex.Split(extractedContents[1], "</File>"); //Split contents and footer
                    string[] fileContents = { fileHeader, extractedContentsMain[0] }; //Write preset header and newly grabbed contents
                    string fileName = "";
                    if (File.Exists(gameDirectory + @"\DATA\BINARY_BEHAVIOR\gameismodded.txt") || //legacy
                        File.Exists(gameDirectory + @"\DATA\BINARY_BEHAVIOR\packagingtool_hasmodded.ayz"))
                    {
                        fileName = extractedContents[0].Substring(1, extractedContents[0].Length - 9); //Grab filename
                    }
                    else
                    {
                        fileName = extractedContents[0].Substring(1, extractedContents[0].Length - 11); //Grab filename UNMODDED FILE
                    }

                    File.WriteAllLines(workingDirectory + fileName + ".xml", fileContents); //Write new file
                }
            }

            /* DELETE EXCESS FILES */
            File.Delete(workingDirectory + "_DIRECTORY_CONTENTS.BML");
            File.Delete(workingDirectory + "_DIRECTORY_CONTENTS.xml");

            /* DONE */
            Cursor.Current = Cursors.Default;
            getSettings();
            if (showMessageBoxes == "1")
            {
                MessageBox.Show("Behaviour trees are exported and ready to use with Brainiac Designer.");
            }
            unpackButton.Enabled = false;
            repackButton.Enabled = true;
            resetTrees.Enabled = false;
            getSettings(); //Check for settings
            if (openFolderOnExport == "1")
            {
                Process.Start(workingDirectory); //Open output folder if requested
            }
        }

        /* REPACK */
        private void repackButton_Click(object sender, EventArgs e)
        {
            /* STARTING */
            unpackButton.Enabled = false;
            repackButton.Enabled = false;
            resetTrees.Enabled = false;
            Cursor.Current = Cursors.WaitCursor;

            /* WRITE NEW _DIRECTORY_CONTENTS XML AND DELETE FILES */
            string compiledBinaryBehaviourContents = "<?xml version=\"1.0\" encoding=\"utf-8\"?><DIR>"; //Start file

            DirectoryInfo workingDirectoryInfo = new DirectoryInfo(workingDirectory); //Get all files in directory
            foreach (FileInfo currentFile in workingDirectoryInfo.GetFiles())
            {
                string fileContents = File.ReadAllText(currentFile.FullName); //Current file contents
                string fileName = currentFile.Name; //Current file name
                string customFileHeader = "<File name=\"" + fileName.Substring(0, fileName.Length - 3) + "bml\">"; //File header
                string customFileFooter = "</File>"; //File footer

                compiledBinaryBehaviourContents += customFileHeader + fileContents.Substring(38) + customFileFooter; //Add to file string

                currentFile.Delete(); //Delete file before finishing
            }

            compiledBinaryBehaviourContents += "</DIR>"; //Finish off file string

            string[] compiledContentsAsArray = { compiledBinaryBehaviourContents };
            File.WriteAllLines(workingDirectory + "_DIRECTORY_CONTENTS.xml", compiledContentsAsArray); //Write new file

            /* CONVERT _BINARY_BEHAVIOUR TO BML */
            new AlienConverter(workingDirectory + "_DIRECTORY_CONTENTS.xml", workingDirectory + "_DIRECTORY_CONTENTS.bml").Run();

            /* COPY _BINARY_BEHAVIOUR TO GAME AND DELETE FILES */
            File.Delete(gameDirectory + @"\DATA\BINARY_BEHAVIOR\_DIRECTORY_CONTENTS.BML");
            File.Copy(workingDirectory + "_DIRECTORY_CONTENTS.bml", gameDirectory + @"\DATA\BINARY_BEHAVIOR\_DIRECTORY_CONTENTS.BML");
            string[] moddedGameText = { "DO NOT DELETE THIS FILE\nPACKAGINGTOOL HAS MODIFIED GAME FILES" };
            File.WriteAllLines(gameDirectory + @"\DATA\BINARY_BEHAVIOR\packagingtool_hasmodded.ayz", moddedGameText); //Write modded game text
            File.Delete(workingDirectory + "_DIRECTORY_CONTENTS.bml");
            File.Delete(workingDirectory + "_DIRECTORY_CONTENTS.xml");

            /* DONE */
            Cursor.Current = Cursors.Default;
            getSettings();
            if (showMessageBoxes == "1")
            {
                MessageBox.Show("Custom behaviour trees have been imported to Alien: Isolation.");
            }
            unpackButton.Enabled = true;
            repackButton.Enabled = false;
            resetTrees.Enabled = true;
            getSettings(); //Check for settings
            if (openGameOnImport == "1")
            {
                Process.Start(gameDirectory + @"\AI.exe"); //Open output folder if requested
            }
        }

        /* RESET */
        private void resetTrees_Click(object sender, EventArgs e)
        {
            /* STARTING */
            unpackButton.Enabled = false;
            repackButton.Enabled = false;
            resetTrees.Enabled = false;
            Cursor.Current = Cursors.WaitCursor;

            /* COPY ORIGINAL FILE FROM PROJECT MEMORY */
            File.WriteAllBytes(gameDirectory + @"\DATA\BINARY_BEHAVIOR\_DIRECTORY_CONTENTS.BML", Alien_Isolation_Mod_Tools.Properties.Resources._DIRECTORY_CONTENTS);
            File.Delete(gameDirectory + @"\DATA\BINARY_BEHAVIOR\gameismodded.txt"); //legacy
            File.Delete(gameDirectory + @"\DATA\BINARY_BEHAVIOR\packagingtool_hasmodded.ayz");

            /* DONE */
            Cursor.Current = Cursors.Default;
            getSettings();
            if (showMessageBoxes == "1")
            {
                MessageBox.Show("Behaviour trees have been reset to defaults. Custom behaviours have been removed from the game.");
            }
            unpackButton.Enabled = true;
            repackButton.Enabled = false;
            resetTrees.Enabled = true;
        }

        /* OPEN OPTIONS */
        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BehaviourPackerSettings settingsForm = new BehaviourPackerSettings();
            settingsForm.Show();
        }

        /* OPEN ATTRIBUTE EDITOR */
        private void attributeEditorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //CharEd attributeForm = new CharEd();
            //attributeForm.Show();
        }

        /* GET CURRENT SETTINGS */
        private void getSettings()
        {
            int loopCount = 0;
            foreach (var line in File.ReadLines(Directory.GetCurrentDirectory() + @"\modtools_settings.ayz"))
            {
                switch (line)
                {
                    case "0":
                        if (loopCount == 0)
                        {
                            openFolderOnExport = "0";
                        }
                        if (loopCount == 1)
                        {
                            openGameOnImport = "0";
                        }
                        if (loopCount == 2)
                        {
                            showMessageBoxes = "0";
                        }
                        break;
                    case "1":
                        if (loopCount == 0)
                        {
                            openFolderOnExport = "1";
                        }
                        if (loopCount == 1)
                        {
                            openGameOnImport = "1";
                        }
                        if (loopCount == 2)
                        {
                            showMessageBoxes = "1";
                        }
                        break;
                    default:
                        break;
                }
                loopCount += 1;
            }
        }

        /* FORM LOAD */
        private void Form1_Load(object sender, EventArgs e)
        {
            //Currently unused.
        }
    }
}
