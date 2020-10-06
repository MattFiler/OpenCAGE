#include "SceneManager.h"
#include "EditorScene.h"

/* Destroy active scene on exit, if one is */
SceneManager::~SceneManager()
{
	Memory::SafeDelete(Shared::materialManager);
	if (currentSceneIndex != -1) availableScenes[currentSceneIndex]->Release();
	for (int i = 0; i < availableScenes.size(); i++) {
		Memory::SafeRelease(availableScenes[i]);
	}
}

/* Set up the core functionality */
bool SceneManager::Init()
{
	bool dxInit = MainSetup::Init();

	EditorScene* level_scene = new EditorScene();
	AddScene(level_scene);
	ChangeScene(0);

	Shared::materialManager = new DynamicMaterialManager();
	Shared::textureManager = new TextureManager();
	Shared::nodeDB = new NodeDB();

	if (Shared::materialManager->GetEnvironmentMaterial() == nullptr) Debug::Log("ERROR! No environment material template configured!");

	env_mat = new DynamicMaterial(*Shared::materialManager->GetEnvironmentMaterial());
	DataTypeRGB* rgbClearColour = static_cast<DataTypeRGB*>(Shared::materialManager->GetEnvironmentMaterial()->GetParameter("colour")->value);
	rgbClearColour->value.R = DirectX::Colors::CornflowerBlue.f[0];
	rgbClearColour->value.G = DirectX::Colors::CornflowerBlue.f[1];
	rgbClearColour->value.B = DirectX::Colors::CornflowerBlue.f[2];
	Shared::environmentMaterial = env_mat;

	textureSelectFileDialog = ImGui::FileBrowser::FileBrowser(ImGuiFileBrowserFlags_CloseOnEsc);
	textureSelectFileDialog.SetTitle("Texture Selection");

	return dxInit;
}

/* Update the current scene, and handle swapping of scenes */
bool SceneManager::Update(double dt)
{
	//Swap scenes if requested
	if (requestedSceneIndex != currentSceneIndex)
	{
		if (currentSceneIndex != -1) availableScenes[currentSceneIndex]->Release();
		currentSceneIndex = requestedSceneIndex;
		availableScenes[currentSceneIndex]->Init();
	}

	//New ImGui frame
	ImGui_ImplDX11_NewFrame();
	ImGui_ImplWin32_NewFrame();
	ImGui::NewFrame();
	ImGuizmo::BeginFrame();

	//Dockspace
	ImGui::SetNextWindowPos(ImVec2(0, 0), ImGuiCond_FirstUseEver);
	ImGui::SetNextWindowSize(ImVec2(Shared::m_renderWidth, Shared::m_renderHeight), ImGuiCond_FirstUseEver);
	ImGui::Begin("DockSpace", nullptr, ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoBringToFrontOnFocus | ImGuiWindowFlags_NoBackground | ImGuiWindowFlags_MenuBar);
	ImGui::DockSpace(ImGui::GetID("MyDockSpace"), ImVec2(0.0f, 0.0f), ImGuiDockNodeFlags_None | ImGuiDockNodeFlags_PassthruCentralNode);
	ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(15, 15));
	if (ImGui::BeginMenuBar())
	{
		if (ImGui::BeginMenu("File"))
		{
			if (ImGui::MenuItem("Quit")) return false;
			ImGui::EndMenu();
		}
		if (ImGui::BeginMenu("About")) {
			if (ImGui::MenuItem("Controls")) {
				showControlsMenu = true;
			}
			ImGui::EndMenu();
		}

		ImGui::EndMenuBar();
	}
	ImGui::PopStyleVar();
	ImGui::End();
	
	//Controls menu
	if (showControlsMenu) {
		ImGui::SetNextWindowPos(ImVec2(490, 248));
		ImGui::SetNextWindowSize(ImVec2(279, 205));
		ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(15, 15));
		ImGui::Begin("Controls", nullptr, ImGuiWindowFlags_NoDocking | ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoResize);
		ImGui::PopStyleVar();

		ImGui::Text("Select Model: Shift+LMB");
		ImGui::Text("Move Camera: RMB");
		ImGui::Text("Move Camera Alt: Ctrl+RMB");

		ImGui::Separator();

		ImGui::Text("Camera Pan Up: A");
		ImGui::Text("Camera Pan Down: Z");
		ImGui::Text("Camera Pan Left: Q");
		ImGui::Text("Camera Pan Right: E");
		ImGui::Text("Camera Move Up: D");
		ImGui::Text("Camera Move Down: C");

		ImGui::Separator();

		ImGui::Text("Translate Mode: O");
		ImGui::Text("Scale Mode: P");

		ImGui::Separator();

		if (ImGui::Button("Close")) {
			showControlsMenu = false;
		}

		ImGui::End();
	}

	//Environment material controls
	ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(15, 15));
	ImGui::Begin("Environment Material Controls", nullptr);
	ImGui::PopStyleVar();
	ImGui::Checkbox("Use As Clear Colour In Render", &useClearColourFromEnvMat);
	ImGui::Separator();
	for (int i = 0; i < Shared::materialManager->GetEnvironmentMaterial()->GetParameterCount(); i++) {
		MaterialParameter* thisParam = Shared::materialManager->GetEnvironmentMaterial()->GetParameter(i);
		switch (thisParam->value->type) {
			case DataTypes::RGB: {
				DataTypeRGB* param = static_cast<DataTypeRGB*>(thisParam->value);
				ImGui::ColorEdit3(thisParam->name.c_str(), param->value.AsFloatArray());
				break;
			}
			case DataTypes::TEXTURE_FILEPATH: {
				DataTypeTextureFilepath* param = static_cast<DataTypeTextureFilepath*>(thisParam->value);
				if (ImGui::Button((thisParam->name + " Select").c_str())) {
					textureSelectFileDialog.Open();
					currentTextureSelectIndex = i;
				}
				ImGui::SameLine();
				if (ImGui::Button((thisParam->name + " Reset").c_str())) {
					static_cast<DataTypeTextureFilepath*>(thisParam->value)->value = "";
				}
				break;
			}
			case DataTypes::STRING: {
				DataTypeString* param = static_cast<DataTypeString*>(thisParam->value);
				ImGui::InputText(thisParam->name.c_str(), &param->value);
				break;
			}
			case DataTypes::FLOAT: {
				DataTypeFloat* param = static_cast<DataTypeFloat*>(thisParam->value);
				ImGui::InputFloat(thisParam->name.c_str(), &param->value);
				break;
			}
			case DataTypes::INTEGER: {
				DataTypeInt* param = static_cast<DataTypeInt*>(thisParam->value);
				ImGui::InputInt(thisParam->name.c_str(), &param->value);
				break;
			}
			case DataTypes::UNSIGNED_INTEGER: {
				DataTypeUInt* param = static_cast<DataTypeUInt*>(thisParam->value);
				int toEdit = (int)param->value;
				ImGui::InputInt(thisParam->name.c_str(), &toEdit);
				param->value = (uint32_t)toEdit;
				break;
			}
			case DataTypes::BOOLEAN: {
				DataTypeBool* param = static_cast<DataTypeBool*>(thisParam->value);
				ImGui::Checkbox(thisParam->name.c_str(), &param->value);
				break;
			}
			case DataTypes::FLOAT_ARRAY: {
				DataTypeFloatArray* param = static_cast<DataTypeFloatArray*>(thisParam->value);
				//TODO: test this
				ImGui::InputInt((thisParam->name + " length").c_str(), &param->length);
				for (int x = 0; x < param->length; x++) {
					ImGui::InputFloat((thisParam->name + " i" + std::to_string(x)).c_str(), param->value[x]);
				}
				break;
			}
			case DataTypes::OPTIONS_LIST: {
				DataTypeOptionsList* param = static_cast<DataTypeOptionsList*>(thisParam->value);
				if (ImGui::BeginCombo(thisParam->name.c_str(), param->options[param->value].c_str())) {
					for (int x = 0; x < param->options.size(); x++) {
						const bool is_selected = (param->value == x);
						if (ImGui::Selectable(param->options[x].c_str(), is_selected)) param->value = x;
						if (is_selected) ImGui::SetItemDefaultFocus();
					}
					ImGui::EndCombo();
				}
				break;
			}
		}
	}
	ImGui::End();

	//Filepicker for texture filepaths
	ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(15, 15));
	textureSelectFileDialog.Display();
	ImGui::PopStyleVar();
	if (textureSelectFileDialog.IsOpened() && textureSelectFileDialog.HasSelected()) {
		static_cast<DataTypeTextureFilepath*>(Shared::materialManager->GetEnvironmentMaterial()->GetParameter(currentTextureSelectIndex)->value)->value = textureSelectFileDialog.GetSelected().string();
	}

	//Update current scene
	if (currentSceneIndex != -1)
		return availableScenes[currentSceneIndex]->Update(dt);
}

/* Render the current scene (if one is set) */
void SceneManager::Render(double dt)
{
	//Clear back buffer & depth stencil view
	XMVECTORF32 clearColour = DirectX::Colors::CornflowerBlue; 
	if (useClearColourFromEnvMat) {
		DataTypeRGB* rgbClearColour = static_cast<DataTypeRGB*>(Shared::materialManager->GetEnvironmentMaterial()->GetParameter("colour")->value);
		clearColour = { { { rgbClearColour->value.R, rgbClearColour->value.G, rgbClearColour->value.B, 1.000000000f } } };
	}
	m_pImmediateContext->ClearRenderTargetView(m_pRenderTargetView, clearColour);
	m_pImmediateContext->ClearDepthStencilView(g_pDepthStencilView, D3D11_CLEAR_DEPTH, 1.0f, 0);

	//Render scene
	if (currentSceneIndex != -1)
		availableScenes[currentSceneIndex]->Render(dt);

	//Render ImGui
	ImGui::Render();
	ImGui_ImplDX11_RenderDrawData(ImGui::GetDrawData());

	//Present the back buffer to front buffer
	m_pSwapChain->Present(0, 0);
}
