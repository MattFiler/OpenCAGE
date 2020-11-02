#pragma once

#include "CS2.h"
#include "BinaryReader.h"
#include <vector>

class ModelPAK {
public:
    ModelPAK(std::string filepath);

private:
	std::vector<CS2> ModelEntries = std::vector<CS2>();
    int TableCountPt1 = -1;
    int TableCountPt2 = -1;
    int FilenameListEnd = -1;
    int HeaderListEnd = -1;
};