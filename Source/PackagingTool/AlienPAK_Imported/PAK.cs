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
     * Our PAK handler.
     * Created by Matt Filer: http://www.mattfiler.co.uk
     * 
     * Intended to support PAK2/TexturePAK/ModelPAK.
     * Potentially will also add the ability to make your own PAK2 archives.
     * Currently a WORK IN PROGRESS.
     * 
    */
    class PAK
    {
        /* --- COMMON PAK --- */
        private string ArchivePath = "";
        private string ArchivePathBin = "";
        private BinaryReader ArchiveFile = null;
        private BinaryReader ArchiveFileBin = null;
        private List<string> FileList = new List<string>();
        private int NumberOfEntries = -1;
        private enum PAKType { PAK2, PAK_TEXTURES, PAK_MODELS, UNRECOGNISED };
        private PAKType Format = PAKType.UNRECOGNISED;
        public enum PAKReturnType { FAILED_UNKNOWN, FAILED_UNSUPPORTED, SUCCESS, FAILED_LOGIC_ERROR, FAILED_FILE_IN_USE }
        public string LatestError = "";

        /* Open a PAK archive */
        public void Open(string FilePath)
        {
            //Open new PAK
            if (ArchiveFile != null) { ArchiveFile.Close(); }
            if (ArchiveFileBin != null) { ArchiveFileBin.Close(); }
            ArchiveFile = new BinaryReader(File.OpenRead(FilePath));

            //Update our info
            ArchivePath = FilePath;
            ArchivePathBin = "";
            string FileName = Path.GetFileName(FilePath);
            switch (FileName)
            {
                case "GLOBAL_TEXTURES.ALL.PAK":
                case "LEVEL_TEXTURES.ALL.PAK":
                    Format = PAKType.PAK_TEXTURES;
                    break;
                case "GLOBAL_MODELS.PAK":
                case "LEVEL_MODELS.PAK":
                    Format = PAKType.PAK_MODELS;
                    break;
                default:
                    try
                    {
                        string PAKMagic = "";
                        for (int i = 0; i < 4; i++)
                        {
                            PAKMagic += ArchiveFile.ReadChar();
                        }
                        ArchiveFile.BaseStream.Position = 0;
                        if (PAKMagic == "PAK2")
                        {
                            Format = PAKType.PAK2;
                            break;
                        }
                    }
                    catch { }
                    Format = PAKType.UNRECOGNISED;
                    break;
            }

            //Certain formats have associated BIN files
            switch (Format)
            {
                case PAKType.PAK_TEXTURES:
                    if (FileName.Substring(0, 5).ToUpper() == "LEVEL")
                    {
                        ArchivePathBin = FilePath.Substring(0, FilePath.Length - FileName.Length) + "LEVEL_TEXTURE_HEADERS.ALL.BIN";
                        ArchiveFileBin = new BinaryReader(File.OpenRead(ArchivePathBin));
                    }
                    else
                    {
                        ArchivePathBin = FilePath.Substring(0, FilePath.Length - FileName.Length) + "GLOBAL_TEXTURES_HEADERS.ALL.BIN";
                        ArchiveFileBin = new BinaryReader(File.OpenRead(ArchivePathBin));
                    }
                    break;
                case PAKType.PAK_MODELS:
                    ArchivePathBin = FilePath.Substring(0, FilePath.Length - FileName.Length) + "MODELS_" + FileName.Substring(0, FileName.Length - 11) + ".BIN";
                    ArchiveFileBin = new BinaryReader(File.OpenRead(ArchivePathBin));
                    break;
            }
        }

        /* Parse a PAK archive */
        public List<string> Parse()
        {
            if (ArchiveFile == null) { return null; }

            FileList.Clear();
            FileOffsets.Clear();
            FilePadding.Clear();

            switch (Format)
            {
                case PAKType.PAK2:
                    return ParsePAK2();
                case PAKType.PAK_TEXTURES:
                    return ParseTexturePAK();
                case PAKType.PAK_MODELS:
                //return ParseModelPAK(); <= Even bigger WIP than textures!
                default:
                    return null;
            }
        }

        /* Get the size of a file within the PAK archive */
        public int GetFileSize(string FileName)
        {
            if (ArchiveFile == null) { return -1; }

            switch (Format)
            {
                case PAKType.PAK2:
                    return FileSizePAK2(FileName);
                case PAKType.PAK_TEXTURES:
                    return FileSizeTexturePAK(FileName);
                case PAKType.PAK_MODELS:
                    return FileSizeModelPAK(FileName);
                default:
                    return -1;
            }
        }

        /* Export from a PAK archive */
        public PAKReturnType ExportFile(string FileName, string ExportPath)
        {
            if (ArchiveFile == null) { return PAKReturnType.FAILED_LOGIC_ERROR; }

            switch (Format)
            {
                case PAKType.PAK2:
                    return ExportFilePAK2(FileName, ExportPath);
                case PAKType.PAK_TEXTURES:
                    return ExportFileTexturePAK(FileName, ExportPath);
                case PAKType.PAK_MODELS:
                    return ExportFileModelPAK(FileName, ExportPath);
                default:
                    return PAKReturnType.FAILED_UNSUPPORTED;
            }
        }

        /* Import to a PAK archive */
        public PAKReturnType ImportFile(string FileName, string ImportPath)
        {
            if (ArchiveFile == null) { return PAKReturnType.FAILED_LOGIC_ERROR; }

            switch (Format)
            {
                case PAKType.PAK2:
                    return ImportFilePAK2(FileName, ImportPath);
                case PAKType.PAK_TEXTURES:
                    return ImportFileTexturePAK(FileName, ImportPath);
                case PAKType.PAK_MODELS:
                    return ImportFileModelPAK(FileName, ImportPath);
                default:
                    return PAKReturnType.FAILED_UNSUPPORTED;
            }
        }

        /* Get the PAK index of the file by name */
        private int GetFileIndex(string FileName)
        {
            //Get index
            for (int i = 0; i < FileList.Count; i++)
            {
                if (FileList[i] == FileName)
                {
                    return i;
                }
            }

            //Couldn't find - sometimes CA use "\" instead of "/"... try that
            FileName = FileName.Replace('/', '\\');
            for (int i = 0; i < FileList.Count; i++)
            {
                if (FileList[i] == FileName)
                {
                    return i;
                }
            }

            //Failed to find
            return -1;
        }


        /* --- PAK2 --- */
        private List<int> FileOffsets = new List<int>();
        private List<int> FilePadding = new List<int>();
        private int OffsetListBegin = -1;
        private int DataSize = -1;

        /* Parse a PAK2 archive */
        private List<string> ParsePAK2()
        {
            //Read the header info
            ArchiveFile.BaseStream.Position += 4; //Skip magic
            OffsetListBegin = ArchiveFile.ReadInt32() + 16;
            NumberOfEntries = ArchiveFile.ReadInt32();
            DataSize = ArchiveFile.ReadInt32();

            //Read all file names
            for (int i = 0; i < NumberOfEntries; i++)
            {
                string ThisFileName = "";
                for (byte b; (b = ArchiveFile.ReadByte()) != 0x00;)
                {
                    ThisFileName += (char)b;
                }
                FileList.Add(ThisFileName);
            }

            //Read all file offsets
            ArchiveFile.BaseStream.Position = OffsetListBegin;
            FileOffsets.Add(OffsetListBegin + (NumberOfEntries * DataSize));
            List<string> debug = new List<string>();
            for (int i = 0; i < NumberOfEntries; i++)
            {
                FileOffsets.Add(ArchiveFile.ReadInt32());
                debug.Add(FileOffsets.ElementAt(i).ToString());
            }

            //Hacky way to store byte alignment values
            for (int i = 0; i < NumberOfEntries; i++)
            {
                ArchiveFile.BaseStream.Position = FileOffsets[i];
                FilePadding.Add(0);
                for (int x = 0; x < DataSize + 1; x++)
                {
                    if (ArchiveFile.ReadByte() == 0x00)
                    {
                        FilePadding[i] += 1;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return FileList;
        }

        /* Get a file's size from the PAK2 archive */
        private int FileSizePAK2(string FileName)
        {
            int FileIndex = GetFileIndex(FileName);
            ArchiveFile.BaseStream.Position = FileOffsets[FileIndex] + FilePadding[FileIndex];
            return FileOffsets.ElementAt(FileIndex + 1) - (int)ArchiveFile.BaseStream.Position;
        }

        /* Export a file from the PAK2 archive */
        private PAKReturnType ExportFilePAK2(string FileName, string ExportPath)
        {
            try
            {
                //Update reader position and work out file size
                int FileLength = FileSizePAK2(FileName);

                //Grab the file's contents (this can probably be optimised!)
                List<byte> FileExport = new List<byte>();
                for (int i = 0; i < FileLength; i++)
                {
                    FileExport.Add(ArchiveFile.ReadByte());
                }

                //Write the file's contents out
                File.WriteAllBytes(ExportPath, FileExport.ToArray());
                return PAKReturnType.SUCCESS;
            }
            catch (Exception e)
            {
                LatestError = e.ToString();
                return PAKReturnType.FAILED_UNKNOWN;
            }
        }

        /* Import a file to the PAK2 archive */
        private PAKReturnType ImportFilePAK2(string FileName, string ImportPath)
        {
            try
            {
                //Open PAK for writing, and read contents of import file
                BinaryReader ImportFile = new BinaryReader(File.OpenRead(ImportPath));

                //Old/new file lengths
                int FileIndex = GetFileIndex(FileName);
                int OldLength = FileOffsets.ElementAt(FileIndex + 1) - FileOffsets.ElementAt(FileIndex);
                int NewLength = (int)ImportFile.BaseStream.Length + FilePadding.ElementAt(FileIndex);

                //Old/new "padding" (next file in sequence's byte alignment)
                int OldNextPadding = (FileIndex != FilePadding.Count - 1) ? FilePadding.ElementAt(FileIndex + 1) : 0;
                int NewNextPadding = 0; //This will be set later

                //Grab the first section of the archive
                ArchiveFile.BaseStream.Position = 0;
                byte[] ArchivePt1 = new byte[FileOffsets.ElementAt(FileIndex)];
                for (int i = 0; i < ArchivePt1.Length; i++)
                {
                    ArchivePt1[i] = ArchiveFile.ReadByte();
                }

                //Update file offset information
                for (int i = 0; i < (NumberOfEntries - FileIndex); i++)
                {
                    //Read original offset
                    byte[] OffsetRaw = new byte[DataSize];
                    for (int x = 0; x < OffsetRaw.Length; x++)
                    {
                        OffsetRaw[x] = ArchivePt1[OffsetListBegin + ((FileIndex + i) * DataSize) + x]; //+1?
                    }

                    //Update original offset
                    int Offset = BitConverter.ToInt32(OffsetRaw, 0);
                    Offset = Offset - OldLength + NewLength;
                    if (i == 0 && FileIndex != NumberOfEntries - 1)
                    {
                        //Correct the byte alignment for first trailing file (if we have one)
                        while ((Offset + NewNextPadding) % 4 != 0)
                        {
                            NewNextPadding += 1;
                        }
                        FilePadding[FileIndex + 1] = NewNextPadding;
                    }
                    else
                    {
                        //Flow new padding over to each following file
                        Offset += (NewNextPadding - OldNextPadding);
                    }
                    OffsetRaw = BitConverter.GetBytes(Offset);

                    //Write back new offset
                    for (int x = 0; x < OffsetRaw.Length; x++)
                    {
                        ArchivePt1[OffsetListBegin + ((FileIndex + i) * DataSize) + x] = OffsetRaw[x]; //+1?
                    }
                }

                //Grab the second half of the archive after the file, and correct the byte offset
                ArchiveFile.BaseStream.Position = FileOffsets.ElementAt(FileIndex + 1);
                byte[] ArchivePt2 = new byte[FileOffsets.ElementAt(FileOffsets.Count - 1) - FileOffsets.ElementAt(FileIndex + 1) + (NewNextPadding - OldNextPadding)];
                ArchiveFile.BaseStream.Position += OldNextPadding;
                for (int i = 0; i < NewNextPadding; i++)
                {
                    ArchivePt2[i] = 0x00;
                }
                for (int i = NewNextPadding; i < ArchivePt2.Length; i++)
                {
                    ArchivePt2[i] = ArchiveFile.ReadByte();
                }

                //Compose new archive from the two old parts and the new file stuck in the middle
                byte[] NewArchive = new byte[FileOffsets.ElementAt(FileOffsets.Count - 1) - OldLength + NewLength + (NewNextPadding - OldNextPadding)];
                int ArchiveIndex = 0;
                for (int i = 0; i < ArchivePt1.Length; i++)
                {
                    NewArchive[ArchiveIndex] = ArchivePt1[i];
                    ArchiveIndex++;
                }
                for (int i = 0; i < FilePadding.ElementAt(FileIndex); i++)
                {
                    NewArchive[ArchiveIndex] = 0x00;
                    ArchiveIndex++;
                }
                for (int i = 0; i < (int)ImportFile.BaseStream.Length; i++)
                {
                    NewArchive[ArchiveIndex] = ImportFile.ReadByte();
                    ArchiveIndex++;
                }
                for (int i = 0; i < ArchivePt2.Length; i++)
                {
                    NewArchive[ArchiveIndex] = ArchivePt2[i];
                    ArchiveIndex++;
                }

                ArchiveFile.Close();
                try
                {
                    //Write out the new archive
                    BinaryWriter ArchiveFileWrite = new BinaryWriter(File.OpenWrite(ArchivePath));
                    ArchiveFileWrite.BaseStream.SetLength(0);
                    ArchiveFileWrite.Write(NewArchive);
                    ArchiveFileWrite.Close();
                }
                catch
                {
                    //File is probably in-use by the game, re-open for reading and exit as fail
                    Open(ArchivePath);
                    return PAKReturnType.FAILED_FILE_IN_USE;
                }

                //Reload the archive for us
                Open(ArchivePath);
                ParsePAK2();

                return PAKReturnType.SUCCESS;
            }
            catch (Exception e)
            {
                LatestError = e.ToString();
                return PAKReturnType.FAILED_UNKNOWN;
            }
        }


        /* --- TEXTURE PAK --- */
        int HeaderListBeginBIN = -1;
        int HeaderListEndPAK = -1;
        int NumberOfEntriesPAK = -1;
        int NumberOfEntriesBIN = -1;
        List<TEX4> TextureEntries = new List<TEX4>();

        /* Parse the file listing for a texture PAK */
        private List<string> ParseTexturePAK()
        {
            //Read the header info from the BIN
            ArchiveFileBin.BaseStream.Position += 4; //Skip unused value (version?)
            NumberOfEntriesBIN = ArchiveFileBin.ReadInt32();
            HeaderListBeginBIN = ArchiveFileBin.ReadInt32();

            //Read all file names from BIN
            for (int i = 0; i < NumberOfEntriesBIN; i++)
            {
                string ThisFileName = "";
                for (byte b; (b = ArchiveFileBin.ReadByte()) != 0x00;)
                {
                    ThisFileName += (char)b;
                }
                if (Path.GetExtension(ThisFileName).ToUpper() != ".DDS")
                {
                    ThisFileName += ".dds";
                }
                FileList.Add(ThisFileName);
            }

            //Read the texture headers from the BIN
            ArchiveFileBin.BaseStream.Position = HeaderListBeginBIN + 12;
            for (int i = 0; i < NumberOfEntriesBIN; i++)
            {
                int HeaderPosition = (int)ArchiveFileBin.BaseStream.Position;
                TEX4 TextureEntry = new TEX4();

                ArchiveFileBin.BaseStream.Position += 4; //Skip magic
                TextureEntry.Format = (TextureFormat)ArchiveFileBin.ReadInt32();
                ArchiveFileBin.BaseStream.Position += 4; //Skip V2 length
                ArchiveFileBin.BaseStream.Position += 4; //Skip V1 length
                TextureEntry.Texture_V1.Width = ArchiveFileBin.ReadInt16();
                TextureEntry.Texture_V1.Height = ArchiveFileBin.ReadInt16();
                ArchiveFileBin.BaseStream.Position += 2; //Skip unknown
                TextureEntry.Texture_V2.Width = ArchiveFileBin.ReadInt16();
                TextureEntry.Texture_V2.Height = ArchiveFileBin.ReadInt16();
                ArchiveFileBin.BaseStream.Position += 22; //Skip unknowns
                TextureEntry.FileName = FileList[i];
                TextureEntry.HeaderPos = HeaderPosition;

                TextureEntries.Add(TextureEntry);
            }

            //Read the header info from the PAK
            BigEndianUtils BigEndian = new BigEndianUtils();
            ArchiveFile.BaseStream.Position += 12; //Skip unknowns
            NumberOfEntriesPAK = BigEndian.ReadInt32(ArchiveFile);
            ArchiveFile.BaseStream.Position += 16; //Skip unknowns

            //Read the texture headers from the PAK
            int OffsetTracker = (NumberOfEntriesPAK * 48) + 32;
            for (int i = 0; i < NumberOfEntriesPAK; i++)
            {
                //Header indexes are out of order, so optimise replacements by saving position
                int HeaderPosition = (int)ArchiveFile.BaseStream.Position;

                //Pull the size info
                int EntrySize = 0;
                ArchiveFile.BaseStream.Position += 8; //Skip unknowns
                EntrySize = BigEndian.ReadInt32(ArchiveFile);
                if (EntrySize != BigEndian.ReadInt32(ArchiveFile)) { continue; }
                ArchiveFile.BaseStream.Position += 18; //Skip unknowns

                //Pull the index info and use that to find the texture entry
                TEX4 TextureEntry = TextureEntries[BigEndian.ReadInt16(ArchiveFile)];

                //Assign size info to the entry with the calculated offset
                if (!TextureEntry.Texture_V1.Saved)
                {
                    TextureEntry.Texture_V1.StartPos = OffsetTracker;
                    TextureEntry.Texture_V1.Length = EntrySize;
                    TextureEntry.Texture_V1.Saved = true;
                    TextureEntry.Texture_V1.HeaderPos = HeaderPosition;
                }
                else
                {
                    TextureEntry.Texture_V2.StartPos = OffsetTracker;
                    TextureEntry.Texture_V2.Length = EntrySize;
                    TextureEntry.Texture_V2.Saved = true;
                    TextureEntry.Texture_V2.HeaderPos = HeaderPosition;
                }
                OffsetTracker += EntrySize;

                //Skip the rest of the header
                ArchiveFile.BaseStream.Position += 12; //Skip unknowns
            }
            HeaderListEndPAK = (int)ArchiveFile.BaseStream.Position;

            return FileList;
        }

        /* Get a file's size from the texture PAK */
        private int FileSizeTexturePAK(string FileName)
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
            return -1; //Should never get here
        }

        /* Export a file from the texture PAK */
        private PAKReturnType ExportFileTexturePAK(string FileName, string ExportPath)
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
                    return PAKReturnType.FAILED_UNSUPPORTED;
                }

                //Pull the texture part content from the archive
                ArchiveFile.BaseStream.Position = TexturePart.StartPos;
                byte[] TexturePartContent = ArchiveFile.ReadBytes(TexturePart.Length);

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
                try
                {
                    if (FailsafeSave)
                    {
                        TextureOutput.SaveCrude(ExportPath);
                        return PAKReturnType.SUCCESS;
                    }
                    TextureOutput.Save(ExportPath);
                    return PAKReturnType.SUCCESS;
                }
                catch
                {
                    return PAKReturnType.FAILED_FILE_IN_USE;
                }
            }
            catch (Exception e)
            {
                LatestError = e.ToString();
                return PAKReturnType.FAILED_UNKNOWN;
            }
        }

        /* Import a file to the texture PAK */
        private PAKReturnType ImportFileTexturePAK(string FileName, string ImportPath)
        {
            try
            {
                //Get the texture entry & parse new DDS
                TEX4 TextureEntry = TextureEntries[GetFileIndex(FileName)];
                DDSReader NewTexture = new DDSReader(ImportPath);

                //Currently we only support textures that have V1 and V2
                if (TextureEntry.Texture_V2.HeaderPos == -1)
                {
                    return PAKReturnType.FAILED_UNSUPPORTED;
                }

                //Load the BIN to byte array
                ArchiveFileBin.BaseStream.Position = 0;
                byte[] BinFile = new byte[ArchiveFileBin.BaseStream.Length];
                for (int i = 0; i < BinFile.Length; i++)
                {
                    BinFile[i] = ArchiveFileBin.ReadByte();
                }

                //Update format in BIN
                int BinOffset = TextureEntry.HeaderPos + 4;
                byte[] NewFormat = BitConverter.GetBytes((int)NewTexture.Format);
                for (int i = 0; i < 4; i++)
                {
                    BinFile[BinOffset] = NewFormat[i];
                    BinOffset++;
                }

                //Change the new filesize dependant on options (SEE LINE 717)
                int FileSize = TextureEntry.Texture_V2.Length;
                //FileSize = (int)NewTexture.DataBlock.Length; : move to this eventually

                //Update filesize in BIN
                byte[] NewEntrySize = BitConverter.GetBytes(FileSize);
                for (int i = 0; i < 4; i++)
                {
                    BinFile[BinOffset] = NewEntrySize[i];
                    BinOffset++;
                }
                BinOffset += 4; //Skip V1

                //Update dimensions in BIN (imported textures apply to V2 only)
                BinOffset += 6;
                byte[] NewWidth = BitConverter.GetBytes((Int16)NewTexture.Width);
                byte[] NewHeight = BitConverter.GetBytes((Int16)NewTexture.Height);
                for (int i = 0; i < 2; i++)
                {
                    BinFile[BinOffset] = NewWidth[i];
                    BinOffset++;
                }
                for (int i = 0; i < 2; i++)
                {
                    BinFile[BinOffset] = NewHeight[i];
                    BinOffset++;
                }

                //Take all headers up to the V2 header in PAK
                ArchiveFile.BaseStream.Position = 0;
                byte[] ArchivePt1 = new byte[TextureEntry.Texture_V2.HeaderPos];
                for (int i = 0; i < ArchivePt1.Length; i++)
                {
                    ArchivePt1[i] = ArchiveFile.ReadByte();
                }
                
                //Update V2 header for new image filesize in PAK
                byte[] ArchivePt2 = new byte[48];
                for (int i = 0; i < ArchivePt2.Length; i++)
                {
                    ArchivePt2[i] = ArchiveFile.ReadByte();
                }
                Array.Reverse(NewEntrySize); //This file is big endian
                for (int i = 0; i < 4; i++)
                {
                    ArchivePt2[8 + i] = NewEntrySize[i];
                }
                for (int i = 0; i < 4; i++)
                {
                    ArchivePt2[12 + i] = NewEntrySize[i];
                }

                //Read to end of headers in PAK
                byte[] ArchivePt3 = new byte[HeaderListEndPAK - ArchivePt1.Length - 48];
                for (int i = 0; i < ArchivePt3.Length; i++)
                {
                    ArchivePt3[i] = ArchiveFile.ReadByte();
                }

                //Take all files up to V2 in PAK
                byte[] ArchivePt4 = new byte[TextureEntry.Texture_V2.StartPos - HeaderListEndPAK];
                for (int i = 0; i < ArchivePt4.Length; i++)
                {
                    ArchivePt4[i] = ArchiveFile.ReadByte();
                }
                ArchiveFile.BaseStream.Position += FileSize;

                //Take all files past V2 in PAK
                byte[] ArchivePt5 = new byte[ArchiveFile.BaseStream.Length - ArchiveFile.BaseStream.Position];
                for (int i = 0; i < ArchivePt5.Length; i++)
                {
                    ArchivePt5[i] = ArchiveFile.ReadByte();
                }

                //CATHODE seems to ignore texture header information regarding size, so as default, resize any imported textures to the original size.
                //An option is provided in the toolkit to write size information to the header (done above) however, so don't resize if that's the case.
                //More work needs to be done to figure out why CATHODE doesn't honour the header's size value.
                Array.Resize(ref NewTexture.DataBlock, TextureEntry.Texture_V2.Length);

                //It's time to try and save!
                try
                {
                    //Write out new BIN
                    ArchiveFileBin.Close();
                    BinaryWriter ArchiveFileWriteBin = new BinaryWriter(File.OpenWrite(ArchivePathBin));
                    ArchiveFileWriteBin.BaseStream.SetLength(0);
                    ArchiveFileWriteBin.Write(BinFile);
                    ArchiveFileWriteBin.Close();

                    //Write out new PAK
                    ArchiveFile.Close();
                    BinaryWriter ArchiveFileWrite = new BinaryWriter(File.OpenWrite(ArchivePath));
                    ArchiveFileWrite.BaseStream.SetLength(0);
                    ArchiveFileWrite.Write(ArchivePt1);
                    ArchiveFileWrite.Write(ArchivePt2);
                    ArchiveFileWrite.Write(ArchivePt3);
                    ArchiveFileWrite.Write(ArchivePt4);
                    ArchiveFileWrite.Write(NewTexture.DataBlock);
                    ArchiveFileWrite.Write(ArchivePt5);
                    ArchiveFileWrite.Close();
                }
                catch
                {
                    //File is probably in-use by the game, re-open for reading and exit as fail
                    Open(ArchivePath);
                    return PAKReturnType.FAILED_FILE_IN_USE;
                }

                //Reload the archive for us
                Open(ArchivePath);
                ParseTexturePAK();

                return PAKReturnType.SUCCESS;
            }
            catch (Exception e)
            {
                LatestError = e.ToString();
                return PAKReturnType.FAILED_UNKNOWN;
            }
        }


        /* --- MODEL PAK --- */
        int TableCountPt1 = -1;
        int TableCountPt2 = -1;
        int FilenameListEnd = -1;

        /* Parse the file listing for a model PAK */
        private List<string> ParseModelPAK()
        {
            //Read the header info from BIN
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

            //Read all file names
            string ThisFileName = "";
            for (int i = 0; i < FilenameListEnd-4; i++)
            {
                byte ThisByte = ArchiveFileBin.ReadByte();
                if (ThisByte == 0x00)
                {
                    FileList.Add(ThisFileName);
                    ThisFileName = "";
                    continue;
                }
                ThisFileName += (char)ThisByte;
            }

            return FileList;
        }

        /* Get a file's size from the model PAK */
        private int FileSizeModelPAK(string FileName)
        {
            //WIP
            return -1;
        }

        /* Export a file from the model PAK */
        private PAKReturnType ExportFileModelPAK(string FileName, string ExportPath)
        {
            //WIP
            return PAKReturnType.FAILED_UNSUPPORTED;
        }

        /* Import a file to the model PAK */
        private PAKReturnType ImportFileModelPAK(string FileName, string ImportPath)
        {
            //WIP
            return PAKReturnType.FAILED_UNSUPPORTED;
        }
    }
}
