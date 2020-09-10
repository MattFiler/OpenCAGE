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