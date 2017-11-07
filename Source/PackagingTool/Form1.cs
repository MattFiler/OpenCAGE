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

namespace PackagingTool
{
    public partial class Form1 : Form
    {
        string workingDirectory = Directory.GetCurrentDirectory() + @"\Conversion Directory\"; //Our working dir
        string gameDirectory = ""; //Our game's dir, set on form load

        /* ONLOAD */
        public Form1()
        {
            InitializeComponent();

            /* CREATE REQUIRED FOLDER */
            Directory.CreateDirectory(workingDirectory);

            /* SET GAME FOLDER */
            if (!File.Exists(Directory.GetCurrentDirectory() + @"\gamelocation.txt"))
            {
                MessageBox.Show("Please locate your Alien: Isolation executable (AI.exe).");
                OpenFileDialog selectGameFile = new OpenFileDialog();
                string[] gameDirectoryArray = { Path.GetDirectoryName(selectGameFile.FileName) };
                File.WriteAllLines(Directory.GetCurrentDirectory() + @"\gamelocation.txt", gameDirectoryArray); //Write new file
            }
            gameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\gamelocation.txt"); //Set our game's dir

            /* SET BUTTONS */
            if (File.Exists(workingDirectory + "Alien_Breakout.xml") && File.Exists(workingDirectory + "android_aggressive_search_check.xml"))
            {
                /* ALREADY UNPACKED */
                unpackButton.Enabled = false;
                repackButton.Enabled = true;
                resetTrees.Enabled = true;
            }
            else
            {
                /* NOT UNPACKED YET */
                unpackButton.Enabled = true;
                repackButton.Enabled = false;
                resetTrees.Enabled = true;
            }

            /* VALIDATE GAME DIRECTORY */
            if (!File.Exists(gameDirectory + @"\DATA\BINARY_BEHAVIOR\_DIRECTORY_CONTENTS.BML"))
            {
                MessageBox.Show("Please ensure you have selected the correct game install location. Missing files!");
                unpackButton.Enabled = false;
                repackButton.Enabled = false;
                resetTrees.Enabled = false;
                File.Delete(Directory.GetCurrentDirectory() + @"\gamelocation.txt");
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
                    if (File.Exists(gameDirectory + @"\DATA\BINARY_BEHAVIOR\gameismodded.txt"))
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
            MessageBox.Show("Behaviour trees are exported and ready to use with Brainiac Designer.");
            unpackButton.Enabled = false;
            repackButton.Enabled = true;
            resetTrees.Enabled = true;
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
            string[] moddedGameText = { "DO NOT DELETE THIS FILE UNLESS RESETTING TO DEFAULT BML" };
            File.WriteAllLines(gameDirectory + @"\DATA\BINARY_BEHAVIOR\gameismodded.txt", moddedGameText); //Write modded game text
            File.Delete(workingDirectory + "_DIRECTORY_CONTENTS.bml");
            File.Delete(workingDirectory + "_DIRECTORY_CONTENTS.xml");

            /* DONE */
            Cursor.Current = Cursors.Default;
            MessageBox.Show("Custom behaviour trees have been imported to Alien: Isolation.");
            unpackButton.Enabled = true;
            repackButton.Enabled = false;
            resetTrees.Enabled = true;
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
            File.WriteAllBytes(gameDirectory + @"\DATA\BINARY_BEHAVIOR\_DIRECTORY_CONTENTS.BML", PackagingTool.Properties.Resources._DIRECTORY_CONTENTS);
            File.Delete(gameDirectory + @"\DATA\BINARY_BEHAVIOR\gameismodded.txt");
            
            /* DONE */
            Cursor.Current = Cursors.Default;
            MessageBox.Show("Behaviour trees have been reset to defaults. Custom behaviours have been removed from the game.");
            unpackButton.Enabled = true;
            repackButton.Enabled = false;
            resetTrees.Enabled = true;
        }
    }
}
