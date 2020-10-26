#pragma once

#include "DynamicMaterial.h"
#include <vector>
#include <fstream>

/* Manages all defined material types from JSON, loaded in Shared persistently */
class DynamicMaterialManager {
public:
	DynamicMaterialManager() {
		json config;
		std::fstream cmd_js("data/materials/material_config.json");
		cmd_js >> config;

		for (int i = 0; i < config["materials"].size(); i++) {
			DynamicMaterial* thisMat = new DynamicMaterial(config["materials"][i], i);
			if (thisMat->GetSurfaceType() == MaterialSurfaceTypes::ENVIRONMENT) envMaterial = thisMat;
			else materials.push_back(thisMat);
		}

		if (materials.size() == 0) {
			throw std::out_of_range("ERROR! No materials provided in configuration JSON file. Fatal.");
		}
	}
	~DynamicMaterialManager() = default;

	DynamicMaterial* GetMaterial(std::string name) {
		for (int i = 0; i < materials.size(); i++) {
			if (materials[i]->GetName() == name) {
				return materials[i];
			}
		}
		throw std::out_of_range("ERROR! Incorrect material requested. Fatal.");
	}
	DynamicMaterial* GetMaterial(int index) {
		if (index >= 0 && index < materials.size()) return materials[index];
		throw std::out_of_range("ERROR! Incorrect material requested. Fatal.");
	}

	DynamicMaterial* GetEnvironmentMaterial() {
		return envMaterial;
	}

	int GetMaterialCount() {
		return materials.size();
	}

private:
	std::vector<DynamicMaterial*> materials = std::vector<DynamicMaterial*>();
	DynamicMaterial* envMaterial = nullptr;
};