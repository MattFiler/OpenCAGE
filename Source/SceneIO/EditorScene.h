#pragma once

#include "Scene.h"
#include "CommandsPAK.h"
#include "DebugCube.h"

/* EditorScene */
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
	void LoadFromFlowgraph(int flowgraph_id, bool clear_previous = true);

	float fovCheck = Shared::cameraFOV;

	bool testLastFrame = false;

	Camera main_cam;
	Light light_source;

	CommandsPAK* commandsPAK = nullptr;
	int selected_flowgraph = 0;
	int loaded_flowgraph = -1;

	std::vector<DebugCube*> debug_cubes = std::vector<DebugCube*>();

	ModelManager* modelManager;
};

