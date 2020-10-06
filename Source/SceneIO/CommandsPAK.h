#pragma once

#include "BinaryReader.h"
#include <vector>
#include <algorithm>

#include "CathodeParameter.h"

class CommandsPAK {
public:
	CommandsPAK(std::string filepath);
	~CommandsPAK() {
		if (pak_stream != nullptr) delete pak_stream;
	}

protected:
	void ReadEntryPoints();
	void ReadPrimaryOffsets();
	void ReadParameters();

    CathodeDataType GetDataType(char* tag)
	{
        //I feel like the remaining UNKNOWN values are actually event triggers (or similar), as they don't define data in the variables block, but are called elsewhere in script

        if (tag[0] == (char)0xDA && tag[1] == (char)0x6B && tag[2] == (char)0xD7 && tag[3] == (char)0x02) return CathodeDataType::TRANSFORM;
        else if (tag[0] == (char)0xDC && tag[1] == (char)0x72 && tag[2] == (char)0x74 && tag[3] == (char)0xFD) return CathodeDataType::FLOAT; //hmm
        else if (tag[0] == (char)0x84 && tag[1] == (char)0x11 && tag[2] == (char)0xCD && tag[3] == (char)0x38) return CathodeDataType::STRING;
        else if (tag[0] == (char)0x5E && tag[1] == (char)0x8E && tag[2] == (char)0x8E && tag[3] == (char)0x5A) return CathodeDataType::UNKNOWN_2; //unknown long block (pointer, then count, then sets of 24 bytes * count, then 16 bytes), related to splines
        else if (tag[0] == (char)0xBF && tag[1] == (char)0xA7 && tag[2] == (char)0x62 && tag[3] == (char)0x8C) return CathodeDataType::ENUM;
        else if (tag[0] == (char)0xF6 && tag[1] == (char)0xAF && tag[2] == (char)0x08 && tag[3] == (char)0x93) return CathodeDataType::RESOURCE_ID;
        else if (tag[0] == (char)0xF0 && tag[1] == (char)0x0B && tag[2] == (char)0x76 && tag[3] == (char)0x96) return CathodeDataType::BOOL;
        else if (tag[0] == (char)0x38 && tag[1] == (char)0x43 && tag[2] == (char)0xFF && tag[3] == (char)0xBF) return CathodeDataType::VECTOR3;
        else if (tag[0] == (char)0x87 && tag[1] == (char)0xC1 && tag[2] == (char)0x25 && tag[3] == (char)0xE7) return CathodeDataType::INTEGER;
        else if (tag[0] == (char)0xC7 && tag[1] == (char)0x6E && tag[2] == (char)0xC8 && tag[3] == (char)0x05) return CathodeDataType::UNKNOWN_6;
        else if (tag[0] == (char)0x25 && tag[1] == (char)0x16 && tag[2] == (char)0x14 && tag[3] == (char)0x8C) return CathodeDataType::UNKNOWN_7;
        else if (tag[0] == (char)0x7E && tag[1] == (char)0x39 && tag[2] == (char)0xA1 && tag[3] == (char)0xDD) return CathodeDataType::UNKNOWN_8;
        else if (tag[0] == (char)0xD1 && tag[1] == (char)0xEA && tag[2] == (char)0x7E && tag[3] == (char)0x5E) return CathodeDataType::UNKNOWN_9;
        else if (tag[0] == (char)0x93 && tag[1] == (char)0xE9 && tag[2] == (char)0xE9 && tag[3] == (char)0x37) return CathodeDataType::UNKNOWN_10;
        else if (tag[0] == (char)0x8A && tag[1] == (char)0x79 && tag[2] == (char)0x61 && tag[3] == (char)0xC5) return CathodeDataType::UNKNOWN_11;
        else if (tag[0] == (char)0x4F && tag[1] == (char)0x2A && tag[2] == (char)0x35 && tag[3] == (char)0x5B) return CathodeDataType::UNKNOWN_12;
        else
        {
            throw std::runtime_error("ERROR! GetDataType couldn't match any CathodeDataType values.");
        }
	}

private:
	BinaryReader* pak_stream = nullptr;

	std::vector<char*> entryPoints = std::vector<char*>();

	int parameter_count;
	int flowgraph_count;

	int32_t* scriptOffsets;
	int32_t* parameter_offsets;

    std::vector<CathodeParameter*> parameters = std::vector<CathodeParameter*>();
};