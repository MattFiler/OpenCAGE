#include "CommandsPAK.h"

/* Load and parse the COMMANDS.PAK */
CommandsPAK::CommandsPAK(std::string filepath)
{
	pak_stream = new BinaryReader(filepath);

	ReadEntryPoints();
	ReadPrimaryOffsets();
	ReadParameters();

    //test param reading
    for (int i = 0; i < parameters.size(); i++) {
        if (parameters[i]->data_type != CathodeDataType::STRING) continue;
        std::string the_string = static_cast<CathodeString*>(parameters[i])->value;
    }
}

/* Parse the three entry flowgraphs for this COMMANDS.PAK */
void CommandsPAK::ReadEntryPoints()
{
	for (int i = 0; i < 3; i++) {
		char entryPoint[] = { 0x00, 0x00, 0x00, 0x00 };
		pak_stream->Read(entryPoint);
		entryPoints.push_back(entryPoint);
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
	scriptOffsets = new int32_t[flowgraph_count]{};
	pak_stream->SetPosition(flowgraph_offset_pos);
	for (int i = 0; i < flowgraph_count; i++)
	{
		pak_stream->Read(scriptOffsets[i]);
		scriptOffsets[i] *= 4;
	}
}

/* Read all parameters from the PAK */
void CommandsPAK::ReadParameters()
{
	pak_stream->SetPosition(parameter_offsets[0]);
	for (int i = 0; i < parameter_count; i++) {
		int length = (i == parameter_count - 1) ? scriptOffsets[0] - parameter_offsets[i] : parameter_offsets[i + 1] - parameter_offsets[i];
        CathodeParameter* this_parameter = nullptr;
        CathodeDataType this_datatype = GetDataType(pak_stream->ReadBytes(4));
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
                static_cast<CathodeString*>(this_parameter)->unk0 = pak_stream->ReadBytes(4); // some kind of ID sometimes referenced in script and resource id
                static_cast<CathodeString*>(this_parameter)->unk1 = pak_stream->ReadBytes(4); // sometimes flowgraph id ?!
                bool should_stop = false;
                for (int x = 0; x < length - 8; x++)
                {
                    char this_byte;
                    pak_stream->Read(this_byte);
                    if (this_byte == 0x00) should_stop = true;
                    if (should_stop && this_byte != 0x00) break;
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
                static_cast<CathodeResource*>(this_parameter)->resourceID = pak_stream->ReadBytes(4);
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
                static_cast<CathodeEnum*>(this_parameter)->enumID = pak_stream->ReadBytes(4);
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
