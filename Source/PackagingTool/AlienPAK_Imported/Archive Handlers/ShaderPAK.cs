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
     * Shader PAK handler.
     * Currently doesn't support import/export. WIP!
     * Also doesn't parse much info out, although the basic file structure is there for the initial PAK.
     * Work needs to be done on parsing the _BIN and how that links to the initial PAK.
     * 
    */
    class ShaderPAK : AnyPAK
    {
        List<CathodeShaderHeader> HeaderDump = new List<CathodeShaderHeader>();

        /* Initialise the ShaderPAK class with the intended location (existing or not) */
        public ShaderPAK(string PathToPAK)
        {
            FilePathPAK = PathToPAK;
            FilePathBIN = PathToPAK.Substring(0, PathToPAK.Length - Path.GetFileName(PathToPAK).Length) + Path.GetFileNameWithoutExtension(FilePathPAK) + "_BIN.PAK";
        }

        /* Load the contents of an existing ShaderPAK set (massive WIP) */
        public override PAKReturnType Load()
        {
            if (!File.Exists(FilePathPAK))
            {
                return PAKReturnType.FAIL_COULD_NOT_ACCESS_FILE;
            }
            
            try
            {
                //The main PAK is unhandled for now, as I can't work out how the main (initial) PAK relates to the _BIN.PAK. Entry counts are different, and no noticeable indexes!
                /*
                //Open PAK
                BinaryReader ArchiveFile = new BinaryReader(File.OpenRead(FilePathPAK));
                ExtraBinaryUtils BinaryUtils = new ExtraBinaryUtils();
                
                //Read shader headers
                for (int i = 0; i < NumOfStringPairs; i++)
                {
                    CathodeShaderHeader newHeaderEntry = new CathodeShaderHeader();

                    //Get the name (or type) of this header
                    ArchiveFile.BaseStream.Position += 32;
                    byte[] entryNameOrType = ArchiveFile.ReadBytes(40);
                    for (int x = 0; x < entryNameOrType.Length; x++)
                    {
                        if (entryNameOrType[x] != 0x00)
                        {
                            newHeaderEntry.ShaderType += (char)entryNameOrType[x];
                            continue;
                        }
                        break;
                    }

                    //Skip over the rest for now just so I can scrape all the names (this needs parsing obvs)
                    for (int x = 0; x < 999999; x++)
                    {
                        if (ArchiveFile.ReadBytes(4).SequenceEqual(new byte[] { 0xA4, 0xBB, 0x25, 0x77 }))
                        {
                            ArchiveFile.BaseStream.Position -= 4;
                            break;
                        }
                    }

                    HeaderDump.Add(newHeaderEntry);
                }

                //Done!
                ArchiveFile.Close();
                */

                BinaryReader ArchiveFileBin = new BinaryReader(File.OpenRead(FilePathBIN));

                //Validate our header magic (look at _IDX_REMAP as I think this should also be supported)
                ArchiveFileBin.BaseStream.Position = 4;
                if (!(ArchiveFileBin.ReadInt32() == 14 && ArchiveFileBin.ReadInt32() == 3))
                    return PAKReturnType.FAIL_ARCHIVE_IS_NOT_EXCPETED_TYPE;

                //Read entry count from header
                ArchiveFileBin.BaseStream.Position = 12;
                int EntryCount = ArchiveFileBin.ReadInt32();
                int EndOfHeaders = 0;

                //Skip rest of the main header
                ArchiveFileBin.BaseStream.Position = 32;

                //Pull each entry's individual header
                for (int i = 0; i < EntryCount; i++)
                {
                    CathodeShaderHeader newStringEntry = new CathodeShaderHeader();
                    ArchiveFileBin.BaseStream.Position += 8; //skip blanks
                    
                    newStringEntry.FileLength = ArchiveFileBin.ReadInt32();
                    newStringEntry.FileLengthWithPadding = ArchiveFileBin.ReadInt32();
                    newStringEntry.FileOffset = ArchiveFileBin.ReadInt32(); 

                    ArchiveFileBin.BaseStream.Position += 8; //skip blanks

                    newStringEntry.StringPart1 = ArchiveFileBin.ReadBytes(4);
                    newStringEntry.FileIndex = ArchiveFileBin.ReadInt32(); //potentially actually int8 or int16 not 32

                    ArchiveFileBin.BaseStream.Position += 8; //skip blanks

                    newStringEntry.StringPart2 = ArchiveFileBin.ReadBytes(4);

                    //TEMP: For now I'm just setting the filename to be the index... need to work out how the _BIN relates to the initial .PAK to get names, etc
                    newStringEntry.FileName = newStringEntry.FileIndex + ".DXBC";
                    //END OF TEMP

                    HeaderDump.Add(newStringEntry);
                }
                EndOfHeaders = (int)ArchiveFileBin.BaseStream.Position;

                //Pull each entry's file content
                foreach (CathodeShaderHeader shaderEntry in HeaderDump)
                {
                    ArchiveFileBin.BaseStream.Position = shaderEntry.FileOffset + EndOfHeaders;
                    shaderEntry.FileContent = ArchiveFileBin.ReadBytes(shaderEntry.FileLength);
                }

                ArchiveFileBin.Close();
                return PAKReturnType.SUCCESS;
            }
            catch (IOException) { return PAKReturnType.FAIL_COULD_NOT_ACCESS_FILE; }
            catch (Exception) { return PAKReturnType.FAIL_UNKNOWN; }
        }

        /* Return a list of filenames for shaders in the ShaderPAK archive (massive WIP) */
        public override List<string> GetFileNames()
        {
            List<string> FileNameList = new List<string>();
            foreach (CathodeShaderHeader shaderEntry in HeaderDump)
            {
                FileNameList.Add(shaderEntry.FileName);
            }
            return FileNameList;
        }

        /* Get the size of the requested shader (not yet implemented) */
        public override int GetFilesize(string FileName)
        {
            return HeaderDump[GetFileIndex(FileName)].FileLength;
        }

        /* Find the shader entry object by name (not yet implemented) */
        protected override int GetFileIndex(string FileName)
        {
            for (int i = 0; i < HeaderDump.Count; i++)
            {
                if (HeaderDump[i].FileName == FileName)
                {
                    return i;
                }
            }
            throw new Exception("Could not find the requested file in ShaderPAK!");
        }

        /* Export a file from the ShaderPAK */
        public override PAKReturnType ExportFile(string PathToExport, string FileName)
        {
            try
            {
                File.WriteAllBytes(PathToExport, HeaderDump[GetFileIndex(FileName)].FileContent);
                return PAKReturnType.SUCCESS;
            }
            catch (IOException) { return PAKReturnType.FAIL_COULD_NOT_ACCESS_FILE; }
            catch (Exception) { return PAKReturnType.FAIL_UNKNOWN; }
        }
    }
}
