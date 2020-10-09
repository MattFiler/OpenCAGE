#include "CommandsPAK.h"

/* Load and parse the COMMANDS.PAK */
CommandsPAK::CommandsPAK(std::string filepath)
{
    SetupDatatypes();

    pak_stream = new BinaryReader(filepath);

    ReadEntryPoints();
    ReadPrimaryOffsets();
    ReadParameters();
    ReadFlowgraphs();

    delete pak_stream;
    pak_stream = nullptr;
}

/* Parse the three entry flowgraphs for this COMMANDS.PAK */
void CommandsPAK::ReadEntryPoints()
{
    for (int i = 0; i < 3; i++) {
        entry_points.push_back(pak_stream->ReadBytes(4));
    }
}

/* Read the parameter and flowgraph offsets */
void CommandsPAK::ReadPrimaryOffsets()
{
    //Read initial parameter count & offset info
    int parameter_offset_pos;
    pak_stream->Read(parameter_offset_pos);
    parameter_offset_pos *= 4;
    pak_stream->Read(parameter_count);

    //Read initial flowgraph count & offset info
    int flowgraph_offset_pos;
    pak_stream->Read(flowgraph_offset_pos);
    flowgraph_offset_pos *= 4;
    pak_stream->Read(flowgraph_count);

    //Read all offsets for parameters
    parameter_offsets = new int32_t[parameter_count]{};
    pak_stream->SetPosition(parameter_offset_pos);
    for (int i = 0; i < parameter_count; i++)
    {
        pak_stream->Read(parameter_offsets[i]);
        parameter_offsets[i] *= 4;
    }

    //Read all offsets for flowgraphs
    flowgraph_offsets = new int32_t[flowgraph_count]{};
    pak_stream->SetPosition(flowgraph_offset_pos);
    for (int i = 0; i < flowgraph_count; i++)
    {
        pak_stream->Read(flowgraph_offsets[i]);
        flowgraph_offsets[i] *= 4;
    }
}

/* Read all parameters from the PAK */
void CommandsPAK::ReadParameters()
{
    pak_stream->SetPosition(parameter_offsets[0]);
    for (int i = 0; i < parameter_count; i++) {
        int length = (i == parameter_count - 1) ? flowgraph_offsets[0] - parameter_offsets[i] : parameter_offsets[i + 1] - parameter_offsets[i];
        CathodeParameter* this_parameter = nullptr;
        CathodeDataType this_datatype = GetDataType(CathodeID(pak_stream));
        switch (this_datatype)
        {
            case CathodeDataType::TRANSFORM:
            {
                this_parameter = new CathodeTransform();
                //TODO: are these X/Y/Zs the right way around?
                static_cast<CathodeTransform*>(this_parameter)->position = pak_stream->ReadVec3();
                static_cast<CathodeTransform*>(this_parameter)->rotation = pak_stream->ReadVec3();
                break;
            }
            case CathodeDataType::INTEGER:
            {
                this_parameter = new CathodeInteger();
                pak_stream->Read(static_cast<CathodeInteger*>(this_parameter)->value);
                break;
            }
            case CathodeDataType::STRING:
            {
                this_parameter = new CathodeString();
                static_cast<CathodeString*>(this_parameter)->unk0 = CathodeID(pak_stream); // some kind of ID sometimes referenced in script and resource id
                static_cast<CathodeString*>(this_parameter)->unk1 = CathodeID(pak_stream); // sometimes flowgraph id ?!
                bool should_stop = false;
                for (int x = 0; x < length - 8; x++)
                {
                    char this_byte;
                    pak_stream->Read(this_byte);
                    if (this_byte == (char)0x00) should_stop = true;
                    if (should_stop && this_byte != (char)0x00) break;
                    static_cast<CathodeString*>(this_parameter)->value += this_byte;
                }
                pak_stream->SetPosition(pak_stream->GetPosition() - 1);
                break;
            }
            case CathodeDataType::BOOL:
            {
                this_parameter = new CathodeBool();
                int bool_val; pak_stream->Read(bool_val);
                static_cast<CathodeBool*>(this_parameter)->value = (bool_val == 1);
                break;
            }
            case CathodeDataType::FLOAT:
            {
                this_parameter = new CathodeFloat();
                pak_stream->Read(static_cast<CathodeFloat*>(this_parameter)->value);
                break;
            }
            case CathodeDataType::RESOURCE_ID:
            {
                this_parameter = new CathodeResource();
                static_cast<CathodeResource*>(this_parameter)->resourceID = CathodeID(pak_stream);
                break;
            }
            case CathodeDataType::VECTOR3:
            {
                this_parameter = new CathodeVector3();
                static_cast<CathodeVector3*>(this_parameter)->value = pak_stream->ReadVec3();
                break;
            }
            case CathodeDataType::ENUM:
            {
                this_parameter = new CathodeEnum();
                static_cast<CathodeEnum*>(this_parameter)->enumID = CathodeID(pak_stream);
                pak_stream->Read(static_cast<CathodeEnum*>(this_parameter)->enumIndex);
                break;
            }
            default:
            {
                this_parameter = new CathodeParameter();
                this_parameter->unk_content_length = length - 4;
                this_parameter->unk_content = pak_stream->ReadBytes(this_parameter->unk_content_length);
                break;
            }
        }

        this_parameter->offset = parameter_offsets[i];
        this_parameter->data_type = this_datatype;

        parameters.push_back(this_parameter);
    }
}

/* Read all flowgraphs from the PAK */
void CommandsPAK::ReadFlowgraphs()
{
    int scriptStart = parameter_offsets[parameter_count - 1] + 8; //Relies on the last param always being 4 in length
    for (int i = 0; i < flowgraph_count; i++)
    {
        CathodeFlowgraph* flowgraph = new CathodeFlowgraph();

        //Game doesn't parse the script name, so there's no real nice way of grabbing it!!
        pak_stream->SetPosition(scriptStart);
        flowgraph->globalID = CathodeID(pak_stream);
        std::string name = "";
        while (true)
        {
            char thisByte;
            pak_stream->Read(thisByte);
            if (thisByte == (char)0x00) break;
            name += thisByte;
        }
        flowgraph->name = name;
        scriptStart = flowgraph_offsets[i] + 116;
        //End of crappy namegrab

        pak_stream->SetPosition(flowgraph_offsets[i] + 4); //+4 to skip 0x00,0x00,0x00,0x00

        //Read the offsets and counts
        std::vector<OffsetPair> offsetPairs = std::vector<OffsetPair>();
        for (int x = 0; x < 13; x++)
        {
            if (x == 0) flowgraph->uniqueID = CathodeID(pak_stream);
            if (x == 1) flowgraph->nodeID = CathodeID(pak_stream);
            OffsetPair newPair = OffsetPair();
            pak_stream->Read(newPair.GlobalOffset);
            newPair.GlobalOffset *= 4;
            pak_stream->Read(newPair.EntryCount);
            offsetPairs.push_back(newPair);
        }

        //Pull data from those offsets
        for (int x = 0; x < offsetPairs.size(); x++)
        {
            pak_stream->SetPosition(offsetPairs[x].GlobalOffset);
            for (int y = 0; y < offsetPairs[x].EntryCount; y++)
            {
                switch ((CathodeScriptBlocks)x)
                {
                    case CathodeScriptBlocks::DEFINE_NODE_LINKS:
                    {
                        pak_stream->SetPosition(offsetPairs[x].GlobalOffset + (y * 12));
                        CathodeID parentID = CathodeID(pak_stream);

                        //TODO: make a BinaryReader function to do this, as we do it a lot!
                        int OffsetToFindParams;
                        pak_stream->Read(OffsetToFindParams);
                        OffsetToFindParams *= 4;
                        int NumberOfParams;
                        pak_stream->Read(NumberOfParams);

                        for (int z = 0; z < NumberOfParams; z++)
                        {
                            pak_stream->SetPosition(OffsetToFindParams + (z * 16));

                            //TODO
                            //I'm currently unsure here if the parentID is actually the child, and then these are all the parent links (node inputs).
                            //Need to figure this out.

                            CathodeNodeLink newLink = CathodeNodeLink();
                            newLink.connectionID = CathodeID(pak_stream);
                            newLink.parentParamID = CathodeID(pak_stream);
                            newLink.childParamID = CathodeID(pak_stream);
                            newLink.childID = CathodeID(pak_stream);
                            newLink.parentID = parentID;
                            flowgraph->links.push_back(newLink);
                        }
                        break;
                    }
                    case CathodeScriptBlocks::DEFINE_NODE_PARAMETERS:
                    {
                        pak_stream->SetPosition(offsetPairs[x].GlobalOffset + (y * 12));
                        CathodeNodeEntity* thisNode = flowgraph->GetNodeByID(CathodeID(pak_stream));

                        int OffsetToFindParams;
                        pak_stream->Read(OffsetToFindParams);
                        OffsetToFindParams *= 4;
                        int NumberOfParams;
                        pak_stream->Read(NumberOfParams);

                        for (int z = 0; z < NumberOfParams; z++)
                        {
                            pak_stream->SetPosition(OffsetToFindParams + (z * 8));
                            CathodeParameterReference thisParamRef = CathodeParameterReference();
                            thisParamRef.paramID = CathodeID(pak_stream);
                            thisParamRef.editOffset = pak_stream->GetPosition();
                            pak_stream->Read(thisParamRef.offset);
                            thisParamRef.offset *= 4;
                            thisNode->nodeParameterReferences.push_back(thisParamRef);
                        }
                        break;
                    }
                    case CathodeScriptBlocks::UNKNOWN_2:
                    {
                        //WIP
                        break;
                    }
                    case CathodeScriptBlocks::UNKNOWN_3:
                    {
                        //WIP
                        break;
                    }
                    case CathodeScriptBlocks::DEFINE_NODE_DATATYPES:
                    {
                        CathodeNodeEntity* thisNode = flowgraph->GetNodeByID(CathodeID(pak_stream));
                        thisNode->dataType = GetDataType(CathodeID(pak_stream));
                        thisNode->dataTypeParam = CathodeID(pak_stream);
                        break;
                    }
                    case CathodeScriptBlocks::DEFINE_LINKED_NODES:
                    {
                        //WIP
                        break;
                    }
                    case CathodeScriptBlocks::DEFINE_NODE_NODETYPES:
                    {
                        CathodeNodeEntity* thisNode = flowgraph->GetNodeByID(CathodeID(pak_stream));
                        thisNode->nodeType = CathodeID(pak_stream);
                        break;
                    }
                    case CathodeScriptBlocks::UNKNOWN_7:
                    {
                        //WIP
                        break;
                    }
                    case CathodeScriptBlocks::UNKNOWN_8:
                    {
                        //WIP
                        break;
                    }
                    case CathodeScriptBlocks::DEFINE_ZONE_CONTENT:
                    {
                        //WIP
                        break;
                    }
                }
            }
        }

        flowgraphs.push_back(flowgraph);
    }
}
