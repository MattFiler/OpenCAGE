using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenCAGE.ConfigEditors
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
        public enum AYZ_Lang { CZECH, ENGLISH, FRENCH, GERMAN, ITALIAN, POLISH, PORTUGUESE, RUSSIAN, SPANISH }
        public string[] languageFolders = { "CZECH", "ENGLISH", "FRENCH", "GERMAN", "ITALIAN", "POLISH", "PORTUGUESE", "RUSSIAN", "SPANISH" };

        private string textFolder = "";
        public LocalisationHandler()
        {
            textFolder = Singleton.PathToAI + @"\DATA\TEXT\";
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

        /* All string entries for a language in file order — one disk read per .TXT (for bulk UI) */
        public List<LocalisedText> GetAllStringsWithValues(AYZ_Lang language)
        {
            string langFolder = languageFolders[(int)language];
            var all = new List<LocalisedText>();
            foreach (string textFile in GetTextFiles(language))
            {
                string missionId = Path.GetFileNameWithoutExtension(textFile);
                string[] lines = File.ReadAllLines(textFile);
                ParseStringsFromLines(lines, missionId, langFolder, all);
            }
            return all;
        }

        /* Same parsing rules as GetLocalisedString: [ID] then body lines until accumulated text ends with '}' */
        private static void ParseStringsFromLines(string[] lines, string missionId, string langFolder, List<LocalisedText> appendTo)
        {
            int i = 0;
            while (i < lines.Length)
            {
                string line = lines[i];
                if (line.Length == 0 || line[0] != '[')
                {
                    i++;
                    continue;
                }

                string id = line.Substring(1, line.Length - 2);
                var sb = new StringBuilder();
                int j = i;
                do
                {
                    j++;
                    if (j >= lines.Length)
                        break;
                    string thisLine = lines[j];
                    if (thisLine == "")
                        thisLine = "\r\n\r\n";
                    sb.Append(thisLine);
                } while (sb.Length == 0 || sb[sb.Length - 1] != '}');

                if (sb.Length >= 2 && sb[sb.Length - 1] == '}')
                {
                    string value = sb.ToString(1, sb.Length - 2);
                    appendTo.Add(new LocalisedText(value, id, missionId, langFolder));
                    i = j + 1;
                }
                else
                {
                    i++;
                }
            }
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

        /* Mission / text-file names (without .TXT) for a language, sorted for UI */
        public List<string> GetSortedMissionIds(AYZ_Lang language)
        {
            var ids = new List<string>();
            foreach (string path in GetTextFiles(language))
                ids.Add(Path.GetFileNameWithoutExtension(path));
            ids.Sort(StringComparer.OrdinalIgnoreCase);
            return ids;
        }

        /* Append a new [id]{...} block to every language file for this mission. Fails if any file is missing or ID already exists anywhere in that mission set. */
        public bool TryAppendNewString(string missionId, string textId, string[] textValuesPerLanguage, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(missionId))
            {
                errorMessage = "No text file was selected.";
                return false;
            }

            textId = textId.Trim();
            if (textId.Length == 0)
            {
                errorMessage = "Enter a string ID.";
                return false;
            }

            if (textId.IndexOf('[') >= 0 || textId.IndexOf(']') >= 0)
            {
                errorMessage = "String ID cannot contain [ or ].";
                return false;
            }

            if (textValuesPerLanguage == null || textValuesPerLanguage.Length != languageFolders.Length)
            {
                errorMessage = "Internal error: expected one text value per language.";
                return false;
            }

            string marker = "[" + textId + "]";

            for (int i = 0; i < languageFolders.Length; i++)
            {
                string path = GetMissionTextFilePath(missionId, languageFolders[i]);
                if (!File.Exists(path))
                {
                    errorMessage = "Missing file for " + languageFolders[i] + ": " + missionId + ".TXT";
                    return false;
                }

                foreach (string line in File.ReadAllLines(path, Encoding.Unicode))
                {
                    if (line == marker)
                    {
                        errorMessage = "The ID \"" + textId + "\" already exists in " + languageFolders[i] + "\\" + missionId + ".TXT";
                        return false;
                    }
                }
            }

            for (int i = 0; i < languageFolders.Length; i++)
            {
                string path = GetMissionTextFilePath(missionId, languageFolders[i]);
                AppendBlockToTextFile(path, marker, textValuesPerLanguage[i] ?? string.Empty);
            }

            return true;
        }

        private string GetMissionTextFilePath(string missionId, string languageFolderName)
        {
            return textFolder + languageFolderName + @"\" + missionId + ".TXT";
        }

        private static void AppendBlockToTextFile(string path, string markerLine, string textValue)
        {
            var lines = new List<string>(File.ReadAllLines(path, Encoding.Unicode));
            if (lines.Count > 0 && lines[lines.Count - 1].Length > 0)
                lines.Add(string.Empty);
            lines.Add(markerLine);
            lines.Add("{" + textValue + "}");
            File.WriteAllLines(path, lines, Encoding.Unicode);
        }
    }
}
