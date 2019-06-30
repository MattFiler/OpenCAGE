using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alien_Isolation_Mod_Tools.Attribute_Editors.Misc
{
    struct LocalisedText
    {
        public string TextValue;
        public string TextID;
        public string MissionID;
        public string Language;

        public LocalisedText(string _TextValue, string _TextID, string _MissionID, string _Language)
        {
            TextValue = _TextValue;
            TextID = _TextID;
            MissionID = _MissionID;
            Language = _Language;
        }
    }

    class LocalisationHandler
    {
        Directories AlienDirectories = new Directories();

        public enum AYZ_Lang { CZECH, ENGLISH, FRENCH, GERMAN, ITALIAN, POLISH, PORTUGUESE, RUSSIAN, SPANISH }
        public string[] languageFolders = { "CZECH", "ENGLISH", "FRENCH", "GERMAN", "ITALIAN", "POLISH", "PORTUGUESE", "RUSSIAN", "SPANISH" };

        private string textFolder = "";
        public LocalisationHandler()
        {
            textFolder = AlienDirectories.GameDirectoryRoot() + @"\DATA\TEXT\";
        }

        /* Get a localised string from the game's string bank */
        public LocalisedText GetLocalisedString(string ID, AYZ_Lang Language)
        {
            //For each text bank, search for the requested ID and return its string
            foreach (string textFile in GetTextFiles(Language))
            {
                string[] textFileContent = File.ReadAllLines(textFile);
                for (int i = 0; i < textFileContent.Length; i++)
                {
                    if (textFileContent[i] == "[" + ID + "]")
                    {
                        LocalisedText theString = new LocalisedText();
                        theString.TextValue = "";
                        int stringIndex = 1;
                        while (theString.TextValue.Length == 0 || theString.TextValue.Substring(theString.TextValue.Length-1) != "}")
                        {
                            string thisLine = textFileContent[i + stringIndex];
                            if (thisLine == "") { thisLine = "\r\n\r\n"; }
                            theString.TextValue += thisLine;
                            stringIndex++;
                        }
                        theString.TextValue = theString.TextValue.Substring(1, theString.TextValue.Length - 2);
                        theString.TextID = ID;
                        theString.MissionID = Path.GetFileNameWithoutExtension(textFile);
                        theString.Language = languageFolders[(int)Language];

                        return theString;
                    }
                }
            }

            //Couldn't find string, this is probably a pretty big issue, so throw here.
            throw new InvalidOperationException("Requested to find localised string which does not exist! Fatal!");
        }

        /* Update a localised string */
        public bool UpdateLocalisedString(LocalisedText Text)
        {
            string textFilePath = textFolder + Text.Language + @"\" + Text.MissionID + ".TXT";
            string[] textFile = File.ReadAllLines(textFilePath);
            List<string> newTextFile = new List<string>();

            //Get up to our string
            for (int i = 0; i < textFile.Length; i++)
            {
                newTextFile.Add(textFile[i]);
                if (textFile[i] == "[" + Text.TextID + "]")
                {
                    break;
                }
            }

            //Write new content
            newTextFile.Add("{" + Text.TextValue + "}");

            //Continue down to end of file
            bool startAdding = false;
            for (int i = newTextFile.Count-1; i < textFile.Length; i++)
            {
                if (!startAdding && textFile[i].Length > 0 && textFile[i].Substring(0, 1) == "[")
                {
                    startAdding = true;
                    newTextFile.Add("");
                }
                if (startAdding)
                {
                    newTextFile.Add(textFile[i]);
                }
            }

            //Write changes
            File.WriteAllLines(textFilePath, newTextFile, Encoding.Unicode);

            return true;
        }

        /* Get all text IDs by language */
        public List<LocalisedText> GetAllIDs(AYZ_Lang Language)
        {
            List<LocalisedText> textIDs = new List<LocalisedText>();
            
            foreach (string textFile in GetTextFiles(Language))
            {
                string[] textFileContent = File.ReadAllLines(textFile);
                for (int i = 0; i < textFileContent.Length; i++)
                {
                    if (textFileContent[i].Length > 0 && textFileContent[i].Substring(0, 1) == "[")
                    {
                        LocalisedText stringID = new LocalisedText();
                        stringID.TextValue = "N/A";
                        stringID.TextID = textFileContent[i].Substring(1, textFileContent[i].Length - 2);
                        stringID.MissionID = Path.GetFileNameWithoutExtension(textFile);
                        stringID.Language = languageFolders[(int)Language];

                        textIDs.Add(stringID);
                    }
                }
            }

            return textIDs;
        }

        /* Get all text file names by language */
        private string[] GetTextFiles(AYZ_Lang Language)
        {
            return Directory.GetFiles(textFolder + languageFolders[(int)Language] + @"\", "*.TXT", SearchOption.TopDirectoryOnly);
        }

        /*
         * 
         * TODO List:
         *  - Support editing a string across languages
         *  - Search ability to find a string that includes X
         * 
        */
    }
}
