#pragma once

#include "Scene.h"
#include "CommandsPAK.h"

class FlowgraphEditorSceneAlt : public Scene {

public:
	FlowgraphEditorSceneAlt() = default;
	~FlowgraphEditorSceneAlt() {
		Release();
	}

	void Init() override;
	void Release() override;

	bool Update(double dt) override;
	void Render(double dt) override;

private:
	Camera main_cam;
	Light light_source;

	void FlowgraphEditor(CathodeFlowgraph* flowgraph);

	CommandsPAK* commandsPAK = nullptr;
	int selected_flowgraph = 0;
};