#include "FlowgraphEditorSceneAlt.h"

/* Init the objects in the scene */
void FlowgraphEditorSceneAlt::Init()
{
    commandsPAK = new CommandsPAK("G:\\SteamLibrary\\steamapps\\common\\Alien Isolation\\DATA\\ENV\\PRODUCTION\\BSP_LV426_PT02\\WORLD\\COMMANDS.PAK");
}

/* Release all objects in the scene */
void FlowgraphEditorSceneAlt::Release()
{
    Memory::SafeDelete(commandsPAK);
}

/* Update the objects in the scene */
bool FlowgraphEditorSceneAlt::Update(double dt)
{
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(15, 15));
    ImGui::Begin("Scene Controls", nullptr);
    ImGui::PopStyleVar();

    if (ImGui::BeginCombo("##combo", commandsPAK->GetFlowgraphs()[selected_flowgraph]->name.c_str(), 0))
    {
        for (int i = 0; i < commandsPAK->GetFlowgraphs().size(); i++)
        {
            bool is_selected = (selected_flowgraph == i);
            if (ImGui::Selectable(commandsPAK->GetFlowgraphs()[i]->name.c_str(), is_selected))
            {
                selected_flowgraph = i;
                if (is_selected) ImGui::SetItemDefaultFocus();
            }
        }
        ImGui::EndCombo();
    }

    ImGui::End();

    FlowgraphEditor(commandsPAK->GetFlowgraphs()[selected_flowgraph]);

    return true;
}

/* Render the objects in the scene */
void FlowgraphEditorSceneAlt::Render(double dt)
{
}

void FlowgraphEditorSceneAlt::FlowgraphEditor(CathodeFlowgraph* flowgraph)
{
    ImGui::Begin("Node Editor");
    ImGui::Text(("Loaded flowgraph: " + flowgraph->name).c_str());
    imnodes::BeginNodeEditor();

	for (int i = 0; i < flowgraph->nodes.size(); i++) {
		imnodes::BeginNode(i);
		imnodes::BeginNodeTitleBar(); ImGui::TextUnformatted(Shared::nodeDB->GetFriendlyName(flowgraph->nodes[i]->nodeID).c_str()); imnodes::EndNodeTitleBar();
		//ImGui::Text(flowgraphJson["nodes"][i]["friendly_name"].get<std::string>().c_str());
		ImGui::PushItemWidth(150.f);
		ImGui::Dummy(ImVec2(1.0f, 10.0f));
		ImGui::Text(" --- Parameters --- ");
		for (int x = 0; x < flowgraph->nodes[i]->nodeParameterReferences.size(); x++) {
			CathodeParameter* base_param = commandsPAK->GetParameter(flowgraph->nodes[i]->nodeParameterReferences[x].offset);
			ImGui::Text(Shared::nodeDB->GetParameterName(flowgraph->nodes[i]->nodeParameterReferences[x].paramID).c_str());
			switch (base_param->data_type) {
			case CathodeDataType::TRANSFORM:
			{
				CathodeTransform* transform_param = static_cast<CathodeTransform*>(base_param);
				float pos[3] = { transform_param->position.x, transform_param->position.y, transform_param->position.z };
				float rot[3] = { transform_param->rotation.x, transform_param->rotation.y, transform_param->rotation.z };
				ImGui::InputFloat3("Position", pos);
				ImGui::InputFloat3("Rotation", rot);
				break;
			}
			case CathodeDataType::INTEGER:
			{
				ImGui::InputInt("", &static_cast<CathodeInteger*>(base_param)->value);
				break;
			}
			case CathodeDataType::STRING:
			{
				ImGui::InputText("", &static_cast<CathodeString*>(base_param)->value);
				break;
			}
			case CathodeDataType::BOOL:
			{
				ImGui::Checkbox("", &static_cast<CathodeBool*>(base_param)->value);
				break;
			}
			case CathodeDataType::FLOAT:
			{
				ImGui::InputFloat("", &static_cast<CathodeFloat*>(base_param)->value);
				break;
			}
			case CathodeDataType::RESOURCE_ID:
			{
				//std::string stringVal = base_param["value"].get<std::string>();
				//char* stringValCSTR = new char[stringVal.length() + 1];
				//strcpy(stringValCSTR, stringVal.c_str());
				//ImGui::InputText("", stringValCSTR, IM_ARRAYSIZE(stringValCSTR));
				ImGui::Text("RESOURCE_ID [todo]");
				break;
			}
			case CathodeDataType::VECTOR3:
			{
				CathodeVector3* vec_param = static_cast<CathodeVector3*>(base_param);
				float pos[3] = { vec_param->value.x, vec_param->value.y, vec_param->value.z };
				ImGui::InputFloat3("", pos);
				break;
			}
			case CathodeDataType::ENUM:
			{
				//std::string stringVal = base_param["enum_id"].get<std::string>();
				//char* stringValCSTR = new char[stringVal.length() + 1];
				//strcpy(stringValCSTR, stringVal.c_str());
				//ImGui::InputText("", stringValCSTR, IM_ARRAYSIZE(stringValCSTR));
				//int intVal = base_param["index"].get<int>();
				//ImGui::InputInt("", &intVal);
				ImGui::Text("ENUM [todo]");
				break;
			}
			default:
			{
				//std::string stringVal = base_param["raw_value"].get<std::string>();
				//char* stringValCSTR = new char[stringVal.length() + 1];
				//strcpy(stringValCSTR, stringVal.c_str());
				//ImGui::InputText("", stringValCSTR, IM_ARRAYSIZE(stringValCSTR));
				ImGui::Text("OTHER [todo]");
				break;
			}
			}
			ImGui::Dummy(ImVec2(1.0f, 10.0f));
		}
		ImGui::PopItemWidth();
		//ImGui::Dummy(ImVec2(1.0f, 10.0f));
		//ImGui::Text(" --- Parents --- ");
		//for (int x = 0; x < flowgraph->nodes[i]->; x++) {
		//	imnodes::BeginInputAttribute(flowgraphJson["nodes"][i]["parents"][x]["link_id"]); ImGui::Text(flowgraphJson["nodes"][i]["parents"][x]["node_id"].get<std::string>().c_str()); imnodes::EndInputAttribute();
		//}
		//ImGui::Dummy(ImVec2(1.0f, 10.0f));
		//ImGui::Text(" --- Children --- ");
		//for (int x = 0; x < flowgraphJson["nodes"][i]["children"].size(); x++) {
		//	imnodes::BeginOutputAttribute(flowgraphJson["nodes"][i]["children"][x]["link_id"]); ImGui::Text(flowgraphJson["nodes"][i]["children"][x]["node_id"].get<std::string>().c_str()); imnodes::EndOutputAttribute();
		//}
		imnodes::EndNode();
	}

	//int link_id = 0;
	//for (int i = 0; i < flowgraphJson["nodes"].size(); i++) {
	//	for (int x = 0; x < flowgraphJson["nodes"][i]["children"].size(); x++) {
	//		int linkToID = -1;
	//		for (int y = 0; y < flowgraphJson["nodes"].size(); y++) {
	//			if (flowgraphJson["nodes"][y]["id"] == flowgraphJson["nodes"][i]["children"][x]["node_id"]) {
	//				for (int z = 0; z < flowgraphJson["nodes"][y]["parents"].size(); z++) {
	//					if (flowgraphJson["nodes"][y]["parents"][z]["node_id"] == flowgraphJson["nodes"][i]["id"]) {
	//						bool connected = flowgraphJson["nodes"][y]["parents"][z]["connected"].get<bool>();
	//						if (flowgraphJson["nodes"][y]["parents"][z]["connected"].get<bool>() == true) continue;
	//						linkToID = flowgraphJson["nodes"][y]["parents"][z]["link_id"];
	//						flowgraphJson["nodes"][y]["parents"][z]["connected"] = true;
	//						break;
	//					}
	//				}
	//				break;
	//			}
	//		}
	//		if (linkToID == -1) continue;
	//		imnodes::Link(link_id, flowgraphJson["nodes"][i]["children"][x]["link_id"], linkToID);
	//		link_id++;
	//	}
	//}
	//
	//for (int i = 0; i < flowgraphJson["nodes"].size(); i++) {
	//	for (int x = 0; x < flowgraphJson["nodes"][i]["parents"].size(); x++) {
	//		flowgraphJson["nodes"][i]["parents"][x]["connected"] = false;
	//	}
	//}

	imnodes::EndNodeEditor();
	ImGui::End();
}