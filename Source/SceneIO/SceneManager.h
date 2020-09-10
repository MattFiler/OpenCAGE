#pragma once

#include "MainSetup.h"
#include "DynamicMaterialManager.h"
#include "Scene.h"

/* The core of the application: calls to set up DirectX, and sets up & handles "scenes" */
class SceneManager : public MainSetup
{
public:
	SceneManager(HINSTANCE hInstance) : MainSetup(hInstance) {};
	~SceneManager();

	bool Init() override;
	bool Update(double dt) override;
	void Render(double dt) override;

	void ChangeScene(int _newScene) 
	{
		if (requestedSceneIndex == _newScene) return;
		Debug::Log("Switching to scene: " + std::to_string(_newScene));
		requestedSceneIndex = _newScene;
	}
	void AddScene(Scene* _newScene)
	{
		Debug::Log("Adding scene!");
		availableScenes.push_back(_newScene);
		Debug::Log("Scene count is now: " + std::to_string(availableScenes.size()));
	}

private:
	int requestedSceneIndex = -1;
	int currentSceneIndex = -1;
	std::vector<Scene*> availableScenes = std::vector<Scene*>();

	DynamicMaterial* env_mat = nullptr;
	bool useClearColourFromEnvMat = true;

	ImGui::FileBrowser textureSelectFileDialog;
	int currentTextureSelectIndex = -1;

	bool showControlsMenu = false;
};

