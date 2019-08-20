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
     * Model PAK handler.
     * Currently doesn't support import/export. WIP!
     * Also needs to verify the PAK version number, etc.
     * 
    */
    class ModelPAK : AnyPAK
    {
        List<CS2> ModelEntries = new List<CS2>();
        int TableCountPt1 = -1;
        int TableCountPt2 = -1;
        int FilenameListEnd = -1;
        int HeaderListEnd = -1;

        /* Initialise the ModelPAK class with the intended location (existing or not) */
        public ModelPAK(string PathToPAK)
        {
            FilePathPAK = PathToPAK;
            FilePathBIN = FilePathPAK.Substring(0, FilePathPAK.Length - Path.GetFileName(FilePathPAK).Length) + "MODELS_" + Path.GetFileName(FilePathPAK).Substring(0, Path.GetFileName(FilePathPAK).Length - 11) + ".BIN";
        }

        /* Load the contents of an existing ModelPAK */
        public override PAKReturnType Load()
        {
            if (!File.Exists(FilePathPAK))
            {
                return PAKReturnType.FAIL_TRIED_TO_LOAD_VIRTUAL_ARCHIVE;
            }

            /* TODO: Verify the PAK loading is a ModelPAK by BIN version number */

            try
            {
                //First, parse the MTL file to find material info
                string PathToMTL = FilePathPAK.Substring(0, FilePathPAK.Length - 3) + "MTL";
                BinaryReader ArchiveFileMtl = new BinaryReader(File.OpenRead(PathToMTL));

                //Header
                ArchiveFileMtl.BaseStream.Position += 40; //There are some knowns here, just not required for us yet
                int MaterialEntryCount = ArchiveFileMtl.ReadInt16();
                ArchiveFileMtl.BaseStream.Position += 2; //Skip unknown

                //Strings - more work will be done on materials eventually, 
                //but taking their names for now is good enough for model export
                List<string> MaterialEntries = new List<string>();
                string ThisMaterialString = "";
                for (int i = 0; i < MaterialEntryCount; i++)
                {
                    while (true)
                    {
                        byte ThisByte = ArchiveFileMtl.ReadByte();
                        if (ThisByte == 0x00)
                        {
                            MaterialEntries.Add(ThisMaterialString);
                            ThisMaterialString = "";
                            break;
                        }
                        ThisMaterialString += (char)ThisByte;
                    }
                }
                ArchiveFileMtl.Close();

                //Read the header info from BIN
                BinaryReader ArchiveFileBin = new BinaryReader(File.OpenRead(FilePathBIN));
                ArchiveFileBin.BaseStream.Position += 4; //Skip magic
                TableCountPt2 = ArchiveFileBin.ReadInt32();
                ArchiveFileBin.BaseStream.Position += 4; //Skip unknown
                TableCountPt1 = ArchiveFileBin.ReadInt32();

                //Skip past table 1
                for (int i = 0; i < TableCountPt1; i++)
                {
                    byte ThisByte = 0x00;
                    while (ThisByte != 0xFF)
                    {
                        ThisByte = ArchiveFileBin.ReadByte();
                    }
                }
                ArchiveFileBin.BaseStream.Position += 23;

                //Read file list info
                FilenameListEnd = ArchiveFileBin.ReadInt32();
                int FilenameListStart = (int)ArchiveFileBin.BaseStream.Position;

                //Read all file names (bytes)
                byte[] filename_bytes = ArchiveFileBin.ReadBytes(FilenameListEnd);

                //Read table 2 (skipping all unknowns for now)
                ExtraBinaryUtils BinaryUtils = new ExtraBinaryUtils();
                for (int i = 0; i < TableCountPt2; i++)
                {
                    CS2 new_entry = new CS2();
                    new_entry.FilenameOffset = ArchiveFileBin.ReadInt32();
                    new_entry.Filename = BinaryUtils.GetStringFromByteArray(filename_bytes, new_entry.FilenameOffset);
                    ArchiveFileBin.BaseStream.Position += 4;
                    new_entry.ModelPartNameOffset = ArchiveFileBin.ReadInt32();
                    new_entry.ModelPartName = BinaryUtils.GetStringFromByteArray(filename_bytes, new_entry.ModelPartNameOffset);
                    ArchiveFileBin.BaseStream.Position += 44;
                    new_entry.MaterialLibaryIndex = ArchiveFileBin.ReadInt32();
                    new_entry.MaterialName = MaterialEntries[new_entry.MaterialLibaryIndex];
                    ArchiveFileBin.BaseStream.Position += 8;
                    new_entry.BlockSize = ArchiveFileBin.ReadInt32();
                    ArchiveFileBin.BaseStream.Position += 14;
                    new_entry.ScaleFactor = ArchiveFileBin.ReadInt16(); //Maybe?
                    ArchiveFileBin.BaseStream.Position += 2;
                    new_entry.VertCount = ArchiveFileBin.ReadInt16();
                    new_entry.FaceCount = ArchiveFileBin.ReadInt16();
                    new_entry.BoneCount = ArchiveFileBin.ReadInt16();
                    ModelEntries.Add(new_entry);
                }
                ArchiveFileBin.Close();

                //Get extra info from each header in the PAK
                BinaryReader ArchiveFile = new BinaryReader(File.OpenRead(FilePathPAK));
                BigEndianUtils BigEndian = new BigEndianUtils();
                ArchiveFile.BaseStream.Position += 32; //Skip header
                for (int i = 0; i < TableCountPt2; i++)
                {
                    ArchiveFile.BaseStream.Position += 8; //Skip unknowns
                    int ThisPakSize = BigEndian.ReadInt32(ArchiveFile);
                    if (ThisPakSize != BigEndian.ReadInt32(ArchiveFile))
                    {
                        //Dud entry... handle this somehow?
                    }
                    int ThisPakOffset = BigEndian.ReadInt32(ArchiveFile);
                    ArchiveFile.BaseStream.Position += 14;
                    int ThisIndex = BigEndian.ReadInt16(ArchiveFile);
                    ArchiveFile.BaseStream.Position += 12;

                    if (ThisIndex == -1)
                    {
                        continue; //Again, dud entry. Need to look into this!
                    }

                    //Push it into the correct entry
                    ModelEntries[ThisIndex].PakSize = ThisPakSize;
                    ModelEntries[ThisIndex].PakOffset = ThisPakOffset;
                }
                HeaderListEnd = (int)ArchiveFile.BaseStream.Position;

                //Done!
                ArchiveFile.Close();
                return PAKReturnType.SUCCESS;
            }
            catch (IOException) { return PAKReturnType.FAIL_COULD_NOT_ACCESS_FILE; }
            catch (Exception) { return PAKReturnType.FAIL_UNKNOWN; }
        }

        /* Return a list of filenames for files in the ModelPAK archive */
        public override List<string> GetFileNames()
        {
            List<string> FileNameList = new List<string>();
            foreach (CS2 ModelEntry in ModelEntries)
            {
                if (!FileNameList.Contains(ModelEntry.Filename))
                {
                    FileNameList.Add(ModelEntry.Filename);
                }
            }
            return FileNameList;
        }

        /* Get the selected model's submeshes and add up their sizes */
        public override int GetFilesize(string FileName)
        {
            int TotalSize = 0;
            foreach (CS2 ThisModel in ModelEntries)
            {
                if (ThisModel.Filename == FileName.Replace("/", "\\"))
                {
                    TotalSize += ThisModel.PakSize;
                }
            }
            return TotalSize;
        }

        /* Find the model entry object by name */
        protected override int GetFileIndex(string FileName)
        {
            for (int i = 0; i < ModelEntries.Count; i++)
            {
                if (ModelEntries[i].Filename == FileName || ModelEntries[i].Filename == FileName.Replace('/', '\\'))
                {
                    return i;
                }
            }
            throw new Exception("Could not find the requested file in TexturePAK!");
        }

        /* Export an existing file from the ModelPAK archive */
        public override PAKReturnType ExportFile(string PathToExport, string FileName)
        {
            return PAKReturnType.FAIL_FEATURE_IS_COMING_SOON; //Disabling export for main branch

            try
            {
                //Get the selected model's submeshes
                List<CS2> ModelSubmeshes = new List<CS2>();
                foreach (CS2 ThisModel in ModelEntries)
                {
                    if (ThisModel.Filename == FileName.Replace("/", "\\"))
                    {
                        ModelSubmeshes.Add(ThisModel);
                    }
                }

                //Extract each submesh into a CS2 folder by material and submesh name
                Directory.CreateDirectory(PathToExport);
                BinaryReader ArchiveFile = new BinaryReader(File.OpenRead(FilePathPAK));
                foreach (CS2 Submesh in ModelSubmeshes)
                {
                    ArchiveFile.BaseStream.Position = HeaderListEnd + Submesh.PakOffset;

                    string ThisExportPath = PathToExport;
                    if (Submesh.ModelPartName != "")
                    {
                        ThisExportPath = PathToExport + "/" + Submesh.ModelPartName;
                        Directory.CreateDirectory(ThisExportPath);
                    }
                    File.WriteAllBytes(ThisExportPath + "/" + Submesh.MaterialName, ArchiveFile.ReadBytes(Submesh.PakSize));
                }
                ArchiveFile.Close();

                //Done!
                return PAKReturnType.SUCCESS;
            }
            catch
            {
                //Failed
                return PAKReturnType.FAIL_UNKNOWN;
            }
        }
    }
}
