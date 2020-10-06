#include "EditorScene.h"
#include <iomanip>
#include "Picker.h"

/* Init the objects in the scene */
void EditorScene::Init()
{
	Shared::mCurrentGizmoOperation = ImGuizmo::TRANSLATE;

	//Setup cam & light source
	light_source = Light();
	main_cam = Camera();
	GameObjectManager::AddObject(&light_source);
	GameObjectManager::AddObject(&main_cam);
	GameObjectManager::Create();
	main_cam.SetLocked(false);
	light_source.SetPosition(DirectX::XMFLOAT3(0.5, 0.5, 0.5));

	//Position "player"
	main_cam.SetPosition(DirectX::XMFLOAT3(0, 0, 0));
	main_cam.SetRotation(DirectX::XMFLOAT3(0, 0, 0));

	//Setup subsystems
	modelManager = new ModelManager();

	//Load COMMANDS.PAK (todo: pass this filepath into scene)
	commandsPAK = new CommandsPAK("COMMANDS.PAK");

	//DEBUG: Load all flowgraph content into the scene
	for (int i = 0; i < commandsPAK->GetFlowgraphs().size(); i++) {
		LoadFromFlowgraph(i, false);
	}

	//Shared global stuff
	Shared::activeCamera = &main_cam;
}

/* Release all objects in the scene */
void EditorScene::Release() {
	Memory::SafeDelete(modelManager);
	GameObjectManager::Release();
}

/* Update the objects in the scene */
bool EditorScene::Update(double dt)
{
	ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(15, 15));
	ImGui::Begin("Scene Controls", nullptr);
	ImGui::PopStyleVar();

	bool camLock = main_cam.GetLocked();
	ImGui::Checkbox("Lock Cam", &camLock);
	main_cam.SetLocked(camLock);

	ImGui::Separator();
	ImGui::Text(("Cam Pos X: " + std::to_string(main_cam.GetPosition().x) + ", Y: " + std::to_string(main_cam.GetPosition().y) + "Z: " + std::to_string(main_cam.GetPosition().z)).c_str());
	ImGui::Text(("Cam Rot X: " + std::to_string(main_cam.GetRotation().x) + ", Y: " + std::to_string(main_cam.GetRotation().y) + "Z: " + std::to_string(main_cam.GetRotation().z)).c_str());

	ImGui::SliderFloat("Camera FOV", &Shared::cameraFOV, 0.01f, 3.14f);
	if (fovCheck != Shared::cameraFOV) Shared::mProjection = DirectX::XMMatrixPerspectiveFovLH(Shared::cameraFOV, Shared::m_renderWidth / (FLOAT)Shared::m_renderHeight, Shared::cameraNear, Shared::cameraFar);
	fovCheck = Shared::cameraFOV;

	ImGui::Separator();
	ImGui::SliderFloat("Sensitivity", &Shared::mouseCameraSensitivity, 0.0f, 1.0f);

	ImGui::Separator();
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

	//Update flowgraph
	if (loaded_flowgraph != selected_flowgraph) {
		//LoadFromFlowgraph(selected_flowgraph, true);
		loaded_flowgraph = selected_flowgraph;
	}

	modelManager->Update(dt);
	GameObjectManager::Update(dt);

	//Allow user to click on a mesh
	if (InputHandler::KeyDown(WindowsKey::SHIFT) && InputHandler::MouseDown(WindowsMouse::LEFT_CLICK))
	{
		//Create picker ray
		Ray picker = Picker::GenerateRay();

		//Picker to mesh intersection
		if (!testLastFrame) {
			modelManager->SelectModel(picker);
			testLastFrame = true;
		}
	}
	else 
	{
		testLastFrame = false;
	}

	return true;
}

/* Render the objects in the scene */
void EditorScene::Render(double dt)
{
	GameObjectManager::Render(dt);
}

/* Load newly selected flowgraph into the scene */
void EditorScene::LoadFromFlowgraph(int flowgraph_id, bool clear_previous)
{
	//Just for now I'm loading in all transforms, and displaying debug stuff at them.
	//In time this will be expanded to show models, markers, etc.

	if (clear_previous) {
		for (int i = 0; i < debug_cubes.size(); i++) {
			GameObjectManager::RemoveObject(debug_cubes[i]);
			delete debug_cubes[i];
		}
		debug_cubes.clear();
	}

	CathodeFlowgraph* thisFlowgraph = commandsPAK->GetFlowgraphs()[flowgraph_id];
	for (int i = 0; i < thisFlowgraph->nodes.size(); i++) {
		for (int x = 0; x < thisFlowgraph->nodes[i]->nodeParameterReferences.size(); x++) {
			CathodeParameter* thisParamContent = commandsPAK->GetParameter(thisFlowgraph->nodes[i]->nodeParameterReferences[x].offset);
			if (thisParamContent == nullptr) continue;
			if (thisParamContent->data_type != CathodeDataType::TRANSFORM) continue;
			DebugCube* new_cube = new DebugCube();
			new_cube->Create();
			new_cube->SetPosition(static_cast<CathodeTransform*>(thisParamContent)->position);
			new_cube->ShowVisual(true);
			new_cube->SetTexture("plastic_base.png");
			GameObjectManager::AddObject(new_cube);
			debug_cubes.push_back(new_cube);
		}
	}
}
