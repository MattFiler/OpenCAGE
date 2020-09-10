#pragma once
#include "Utilities.h"
#include "Model.h"
#include "SharedModelBuffers.h"
#include "DynamicMaterialManager.h"

/* Manages all model instances within the scene */
class ModelManager {
public:
	ModelManager();
	~ModelManager();

	void Update(double dt);

	Model* GetModel(int index) {
		if (index >= 0 && index < models.size()) return models[index];
		return nullptr;
	}

	int GetModelCount() {
		return models.size();
	}

	std::vector<Model*>* GetModels() {
		return &models;
	}

	void SelectModel(Ray& _r);

private:
	void ModelManagerUI();
	void ModelTransformUI();
	void ModelMaterialUI();

	SharedModelBuffers* LoadModelToLevel(std::string filename);
	SharedModelBuffers* LoadModelToLevel(LoadedModel* modelref);
	std::vector<SharedModelBuffers*> modelBuffers = std::vector<SharedModelBuffers*>();
	std::vector<Model*> models = std::vector<Model*>();

	std::vector<SharedModelBuffers*> modelBuffersSanity = std::vector<SharedModelBuffers*>(); //DO NOT USE - TEMP DEALLOC CHECK
	float deallocCheckAfter = 10.0f;
	float timeSinceDeallocCheck = 0.0f;

	int selectedModelUI = -1;
	int selectedModelSubmeshUI = -1;

	bool shouldExportAsWorld = false;

	bool didIOThisFrame = false;
};