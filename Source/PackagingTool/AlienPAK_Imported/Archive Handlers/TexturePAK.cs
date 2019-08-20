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
     * Texture PAK handler.
     * Allows import/export of CATHODE TEX4 texture files.
     * More work needs to be done to understand the broken formats and to allow importing for files with only V1.
     * 
    */
    class TexturePAK : AnyPAK
    {
        private List<TEX4> TextureEntries = new List<TEX4>();
        private int HeaderListBeginBIN = -1;
        private int HeaderListEndPAK = -1;
        private int NumberOfEntriesPAK = -1;
        private int NumberOfEntriesBIN = -1;
        private int VersionNumber_BIN = 45;
        private int VersionNumber_PAK = 14;

        /* Initialise the TexturePAK class with the intended location (existing or not) */
        public TexturePAK(string PathToPAK)
        {
            FilePathPAK = PathToPAK;

            if (Path.GetFileName(FilePathPAK).Substring(0, 5).ToUpper() == "LEVEL")
            {
                FilePathBIN = FilePathPAK.Substring(0, FilePathPAK.Length - Path.GetFileName(FilePathPAK).Length) + "LEVEL_TEXTURE_HEADERS.ALL.BIN";
            }
            else
            {
                FilePathBIN = FilePathPAK.Substring(0, FilePathPAK.Length - Path.GetFileName(FilePathPAK).Length) + "GLOBAL_TEXTURES_HEADERS.ALL.BIN";
            }
        }

        /* Load the contents of an existing TexturePAK */
        public override PAKReturnType Load()
        {
            if (!File.Exists(FilePathPAK))
            {
                return PAKReturnType.FAIL_TRIED_TO_LOAD_VIRTUAL_ARCHIVE;
            }
            
            try
            {
                /* First, parse the BIN and pull ALL info from it */
                BinaryReader ArchiveFileBin = new BinaryReader(File.OpenRead(FilePathBIN));

                //Read the header info from the BIN
                VersionNumber_BIN = ArchiveFileBin.ReadInt32();
                if (VersionNumber_BIN != 45) { return PAKReturnType.FAIL_ARCHIVE_IS_NOT_EXCPETED_TYPE; } //BIN version number is 45 for textures
                NumberOfEntriesBIN = ArchiveFileBin.ReadInt32();
                HeaderListBeginBIN = ArchiveFileBin.ReadInt32();

                //Read all file names from BIN
                string ThisFileName = "";
                for (int i = 0; i < NumberOfEntriesBIN; i++)
                {
                    ThisFileName = "";
                    for (byte b; (b = ArchiveFileBin.ReadByte()) != 0x00;)
                    {
                        ThisFileName += (char)b;
                    }
                    if (Path.GetExtension(ThisFileName).ToUpper() != ".DDS")
                    {
                        ThisFileName += ".dds";
                    }
                    //Create texture entry and add filename
                    TEX4 TextureEntry = new TEX4();
                    TextureEntry.FileName = ThisFileName;
                    TextureEntries.Add(TextureEntry);
                }

                //Read the texture headers from the BIN
                ArchiveFileBin.BaseStream.Position = HeaderListBeginBIN + 12;
                for (int i = 0; i < NumberOfEntriesBIN; i++)
                {
                    TextureEntries[i].HeaderPos = (int)ArchiveFileBin.BaseStream.Position;
                    for (int x = 0; x < 4; x++) { TextureEntries[i].Magic += ArchiveFileBin.ReadChar(); }
                    TextureEntries[i].Format = (TextureFormat)ArchiveFileBin.ReadInt32();
                    TextureEntries[i].Length_V2 = ArchiveFileBin.ReadInt32();
                    TextureEntries[i].Length_V1 = ArchiveFileBin.ReadInt32();
                    TextureEntries[i].Texture_V1.Width = ArchiveFileBin.ReadInt16();
                    TextureEntries[i].Texture_V1.Height = ArchiveFileBin.ReadInt16();
                    TextureEntries[i].Unk_V1 = ArchiveFileBin.ReadInt16();
                    TextureEntries[i].Texture_V2.Width = ArchiveFileBin.ReadInt16();
                    TextureEntries[i].Texture_V2.Height = ArchiveFileBin.ReadInt16();
                    TextureEntries[i].Unk_V2 = ArchiveFileBin.ReadInt16();
                    TextureEntries[i].UnknownHeaderBytes = ArchiveFileBin.ReadBytes(20);
                }

                /* Second, parse the PAK and pull ONLY header info from it - we'll pull textures when requested (to save memory) */
                ArchiveFileBin.Close();
                BinaryReader ArchiveFile = new BinaryReader(File.OpenRead(FilePathPAK));

                //Read the header info from the PAK
                BigEndianUtils BigEndian = new BigEndianUtils();
                ArchiveFile.BaseStream.Position += 4; //Skip nulls
                VersionNumber_PAK = BigEndian.ReadInt32(ArchiveFile);
                if (BigEndian.ReadInt32(ArchiveFile) != VersionNumber_BIN) { throw new Exception("Archive version mismatch!"); }
                NumberOfEntriesPAK = BigEndian.ReadInt32(ArchiveFile);
                if (BigEndian.ReadInt32(ArchiveFile) != NumberOfEntriesPAK) { throw new Exception("PAK entry count mismatch!"); }
                ArchiveFile.BaseStream.Position += 12; //Skip unknowns (1,1,1)

                //Read the texture headers from the PAK
                int OffsetTracker = (NumberOfEntriesPAK * 48) + 32;
                for (int i = 0; i < NumberOfEntriesPAK; i++)
                {
                    //Header indexes are out of order, so optimise replacements by saving position
                    int HeaderPosition = (int)ArchiveFile.BaseStream.Position;

                    //Pull the entry info
                    byte[] UnknownHeaderLead = ArchiveFile.ReadBytes(8);
                    int PartLength = BigEndian.ReadInt32(ArchiveFile);
                    if (PartLength != BigEndian.ReadInt32(ArchiveFile)) { continue; }
                    byte[] UnknownHeaderTrail_1 = ArchiveFile.ReadBytes(18);

                    //Find the entry
                    TEX4 TextureEntry = TextureEntries[BigEndian.ReadInt16(ArchiveFile)];
                    TEX4_Part TexturePart = (!TextureEntry.Texture_V1.Saved) ? TextureEntry.Texture_V1 : TextureEntry.Texture_V2;

                    //Write out the info
                    TexturePart.HeaderPos = HeaderPosition;
                    TexturePart.StartPos = OffsetTracker;
                    TexturePart.UnknownHeaderLead = UnknownHeaderLead;
                    TexturePart.Length = PartLength;
                    TexturePart.Saved = true;
                    TexturePart.UnknownHeaderTrail_1 = UnknownHeaderTrail_1;
                    TexturePart.UnknownHeaderTrail_2 = ArchiveFile.ReadBytes(12);

                    //Keep file offset updated
                    OffsetTracker += TexturePart.Length;
                }
                HeaderListEndPAK = (int)ArchiveFile.BaseStream.Position;

                //Close PAK
                ArchiveFile.Close();
                return PAKReturnType.SUCCESS;
            }
            catch (IOException) { return PAKReturnType.FAIL_COULD_NOT_ACCESS_FILE; }
            catch (Exception) { return PAKReturnType.FAIL_UNKNOWN; }
        }

        /* Return a list of filenames for files in the TexturePAK archive */
        public override List<string> GetFileNames()
        {
            List<string> FileNameList = new List<string>();
            foreach (TEX4 ArchiveFile in TextureEntries)
            {
                FileNameList.Add(ArchiveFile.FileName);
            }
            return FileNameList;
        }

        /* Get the file size of an archive entry */
        public override int GetFilesize(string FileName)
        {
            int FileIndex = GetFileIndex(FileName);
            if (TextureEntries[FileIndex].Texture_V2.Saved)
            {
                return TextureEntries[FileIndex].Texture_V2.Length + 148;
            }
            //Fallback to V1 if this texture has no V2
            else if (TextureEntries[FileIndex].Texture_V1.Saved)
            {
                return TextureEntries[FileIndex].Texture_V1.Length + 148;
            }
            throw new Exception("Texture has no size! Fatal logic error.");
        }

        /* Find a file entry object by name */
        protected override int GetFileIndex(string FileName)
        {
            for (int i = 0; i < TextureEntries.Count; i++)
            {
                if (TextureEntries[i].FileName == FileName || TextureEntries[i].FileName == FileName.Replace('/', '\\'))
                {
                    return i;
                }
            }
            throw new Exception("Could not find the requested file in TexturePAK!");
        }

        /* Replace an existing file in the TexturePAK archive */
        public override PAKReturnType ReplaceFile(string PathToNewFile, string FileName)
        {
            try
            {
                //Get the texture entry & parse new DDS
                int EntryIndex = GetFileIndex(FileName);
                TEX4 TextureEntry = TextureEntries[EntryIndex];
                DDSReader NewTexture = new DDSReader(PathToNewFile);

                //Currently we only apply the new texture to the "biggest", some have lower mips that we don't edit (TODO)
                TEX4_Part BiggestPart = TextureEntry.Texture_V2;
                if (BiggestPart.HeaderPos == -1 || !BiggestPart.Saved)
                {
                    BiggestPart = TextureEntry.Texture_V1;
                }
                if (BiggestPart.HeaderPos == -1 || !BiggestPart.Saved)
                {
                    return PAKReturnType.FAIL_REQUEST_IS_UNSUPPORTED; //Shouldn't reach this.
                }

                //CATHODE seems to ignore texture header information regarding size, so as default, resize any imported textures to the original size.
                //An option is provided in the toolkit to write size information to the header (done above) however, so don't resize if that's the case.
                //More work needs to be done to figure out why CATHODE doesn't honour the header's size value.
                int OriginalLength = BiggestPart.Length;
                Array.Resize(ref NewTexture.DataBlock, OriginalLength);

                //Update our internal knowledge of the textures
                BiggestPart.Length = (int)NewTexture.DataBlock.Length;
                BiggestPart.Width = (Int16)NewTexture.Width;
                BiggestPart.Height = (Int16)NewTexture.Height;
                TextureEntry.Format = NewTexture.Format;
                //TODO: Update smallest here too if it exists!
                //Will need to be written into the PAK at "Pull PAK sections before/after V2" too - headers are handled already.

                //Load the BIN and write out updated BIN texture header
                BinaryWriter ArchiveFileBinWriter = new BinaryWriter(File.OpenWrite(FilePathBIN));
                ExtraBinaryUtils BinaryUtils = new ExtraBinaryUtils();
                ArchiveFileBinWriter.BaseStream.Position = TextureEntry.HeaderPos;
                BinaryUtils.WriteString(TextureEntry.Magic, ArchiveFileBinWriter);
                ArchiveFileBinWriter.Write(BitConverter.GetBytes((int)TextureEntry.Format));
                ArchiveFileBinWriter.Write((TextureEntry.Texture_V2.Length == -1) ? 0 : TextureEntry.Texture_V2.Length);
                ArchiveFileBinWriter.Write(TextureEntry.Texture_V1.Length);
                ArchiveFileBinWriter.Write(TextureEntry.Texture_V1.Width);
                ArchiveFileBinWriter.Write(TextureEntry.Texture_V1.Height);
                ArchiveFileBinWriter.Write(TextureEntry.Unk_V1);
                ArchiveFileBinWriter.Write(TextureEntry.Texture_V2.Width);
                ArchiveFileBinWriter.Write(TextureEntry.Texture_V2.Height);
                ArchiveFileBinWriter.Write(TextureEntry.Unk_V2);
                ArchiveFileBinWriter.Write(TextureEntry.UnknownHeaderBytes);
                ArchiveFileBinWriter.Close();

                //Update headers for V1+2 in PAK if they exist
                BinaryWriter ArchiveFileWriter = new BinaryWriter(File.OpenWrite(FilePathPAK));
                BigEndianUtils BigEndian = new BigEndianUtils();
                if (TextureEntry.Texture_V1.HeaderPos != -1)
                {
                    ArchiveFileWriter.BaseStream.Position = TextureEntry.Texture_V1.HeaderPos;
                    ArchiveFileWriter.Write(TextureEntry.Texture_V1.UnknownHeaderLead);
                    ArchiveFileWriter.Write(BigEndian.FlipEndian(BitConverter.GetBytes(TextureEntry.Texture_V1.Length)));
                    ArchiveFileWriter.Write(BigEndian.FlipEndian(BitConverter.GetBytes(TextureEntry.Texture_V1.Length)));
                    ArchiveFileWriter.Write(TextureEntry.Texture_V1.UnknownHeaderTrail_1);
                    ArchiveFileWriter.Write(BigEndian.FlipEndian(BitConverter.GetBytes((Int16)EntryIndex)));
                    ArchiveFileWriter.Write(TextureEntry.Texture_V1.UnknownHeaderTrail_2);
                }
                if (TextureEntry.Texture_V2.HeaderPos != -1)
                {
                    ArchiveFileWriter.BaseStream.Position = TextureEntry.Texture_V2.HeaderPos;
                    ArchiveFileWriter.Write(TextureEntry.Texture_V2.UnknownHeaderLead);
                    ArchiveFileWriter.Write(BigEndian.FlipEndian(BitConverter.GetBytes(TextureEntry.Texture_V2.Length)));
                    ArchiveFileWriter.Write(BigEndian.FlipEndian(BitConverter.GetBytes(TextureEntry.Texture_V2.Length)));
                    ArchiveFileWriter.Write(TextureEntry.Texture_V2.UnknownHeaderTrail_1);
                    ArchiveFileWriter.Write(BigEndian.FlipEndian(BitConverter.GetBytes((Int16)EntryIndex)));
                    ArchiveFileWriter.Write(TextureEntry.Texture_V2.UnknownHeaderTrail_2);
                }
                ArchiveFileWriter.Close();

                //Pull PAK sections before/after V2
                BinaryReader ArchiveFile = new BinaryReader(File.OpenRead(FilePathPAK));
                byte[] PAK_Pt1 = ArchiveFile.ReadBytes(BiggestPart.StartPos);
                ArchiveFile.BaseStream.Position += OriginalLength;
                byte[] PAK_Pt2 = ArchiveFile.ReadBytes((int)ArchiveFile.BaseStream.Length - (int)ArchiveFile.BaseStream.Position);
                ArchiveFile.Close();

                //Write the PAK back out with new content
                ArchiveFileWriter = new BinaryWriter(File.OpenWrite(FilePathPAK));
                ArchiveFileWriter.BaseStream.SetLength(0);
                ArchiveFileWriter.Write(PAK_Pt1);
                ArchiveFileWriter.Write(NewTexture.DataBlock);
                ArchiveFileWriter.Write(PAK_Pt2);
                ArchiveFileWriter.Close();

                return PAKReturnType.SUCCESS;
            }
            catch (IOException) { return PAKReturnType.FAIL_COULD_NOT_ACCESS_FILE; }
            catch (Exception) { return PAKReturnType.FAIL_UNKNOWN; }
        }

        /* Export an existing file from the TexturePAK archive */
        public override PAKReturnType ExportFile(string PathToExport, string FileName)
        {
            try
            {
                //Get the texture index
                int FileIndex = GetFileIndex(FileName);

                //Get the biggest texture part stored
                TEX4_Part TexturePart;
                if (TextureEntries[FileIndex].Texture_V2.Saved)
                {
                    TexturePart = TextureEntries[FileIndex].Texture_V2;
                }
                else if (TextureEntries[FileIndex].Texture_V1.Saved)
                {
                    TexturePart = TextureEntries[FileIndex].Texture_V1;
                }
                else
                {
                    return PAKReturnType.FAIL_REQUEST_IS_UNSUPPORTED;
                }

                //Pull the texture part content from the PAK
                BinaryReader ArchiveFile = new BinaryReader(File.OpenRead(FilePathPAK));
                ArchiveFile.BaseStream.Position = TexturePart.StartPos;
                byte[] TexturePartContent = ArchiveFile.ReadBytes(TexturePart.Length);
                ArchiveFile.Close();

                //Generate a DDS header based on the tex4's information
                DDSWriter TextureOutput;
                bool FailsafeSave = false;
                switch (TextureEntries[FileIndex].Format)
                {
                    case TextureFormat.DXGI_FORMAT_BC5_UNORM:
                        TextureOutput = new DDSWriter(TexturePartContent, TexturePart.Width, TexturePart.Height, 32, 0, TextureType.ATI2N);
                        break;
                    case TextureFormat.DXGI_FORMAT_BC1_UNORM:
                        TextureOutput = new DDSWriter(TexturePartContent, TexturePart.Width, TexturePart.Height, 32, 0, TextureType.Dxt1);
                        break;
                    case TextureFormat.DXGI_FORMAT_BC3_UNORM:
                        TextureOutput = new DDSWriter(TexturePartContent, TexturePart.Width, TexturePart.Height, 32, 0, TextureType.Dxt5);
                        break;
                    case TextureFormat.DXGI_FORMAT_B8G8R8A8_UNORM:
                        TextureOutput = new DDSWriter(TexturePartContent, TexturePart.Width, TexturePart.Height, 32, 0, TextureType.UNCOMPRESSED_GENERAL);
                        break;
                    case TextureFormat.DXGI_FORMAT_BC7_UNORM:
                    default:
                        TextureOutput = new DDSWriter(TexturePartContent, TexturePart.Width, TexturePart.Height);
                        FailsafeSave = true;
                        break;
                }

                //Try and save out the part
                if (FailsafeSave)
                {
                    TextureOutput.SaveCrude(PathToExport);
                    return PAKReturnType.SUCCESS_WITH_WARNINGS;
                }
                TextureOutput.Save(PathToExport);
                return PAKReturnType.SUCCESS;
            }
            catch (IOException) { return PAKReturnType.FAIL_COULD_NOT_ACCESS_FILE; }
            catch (Exception) { return PAKReturnType.FAIL_UNKNOWN; }
        }
    }
}
