using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AlienPAK
{
    /*
     *
     * Material Mapping PAK handler.
     * Supports the ability to edit the material remapping PAK file full of stripped XMLs. We produce our own XML to handle the remapping data.
     * Works similar to the PAK2 class with the Save() method, however the format still requires understanding of a couple odd binary tags to be able to reconstruct from scratch fully.
     * TODO comments mark the binary tags that still need to be figured out. I'm guessing they work similar to the unique ID tags in the COMMANDS.PAK.
     * 
    */
    class MaterialMapPAK : AnyPAK
    {
        List<EntryMaterialMappingsPAK> MaterialMappingEntries = new List<EntryMaterialMappingsPAK>();
        byte[] FileHeaderJunk = new byte[8];

        /* Initialise the MaterialMapPAK class with the intended location (existing or not) */
        public MaterialMapPAK(string PathToPAK)
        {
            FilePathPAK = PathToPAK;
        }

        /* Load the contents of an existing MaterialMapPAK */
        public override PAKReturnType Load()
        {
            if (!File.Exists(FilePathPAK))
            {
                return PAKReturnType.FAIL_TRIED_TO_LOAD_VIRTUAL_ARCHIVE;
            }

            try
            {
                //Open PAK
                BinaryReader ArchiveFile = new BinaryReader(File.OpenRead(FilePathPAK));

                //Parse header
                FileHeaderJunk = ArchiveFile.ReadBytes(8); //TODO: Work out what this contains
                int NumberOfFiles = ArchiveFile.ReadInt32();

                //Parse entries (XML is broken in the build files - doesn't get shipped)
                for (int x = 0; x < NumberOfFiles; x++)
                {
                    //This entry
                    EntryMaterialMappingsPAK NewMatEntry = new EntryMaterialMappingsPAK();
                    NewMatEntry.MapHeader = ArchiveFile.ReadBytes(4); //TODO: Work out the significance of this value, to be able to construct new PAKs from scratch.
                    NewMatEntry.MapEntryCoupleCount = ArchiveFile.ReadInt32();
                    NewMatEntry.MapJunk = ArchiveFile.ReadBytes(4); //TODO: Work out if this is always null.
                    for (int p = 0; p < (NewMatEntry.MapEntryCoupleCount * 2) + 1; p++)
                    {
                        //String
                        int NewMatStringLength = ArchiveFile.ReadInt32();
                        string NewMatString = "";
                        for (int i = 0; i < NewMatStringLength; i++)
                        {
                            NewMatString += ArchiveFile.ReadChar();
                        }

                        //First string is filename, others are materials
                        if (p == 0)
                        {
                            NewMatEntry.MapFilename = NewMatString;
                        }
                        else
                        {
                            NewMatEntry.MapMatEntries.Add(NewMatString);
                        }
                    }
                    MaterialMappingEntries.Add(NewMatEntry);
                }

                //Done!
                ArchiveFile.Close();
                return PAKReturnType.SUCCESS;
            }
            catch (IOException) { return PAKReturnType.FAIL_COULD_NOT_ACCESS_FILE; }
            catch (Exception) { return PAKReturnType.FAIL_UNKNOWN; }
        }

        /* Return a list of filenames for files in the MaterialMapPAK archive */
        public override List<string> GetFileNames()
        {
            List<string> FileNameList = new List<string>();
            foreach (EntryMaterialMappingsPAK MapEntry in MaterialMappingEntries)
            {
                FileNameList.Add(MapEntry.MapFilename);
            }
            return FileNameList;
        }

        /* Get the rough size of a material mapping entry based on its entries */
        public override int GetFilesize(string FileName)
        {
            int size = 0;
            foreach (string MatMap in MaterialMappingEntries[GetFileIndex(FileName)].MapMatEntries)
            {
                size += MatMap.Length;
            }
            return size;
        }

        /* Find the entry object by name */
        protected override int GetFileIndex(string FileName)
        {
            for (int i = 0; i < MaterialMappingEntries.Count; i++)
            {
                if (MaterialMappingEntries[i].MapFilename == FileName || MaterialMappingEntries[i].MapFilename == FileName.Replace('/', '\\'))
                {
                    return i;
                }
            }
            throw new Exception("Could not find the requested file in CommandPAK!");
        }

        /* Deconstruct a given XML and update material override info accordingly */
        public override PAKReturnType ReplaceFile(string PathToNewFile, string FileName)
        {
            try
            { 
                //Pull the new mapping info from the import XML
                XDocument InputFile = XDocument.Load(PathToNewFile);
                EntryMaterialMappingsPAK MaterialMapping = MaterialMappingEntries[GetFileIndex(FileName)];
                List<string> NewOverrides = new List<string>();
                foreach (XElement ThisMap in InputFile.Element("material_mappings").Elements())
                {
                    for (int i = 0; i < 2; i++)
                    {
                        XElement ThisElement = ThisMap.Elements().ElementAt(i);
                        string ThisMaterial = ThisElement.Value;
                        if (ThisElement.Attribute("arrow").Value == "true") { ThisMaterial = ThisMaterial + "->" + ThisMaterial; }
                        NewOverrides.Add(ThisMaterial);
                    }
                }

                //Apply the pulled info
                MaterialMapping.MapMatEntries = NewOverrides;
                MaterialMapping.MapEntryCoupleCount = MaterialMapping.MapMatEntries.Count / 2; //Should probably error if this is different.
                return PAKReturnType.SUCCESS;
            }
            catch (IOException) { return PAKReturnType.FAIL_COULD_NOT_ACCESS_FILE; }
            catch (Exception) { return PAKReturnType.FAIL_UNKNOWN; }
        }

        /* Construct an XML with given material info, and export it */
        public override PAKReturnType ExportFile(string PathToExport, string FileName)
        {
            try
            {
                XDocument OutputFile = XDocument.Parse("<material_mappings></material_mappings>");
                XElement MaterialPair = XElement.Parse("<map><original arrow='false'></original><override arrow='false'></override></map>");

                int ThisEntryNum = 0;
                EntryMaterialMappingsPAK ThisEntry = MaterialMappingEntries[GetFileIndex(FileName)];
                for (int i = 0; i < ThisEntry.MapEntryCoupleCount; i++)
                {
                    for (int x = 0; x < 2; x++)
                    {
                        string ThisMaterial = ThisEntry.MapMatEntries[ThisEntryNum]; ThisEntryNum++;
                        XElement ThisElement = MaterialPair.Elements().ElementAt(x);
                        if (ThisMaterial.Contains("->"))
                        {
                            //Remove the arrow split format as both sides are the same, and XML will web-ify the ">"
                            ThisMaterial = ThisMaterial.Split(new string[] { "->" }, StringSplitOptions.None)[0];
                            ThisElement.Attribute("arrow").Value = "true";
                        }
                        else
                        {
                            //Don't think there are any without the arrow format, but just in-case.
                            ThisElement.Attribute("arrow").Value = "false";
                        }
                        ThisElement.Value = ThisMaterial;
                    }
                    OutputFile.Element("material_mappings").Add(MaterialPair);
                }

                OutputFile.Save(PathToExport);
                return PAKReturnType.SUCCESS;
            }
            catch (IOException) { return PAKReturnType.FAIL_COULD_NOT_ACCESS_FILE; }
            catch (Exception) { return PAKReturnType.FAIL_UNKNOWN; }
        }

        /* Save out our material mappings archive */
        public override PAKReturnType Save()
        {
            try
            {
                //Re-write out to the PAK
                ExtraBinaryUtils BinaryUtils = new ExtraBinaryUtils();
                BinaryWriter ArchiveWriter = new BinaryWriter(File.OpenWrite(FilePathPAK));
                ArchiveWriter.BaseStream.SetLength(0);
                ArchiveWriter.Write(FileHeaderJunk);
                ArchiveWriter.Write(MaterialMappingEntries.Count);
                foreach (EntryMaterialMappingsPAK ThisMatRemap in MaterialMappingEntries)
                {
                    ArchiveWriter.Write(ThisMatRemap.MapHeader);
                    ArchiveWriter.Write(ThisMatRemap.MapEntryCoupleCount);
                    ArchiveWriter.Write(ThisMatRemap.MapJunk);
                    ArchiveWriter.Write(ThisMatRemap.MapFilename.Length);
                    BinaryUtils.WriteString(ThisMatRemap.MapFilename, ArchiveWriter);
                    foreach (string MaterialName in ThisMatRemap.MapMatEntries)
                    {
                        ArchiveWriter.Write(MaterialName.Length);
                        BinaryUtils.WriteString(MaterialName, ArchiveWriter);
                    }
                }
                ArchiveWriter.Close();

                return PAKReturnType.SUCCESS;
            }
            catch (IOException) { return PAKReturnType.FAIL_COULD_NOT_ACCESS_FILE; }
            catch (Exception) { return PAKReturnType.FAIL_UNKNOWN; }
        }
    }
}
