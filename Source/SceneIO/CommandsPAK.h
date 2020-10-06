#pragma once

#include "BinaryReader.h"
#include "CathodeFlowgraph.h"
#include <vector>
#include <algorithm>

class CommandsPAK {
public:
	CommandsPAK(std::string filepath);
	~CommandsPAK() {
		if (pak_stream != nullptr) delete pak_stream;
	}

    std::vector<CathodeFlowgraph*> GetFlowgraphs() {
        return flowgraphs;
    }
    CathodeParameter* GetParameter(int offset) {
        for (int i = 0; i < parameters.size(); i++) {
            if (parameters[i]->offset == offset) {
                return parameters[i];
            }
        }
        return nullptr;
    }

protected:
	void ReadEntryPoints();
	void ReadPrimaryOffsets();
	void ReadParameters();
    void ReadFlowgraphs();

    CathodeDataType GetDataType(CathodeID tag)
	{
        //I feel like the remaining UNKNOWN values are actually event triggers (or similar), as they don't define data in the variables block, but are called elsewhere in script

        if (tag.Equals(CathodeID(new char[4] { (char)0xDA, (char)0x6B, (char)0xD7, (char)0x02}))) return CathodeDataType::TRANSFORM;
        else if (tag.Equals(CathodeID(new char[4] {(char)0xDC, (char)0x72, (char)0x74, (char)0xFD }))) return CathodeDataType::FLOAT; //hmm
        else if (tag.Equals(CathodeID(new char[4]{ (char)0x84, (char)0x11, (char)0xCD, (char)0x38 }))) return CathodeDataType::STRING;
        else if (tag.Equals(CathodeID(new char[4]{ (char)0x5E, (char)0x8E, (char)0x8E, (char)0x5A }))) return CathodeDataType::UNKNOWN_2; //unknown long block (pointer, then count, then sets of 24 bytes * count, then 16 bytes), related to splines
        else if (tag.Equals(CathodeID(new char[4]{ (char)0xBF, (char)0xA7, (char)0x62, (char)0x8C }))) return CathodeDataType::ENUM;
        else if (tag.Equals(CathodeID(new char[4]{ (char)0xF6, (char)0xAF, (char)0x08, (char)0x93 }))) return CathodeDataType::RESOURCE_ID;
        else if (tag.Equals(CathodeID(new char[4]{ (char)0xF0, (char)0x0B, (char)0x76, (char)0x96 }))) return CathodeDataType::BOOL;
        else if (tag.Equals(CathodeID(new char[4]{ (char)0x38, (char)0x43, (char)0xFF, (char)0xBF }))) return CathodeDataType::VECTOR3;
        else if (tag.Equals(CathodeID(new char[4]{ (char)0x87, (char)0xC1, (char)0x25, (char)0xE7 }))) return CathodeDataType::INTEGER;
        else if (tag.Equals(CathodeID(new char[4]{ (char)0xC7, (char)0x6E, (char)0xC8, (char)0x05 }))) return CathodeDataType::UNKNOWN_6;
        else if (tag.Equals(CathodeID(new char[4]{ (char)0x25, (char)0x16, (char)0x14, (char)0x8C }))) return CathodeDataType::UNKNOWN_7;
        else if (tag.Equals(CathodeID(new char[4]{ (char)0x7E, (char)0x39, (char)0xA1, (char)0xDD }))) return CathodeDataType::UNKNOWN_8;
        else if (tag.Equals(CathodeID(new char[4]{ (char)0xD1, (char)0xEA, (char)0x7E, (char)0x5E }))) return CathodeDataType::UNKNOWN_9;
        else if (tag.Equals(CathodeID(new char[4]{ (char)0x93, (char)0xE9, (char)0xE9, (char)0x37 }))) return CathodeDataType::UNKNOWN_10;
        else if (tag.Equals(CathodeID(new char[4]{ (char)0x8A, (char)0x79, (char)0x61, (char)0xC5 }))) return CathodeDataType::UNKNOWN_11;
        else if (tag.Equals(CathodeID(new char[4]{ (char)0x4F, (char)0x2A, (char)0x35, (char)0x5B}))) return CathodeDataType::UNKNOWN_12;
        else
        {
            throw std::runtime_error("ERROR! GetDataType couldn't match any CathodeDataType values.");
        }
	}

private:
	BinaryReader* pak_stream = nullptr;

	std::vector<char*> entry_points = std::vector<char*>();

    int32_t* parameter_offsets;
    int parameter_count;
	int32_t* flowgraph_offsets;
    int flowgraph_count;

    std::vector<CathodeParameter*> parameters = std::vector<CathodeParameter*>();
    std::vector<CathodeFlowgraph*> flowgraphs = std::vector<CathodeFlowgraph*>();
};