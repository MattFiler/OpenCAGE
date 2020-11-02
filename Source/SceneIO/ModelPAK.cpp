#include "ModelPAK.h"

ModelPAK::ModelPAK(std::string filepath)
{
    /*
    //Read the header info from BIN
    BinaryReader* ArchiveFileBin = new BinaryReader(filepath.substr(0, filepath.size() - 3) + "BIN");
    ArchiveFileBin->SetPosition(4); //Skip magic
    ArchiveFileBin->Read(TableCountPt2);
    ArchiveFileBin->SetPosition(ArchiveFileBin->GetPosition() + 4); //Skip unknown
    ArchiveFileBin->Read(TableCountPt1);

    //Skip past table 1
    for (int i = 0; i < TableCountPt1; i++)
    {
        char ThisByte = 0x00;
        while (ThisByte != 0xFF)
        {
            ArchiveFileBin->Read(ThisByte);
        }
    }
    ArchiveFileBin->SetPosition(ArchiveFileBin->GetPosition() + 23);

    //Read file list info
    ArchiveFileBin->Read(FilenameListEnd);
    int FilenameListStart = ArchiveFileBin->GetPosition();

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
    ArchiveFileBin->Close();
    delete ArchiveFileBin;

    //Get extra info from each header in the PAK
    BinaryReader* ArchiveFile = new BinaryReader(filepath);
    BigEndianUtils BigEndian = new BigEndianUtils();
    ArchiveFile->SetPosition(32); //Skip header
    for (int i = 0; i < TableCountPt2; i++)
    {
        ArchiveFile->SetPosition(ArchiveFile->GetPosition() + 8); //Skip unknowns
        int ThisPakSize = BigEndian.ReadInt32(ArchiveFile);
        if (ThisPakSize != BigEndian.ReadInt32(ArchiveFile))
        {
            //Dud entry... handle this somehow?
        }
        int ThisPakOffset = BigEndian.ReadInt32(ArchiveFile);
        ArchiveFile->SetPosition(ArchiveFile->GetPosition() + 14);
        int ThisIndex = BigEndian.ReadInt16(ArchiveFile);
        ArchiveFile->SetPosition(ArchiveFile->GetPosition() + 12);

        if (ThisIndex == -1)
        {
            continue; //Again, dud entry. Need to look into this!
        }

        //Push it into the correct entry
        ModelEntries[ThisIndex].PakSize = ThisPakSize;
        ModelEntries[ThisIndex].PakOffset = ThisPakOffset;
    }
    HeaderListEnd = ArchiveFile->GetPosition();

    //Done!
    ArchiveFile->Close();
    delete ArchiveFile;
    */
}
