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
    CathodeFlowgraph* GetFlowgraph(CathodeID id) {
        for (int i = 0; i < flowgraphs.size(); i++) {
            if (flowgraphs[i]->nodeID.Equals(id)) return flowgraphs[i];
        }
        return nullptr;
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
        for (int i = 0; i < 16; i++) {
            if (tag.Equals(datatype_ids[i])) return (CathodeDataType)i;
        }
        throw std::runtime_error("ERROR! GetDataType couldn't match any CathodeDataType values.");
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

    CathodeID* datatype_ids = nullptr;
    void SetupDatatypes() {
        datatype_ids = new CathodeID[16]{};
        datatype_ids[0] = CathodeID(new char[4]{ (char)0xDA, (char)0x6B, (char)0xD7, (char)0x02 });
        datatype_ids[1] = CathodeID(new char[4]{ (char)0xDC, (char)0x72, (char)0x74, (char)0xFD });
        datatype_ids[2] = CathodeID(new char[4]{ (char)0x84, (char)0x11, (char)0xCD, (char)0x38 });
        datatype_ids[3] = CathodeID(new char[4]{ (char)0x5E, (char)0x8E, (char)0x8E, (char)0x5A });
        datatype_ids[4] = CathodeID(new char[4]{ (char)0xBF, (char)0xA7, (char)0x62, (char)0x8C });
        datatype_ids[5] = CathodeID(new char[4]{ (char)0xF6, (char)0xAF, (char)0x08, (char)0x93 });
        datatype_ids[6] = CathodeID(new char[4]{ (char)0xF0, (char)0x0B, (char)0x76, (char)0x96 });
        datatype_ids[7] = CathodeID(new char[4]{ (char)0x38, (char)0x43, (char)0xFF, (char)0xBF });
        datatype_ids[8] = CathodeID(new char[4]{ (char)0x87, (char)0xC1, (char)0x25, (char)0xE7 });
        datatype_ids[9] = CathodeID(new char[4]{ (char)0xC7, (char)0x6E, (char)0xC8, (char)0x05 });
        datatype_ids[10] = CathodeID(new char[4]{ (char)0x25, (char)0x16, (char)0x14, (char)0x8C });
        datatype_ids[11] = CathodeID(new char[4]{ (char)0x7E, (char)0x39, (char)0xA1, (char)0xDD });
        datatype_ids[12] = CathodeID(new char[4]{ (char)0xD1, (char)0xEA, (char)0x7E, (char)0x5E });
        datatype_ids[13] = CathodeID(new char[4]{ (char)0x93, (char)0xE9, (char)0xE9, (char)0x37 });
        datatype_ids[14] = CathodeID(new char[4]{ (char)0x8A, (char)0x79, (char)0x61, (char)0xC5 });
        datatype_ids[15] = CathodeID(new char[4]{ (char)0x4F, (char)0x2A, (char)0x35, (char)0x5B });
    }
};