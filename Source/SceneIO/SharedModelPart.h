#pragma once

#include "Utilities.h"
#include "Light.h"
#include "DynamicMaterial.h"

/* A submesh within a SharedModelBuffers object (used per-material type) */
class SharedModelPart {
public:
	SharedModelPart(LoadedModelPart* _m);
	~SharedModelPart();

	void Render(XMMATRIX world, DynamicMaterial* material);

	LoadedModelPart* GetAsLoadedModelPart() {
		return modelMetaData;
	}

private:
	ID3D11Buffer* g_pConstantBuffer = nullptr;
	ID3D11Buffer* g_pIndexBuffer = nullptr;

	ID3D11ShaderResourceView* nullSRV = nullptr; //As odd as this may seem - always keep this value null!

	LoadedModelPart* modelMetaData = nullptr;

	int indexCount = 0;
};