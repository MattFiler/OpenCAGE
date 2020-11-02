#pragma once
#include <string>

struct CS2 {
    /* BIN */
    std::string Filename = "";
    std::string ModelPartName = "";
    std::string MaterialName = ""; //Pulled from MTL with MateralLibraryIndex

    int FilenameOffset = 0;
    int ModelPartNameOffset = 0;
    int MaterialLibaryIndex = 0;
    int BlockSize = 0;
    int ScaleFactor = 0;
    int VertCount = 0;
    int FaceCount = 0;
    int BoneCount = 0;

    /* PAK */
    int PakOffset = 0;
    int PakSize = 0;
};