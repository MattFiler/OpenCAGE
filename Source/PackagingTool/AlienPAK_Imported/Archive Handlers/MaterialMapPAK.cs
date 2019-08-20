using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlienPAK
{
    /*
     *
     * Material Mapping PAK handler.
     * This PAK doesn't ship with full XMLs,so some kind of custom format needs to be implemented.
     * When this is done, import/export can be added.
     * 
    */
    class MaterialMapPAK : AnyPAK
    {
        List<EntryMaterialMappingsPAK> MaterialMappingEntries = new List<EntryMaterialMappingsPAK>();

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
                ArchiveFile.BaseStream.Position += 8;
                int NumberOfFiles = ArchiveFile.ReadInt32();

                //Parse entries (XML is broken in the build files - doesn't get shipped)
                for (int x = 0; x < NumberOfFiles; x++)
                {
                    //This entry
                    EntryMaterialMappingsPAK NewMatEntry = new EntryMaterialMappingsPAK();
                    NewMatEntry.MapHeader = ArchiveFile.ReadBytes(4);
                    NewMatEntry.MapEntryCoupleCount = ArchiveFile.ReadInt32();
                    ArchiveFile.BaseStream.Position += 4; //skip nulls (always nulls?)
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

        /* Get the file size of a MaterialMapPAK entry (kinda faked for now) */
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
    }
}
