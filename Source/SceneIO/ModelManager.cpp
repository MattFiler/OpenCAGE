#include "ModelManager.h"
#include "imgui/imgui.h"
#include "InputHandler.h"
#include "Camera.h"
#include <iostream>
#include <algorithm>
#include <vector>
#include <codecvt>
#include <math.h>

/* Setup available types in filepicker */
ModelManager::ModelManager()
{
}

/* Free all model instances */
ModelManager::~ModelManager()
{
	for (int i = 0; i < models.size(); i++) {
		Memory::SafeRelease(models[i]);
	}
	models.clear();
	for (int i = 0; i < modelBuffers.size(); i++) {
		Memory::SafeDelete(modelBuffers[i]);
	}
	modelBuffers.clear();
	Shared::textureManager->UnloadAll();
}

/* Render controls UI */
void ModelManager::Update(double dt)
{
	//Hotkeys for swapping modes (TODO: add rotation back)
	if (InputHandler::KeyDown(WindowsKey::O)) {
		Shared::mCurrentGizmoOperation = ImGuizmo::TRANSLATE;
	}
	else if (InputHandler::KeyDown(WindowsKey::P)) {
		Shared::mCurrentGizmoOperation = ImGuizmo::SCALE;
	}

	//Deallocate from the buffer pool after a time of inactivity
	timeSinceDeallocCheck += dt;
	if (timeSinceDeallocCheck >= deallocCheckAfter) {
		modelBuffersSanity.clear();
		for (int i = 0; i < modelBuffers.size(); i++) {
			if (modelBuffers[i]->GetUsageCount() <= 0) {
				Memory::SafeDelete(modelBuffers[i]);
			}
			else
			{
				modelBuffersSanity.push_back(modelBuffers[i]);
			}
		}
		if (modelBuffersSanity.size() != modelBuffers.size()) {
			Debug::Log("Successfully deallocated " + std::to_string(modelBuffers.size() - modelBuffersSanity.size()) + " model buffer(s).");
			modelBuffers = modelBuffersSanity;
		}
		timeSinceDeallocCheck = 0.0f;
	}

	//UI
	ModelManagerUI();
	if (didIOThisFrame) {
		didIOThisFrame = false;
		GameObjectManager::Update(dt);
	}
	ModelTransformUI();
	ModelMaterialUI();

	Shared::textureManager->Update(dt);
}

/* Model manager core UI (list model instances, add new models, etc) */
void ModelManager::ModelManagerUI()
{
	ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(15, 15));
	ImGui::Begin("Content Controls", nullptr);
	ImGui::PopStyleVar();

	//Show all model instances in a list
	if (ImGui::CollapsingHeader("Model Instances", ImGuiTreeNodeFlags_DefaultOpen)) {
		for (int i = 0; i < models.size(); i++) {
			ImGui::RadioButton((std::to_string(i) + ": " + models[i]->GetSharedBuffers()->GetFilepath()).c_str(), &selectedModelUI, i);
		}
	}

	ImGui::End();
}

/* Model transform UI (control rotation/scale/position of selected model) */
void ModelManager::ModelTransformUI()
{
	//Only continue if our requested edit object is valid
	if (selectedModelUI == -1) return;

	//Get the GameObject we're editing
	GameObject* objectToEdit = models.at(selectedModelUI);

	//Get matrices as float arrays
	float* objectMatrix = &objectToEdit->GetWorldMatrix4X4().m[0][0];
	float* projMatrix = &Utilities::MatrixToFloat4x4(Shared::mProjection).m[0][0];
	float* viewMatrix = &Utilities::MatrixToFloat4x4(Shared::mView).m[0][0];

	//Show options to swap between different transforms
	ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(15, 15));
	ImGui::Begin("Model Transform Control", nullptr);
	ImGui::PopStyleVar();

	if (ImGui::RadioButton("Translate Gizmo", Shared::mCurrentGizmoOperation == ImGuizmo::TRANSLATE))
		Shared::mCurrentGizmoOperation = ImGuizmo::TRANSLATE;
	ImGui::SameLine();
	/* if (ImGui::RadioButton("Rotate Gizmo", Shared::mCurrentGizmoOperation == ImGuizmo::ROTATE))
		Shared::mCurrentGizmoOperation = ImGuizmo::ROTATE;*/
	ImGui::SameLine();
	if (ImGui::RadioButton("Scale Gizmo", Shared::mCurrentGizmoOperation == ImGuizmo::SCALE))
		Shared::mCurrentGizmoOperation = ImGuizmo::SCALE;

	//Allow swap between local/world
	/*
	if (Shared::mCurrentGizmoOperation != ImGuizmo::SCALE)
	{
		if (ImGui::RadioButton("Local", Shared::mCurrentGizmoMode == ImGuizmo::LOCAL))
			Shared::mCurrentGizmoMode = ImGuizmo::LOCAL;
		ImGui::SameLine();
		if (ImGui::RadioButton("World", Shared::mCurrentGizmoMode == ImGuizmo::WORLD))
			Shared::mCurrentGizmoMode = ImGuizmo::WORLD;
	}*/

	//If user isn't clicking on a mesh, allow gizmo control
	if (!(InputHandler::KeyDown(WindowsKey::SHIFT) && InputHandler::MouseDown(WindowsMouse::LEFT_CLICK)))
	{
		//Draw manipulation control
		ImGuizmo::SetRect(0, 0, Shared::m_renderWidth, Shared::m_renderHeight);
		ImGuizmo::Manipulate(viewMatrix, projMatrix, Shared::mCurrentGizmoOperation, Shared::mCurrentGizmoMode, objectMatrix, NULL, NULL);
	}

	//Get values from manipulation
	float matrixTranslation[3], matrixRotation[3], matrixScale[3];
	ImGuizmo::DecomposeMatrixToComponents(objectMatrix, matrixTranslation, matrixRotation, matrixScale);

	//We don't allow gizmo editing of rotation, as ImGuizmo's accuracy really sucks, and throws everything off
	matrixRotation[0] = objectToEdit->GetRotation(false).x;
	matrixRotation[1] = objectToEdit->GetRotation(false).y;
	matrixRotation[2] = objectToEdit->GetRotation(false).z;

	ImGui::Separator();

	//Allow text overwrite
	ImGui::InputFloat3("Translation", matrixTranslation, 3);
	ImGui::InputFloat3("Rotation", matrixRotation, 3);
	ImGui::InputFloat3("Scale", matrixScale, 3);

	//Set new transforms back
	objectToEdit->SetPosition(DirectX::XMFLOAT3(matrixTranslation[0], matrixTranslation[1], matrixTranslation[2]));
	float test[3] = { objectToEdit->GetRotation(false).x, objectToEdit->GetRotation(false).y, objectToEdit->GetRotation(false).z };
	objectToEdit->SetRotation(DirectX::XMFLOAT3(matrixRotation[0], matrixRotation[1], matrixRotation[2]));
	objectToEdit->SetScale(DirectX::XMFLOAT3(matrixScale[0], matrixScale[1], matrixScale[2]));

	/*
	DirectX::XMVECTOR positionv, rotationv, scalev;
	DirectX::XMMatrixDecompose(&scalev, &rotationv, &positionv, objectToEdit->GetWorldMatrix());
	DirectX::XMFLOAT3 position, scale; DirectX::XMFLOAT4 rotation;

	DirectX::XMStoreFloat3(&position, positionv);
	Vec3 test = Vec3(position.x, position.y, position.z);
	ImGui::gizmo3D("Position", test);
	position = XMFLOAT3(test.x, test.y, test.z);
	objectToEdit->SetPosition(position);

	DirectX::XMStoreFloat3(&scale, scalev);
	Vec3 test2 = Vec3(scale.x, scale.y, scale.z);
	ImGui::gizmo3D("Scale", test2);
	scale = XMFLOAT3(test2.x, test2.y, test2.z);
	objectToEdit->SetScale(scale);

	DirectX::XMStoreFloat4(&rotation, rotationv);
	Quat test3 = Quat(rotation.w, rotation.x, rotation.y, rotation.z);
	ImGui::gizmo3D("Rotation", test3);
	rotation = XMFLOAT4(test3.x, test3.y, test3.z, test3.w);
	rotationv = DirectX::XMLoadFloat4(&rotation);
	float roll = atan2(2 * rotation.y * rotation.w - 2 * rotation.x * rotation.z, 1 - 2 * rotation.y * rotation.y - 2 * rotation.z * rotation.z);
	float pitch = atan2(2 * rotation.x * rotation.w - 2 * rotation.y * rotation.z, 1 - 2 * rotation.x * rotation.x - 2 * rotation.z * rotation.z);
	float yaw = asin(2 * rotation.x * rotation.y + 2 * rotation.z * rotation.w);
	objectToEdit->SetRotation(XMFLOAT3(roll, pitch, yaw), true);
	*/

	ImGui::End();
}

/* Model material UI (change selected model's material parameters) */
void ModelManager::ModelMaterialUI()
{
	//Only continue if our requested edit object is valid
	if (selectedModelUI == -1) return;

	//Get the GameObject we're editing
	Model* objectToEdit = models.at(selectedModelUI);

	ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(15, 15));
	ImGui::Begin("Model Material Control", nullptr);
	ImGui::PopStyleVar();

	for (int i = 0; i < objectToEdit->GetSubmeshCount(); i++) {
		if (ImGui::CollapsingHeader(("Submesh " + std::to_string(i)).c_str())) {
			//MaterialDropdownUI(objectToEdit, i);
		}
	}

	ImGui::End();
}

/* Select a model given a ray through the scene */
void ModelManager::SelectModel(Ray& _r)
{
	Intersection closestHit = Intersection();
	float testDistance = 0.0f;
	for (int i = 0; i < models.size(); i++) {
		if (models[i]->DoesIntersect(_r, testDistance) && testDistance <= closestHit.distance) closestHit = Intersection(i, testDistance);
	}
	selectedModelUI = closestHit.entityIndex;
}

/* Requested load of model: check our existing loaded data, and if not already loaded, load it */
SharedModelBuffers* ModelManager::LoadModelToLevel(std::string filename)
{
	//Return an already loaded model buffer, if it exists
	for (int i = 0; i < modelBuffers.size(); i++) {
		if (modelBuffers[i]->GetFilepath() == filename) {
			Debug::Log("Pulling model from pool.");
			return modelBuffers[i];
		}
	}

	//Model isn't already loaded - load it
	SharedModelBuffers* newLoadedModel = new SharedModelBuffers(filename);
	if (!newLoadedModel->DidLoadOK()) return nullptr;
	modelBuffers.push_back(newLoadedModel);
	return newLoadedModel;
}
SharedModelBuffers* ModelManager::LoadModelToLevel(LoadedModel* modelref)
{
	//Return an already loaded model buffer, if it exists
	for (int i = 0; i < modelBuffers.size(); i++) {
		if (modelBuffers[i]->GetFilepath() == modelref->filepath) {
			Debug::Log("Pulling model from pool.");
			return modelBuffers[i];
		}
	}

	//Model isn't already loaded - load it
	SharedModelBuffers* newLoadedModel = new SharedModelBuffers(modelref);
	if (!newLoadedModel->DidLoadOK()) return nullptr;
	modelBuffers.push_back(newLoadedModel);
	return newLoadedModel;
}
