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
        public string MissionID;
        public string Language;

        public LocalisedText(string _TextValue, string _MissionID, string _Language)
        {
            TextValue = _TextValue;
            MissionID = _MissionID;
            Language = _Language;
        }
    }

    class LocalisationHandler
    {
        Directories AlienDirectories = new Directories();

        public enum AYZ_Lang { CZECH, ENGLISH, FRENCH, GERMAN, ITALIAN, POLISH, PORTUGUESE, RUSSIAN, SPANISH }
        private string[] languageFolder = { "CZECH", "ENGLISH", "FRENCH", "GERMAN", "ITALIAN", "POLISH", "PORTUGUESE", "RUSSIAN", "SPANISH" };

        private string textFolder = "";
        public LocalisationHandler()
        {
            textFolder = AlienDirectories.GameDirectoryRoot() + @"\DATA\TEXT\";
        }

        /* Get a localised string from the game's string bank */
        public LocalisedText GetLocalisedString(string ID, AYZ_Lang Language)
        {
            //Get all text banks in our language's directory
            string[] textFiles = Directory.GetFiles(textFolder + languageFolder[(int)Language] + @"\", "*.TXT", SearchOption.TopDirectoryOnly);

            //For each text bank, search for the requested ID and return its string
            foreach (string textFile in textFiles)
            {
                string[] textFileContent = File.ReadAllLines(textFile);
                for (int i = 0; i < textFileContent.Length; i++)
                {
                    if (textFileContent[i] == "[" + ID + "]")
                    {
                        LocalisedText theString = new LocalisedText();
                        theString.TextValue = textFileContent[i + 1].Substring(1, textFileContent[i + 1].Length - 2); //TODO: support multiline strings
                        theString.MissionID = Path.GetFileNameWithoutExtension(textFile);
                        theString.Language = languageFolder[(int)Language];

                        return theString;
                    }
                }
            }

            //Couldn't find string, this is probably a pretty big issue, so throw here.
            throw new InvalidOperationException("Requested to find localised string which does not exist! Fatal!");
        }

        /*
         * 
         * TODO List:
         *  - Support listing ALL localisation strings for GUI's sake
         *  - Support editing a string across languages
         *  - Support multiline strings
         * 
        */
    }
}
