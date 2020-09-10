#pragma once

#include "Scene.h"

/* EditorScene: default scene for SceneIO */
class EditorScene : public Scene
{
public:
	EditorScene() = default;
	~EditorScene() {
		Release();
	}

	void Init() override;
	void Release() override;

	bool Update(double dt) override;
	void Render(double dt) override;

private:
	float fovCheck = Shared::cameraFOV;

	bool testLastFrame = false;

	Camera main_cam;
	Light light_source;

	ModelManager* modelManager;
};

