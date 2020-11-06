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
	commandsPAK = new CommandsPAK("G:\\SteamLibrary\\steamapps\\common\\Alien Isolation\\DATA\\ENV\\PRODUCTION\\BSP_TORRENS\\WORLD\\COMMANDS.PAK");
	RecursiveLoad(commandsPAK->GetEntryPoint(), PosAndRot());

	//Shared global stuff
	Shared::activeCamera = &main_cam;
}

/* Release all objects in the scene */
void EditorScene::Release() {
	Memory::SafeDelete(modelManager);
	Memory::SafeDelete(commandsPAK);
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
	ImGui::Text(std::to_string(GameObjectManager::GetModels().size()).c_str());

	ImGui::End();

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

/* Recursively load flowgraphs into the scene */
void EditorScene::RecursiveLoad(CathodeFlowgraph* flowgraph, PosAndRot stackedTransform)
{
	for (int i = 0; i < flowgraph->nodes.size(); i++) {
		std::string node_type = Shared::nodeDB->GetName(flowgraph->nodes[i]->nodeType);
		if (!(node_type == "ModelReference" || node_type == "EnvironmentModelReference")) continue;

		PosAndRot thisNodePos = stackedTransform + GetTransform(flowgraph->nodes[i]);
		DebugCube* new_cube = new DebugCube();
		new_cube->Create();
		new_cube->SetPosition(thisNodePos.position);
		new_cube->SetRotation(DirectX::XMFLOAT3(thisNodePos.rotation.x, thisNodePos.rotation.y, thisNodePos.rotation.z));
		new_cube->ShowVisual(true);
		new_cube->SetTexture("plastic_base.png");
		GameObjectManager::AddObject(new_cube);
		debug_cubes.push_back(new_cube);
	}

	for (int i = 0; i < flowgraph->nodes.size(); i++) {
		CathodeFlowgraph* nextCall = commandsPAK->GetFlowgraph(flowgraph->nodes[i]->nodeType);
		if (nextCall == nullptr) continue;
		RecursiveLoad(nextCall, stackedTransform + GetTransform(flowgraph->nodes[i]));
	}
}

/* Get the transform info from a node */
PosAndRot EditorScene::GetTransform(CathodeNodeEntity* node)
{
	PosAndRot toReturn = PosAndRot();
	for (int i = 0; i < node->nodeParameterReferences.size(); i++)
	{
		CathodeParameter* param = commandsPAK->GetParameter(node->nodeParameterReferences[i].offset);
		if (param == nullptr) continue;
		if (param->data_type != CathodeDataType::TRANSFORM) continue;
		toReturn.position = static_cast<CathodeTransform*>(param)->position;
		toReturn.rotation = static_cast<CathodeTransform*>(param)->rotation;
	}
	return toReturn;
}
