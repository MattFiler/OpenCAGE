#pragma once

#include "GameObject.h"

/* A debug cube which can be rendered at any given location with any given texture for testing */
class DebugCube : public GameObject {
public:
	~DebugCube() {
		Release();
	}

	void Create() override;
	void Release() override;
	void Render(float dt) override;

	void SetTexture(std::string path);

	void ShowVisual(bool shouldShow) {
#ifdef _DEBUG
		showBounds = shouldShow;
#endif
	}

private:
#ifdef _DEBUG
	bool showBounds = false;
	bool setTex = false;
	ID3D11Buffer* GO_ConstantBuffer = nullptr;
	ID3D11Buffer* GO_VertexBuffer = nullptr;
	ID3D11Buffer* GO_IndexBuffer = nullptr;
	ID3D11SamplerState* g_pSamplerLinear = nullptr;
	ID3D11ShaderResourceView* materialTexture = nullptr; 
	Texture* thisTexture = nullptr;
	ID3D11VertexShader* GO_VertexShader = nullptr;
	ID3D11PixelShader* GO_PixelShader = nullptr;
	ID3D11InputLayout* GO_VertLayout = nullptr;
	int GO_VertCount = 0;
	int GO_IndexCount = 0;
#endif
};