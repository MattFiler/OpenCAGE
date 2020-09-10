#include "DebugCube.h"

/* Create resources */
void DebugCube::Create()
{
	GameObject::Create();

#ifdef _DEBUG
	//Create vertex buffer
	SimpleVertexAlt vertices[] =
	{
		{ XMFLOAT3(-1.0f, 1.0f, -1.0f), XMFLOAT2(1.0f, 0.0f) },
		{ XMFLOAT3(1.0f, 1.0f, -1.0f), XMFLOAT2(0.0f, 0.0f) },
		{ XMFLOAT3(1.0f, 1.0f, 1.0f), XMFLOAT2(0.0f, 1.0f) },
		{ XMFLOAT3(-1.0f, 1.0f, 1.0f), XMFLOAT2(1.0f, 1.0f) },

		{ XMFLOAT3(-1.0f, -1.0f, -1.0f), XMFLOAT2(0.0f, 0.0f) },
		{ XMFLOAT3(1.0f, -1.0f, -1.0f), XMFLOAT2(1.0f, 0.0f) },
		{ XMFLOAT3(1.0f, -1.0f, 1.0f), XMFLOAT2(1.0f, 1.0f) },
		{ XMFLOAT3(-1.0f, -1.0f, 1.0f), XMFLOAT2(0.0f, 1.0f) },

		{ XMFLOAT3(-1.0f, -1.0f, 1.0f), XMFLOAT2(0.0f, 1.0f) },
		{ XMFLOAT3(-1.0f, -1.0f, -1.0f), XMFLOAT2(1.0f, 1.0f) },
		{ XMFLOAT3(-1.0f, 1.0f, -1.0f), XMFLOAT2(1.0f, 0.0f) },
		{ XMFLOAT3(-1.0f, 1.0f, 1.0f), XMFLOAT2(0.0f, 0.0f) },

		{ XMFLOAT3(1.0f, -1.0f, 1.0f), XMFLOAT2(1.0f, 1.0f) },
		{ XMFLOAT3(1.0f, -1.0f, -1.0f), XMFLOAT2(0.0f, 1.0f) },
		{ XMFLOAT3(1.0f, 1.0f, -1.0f), XMFLOAT2(0.0f, 0.0f) },
		{ XMFLOAT3(1.0f, 1.0f, 1.0f), XMFLOAT2(1.0f, 0.0f) },

		{ XMFLOAT3(-1.0f, -1.0f, -1.0f), XMFLOAT2(0.0f, 1.0f) },
		{ XMFLOAT3(1.0f, -1.0f, -1.0f), XMFLOAT2(1.0f, 1.0f) },
		{ XMFLOAT3(1.0f, 1.0f, -1.0f), XMFLOAT2(1.0f, 0.0f) },
		{ XMFLOAT3(-1.0f, 1.0f, -1.0f), XMFLOAT2(0.0f, 0.0f) },

		{ XMFLOAT3(-1.0f, -1.0f, 1.0f), XMFLOAT2(1.0f, 1.0f) },
		{ XMFLOAT3(1.0f, -1.0f, 1.0f), XMFLOAT2(0.0f, 1.0f) },
		{ XMFLOAT3(1.0f, 1.0f, 1.0f), XMFLOAT2(0.0f, 0.0f) },
		{ XMFLOAT3(-1.0f, 1.0f, 1.0f), XMFLOAT2(1.0f, 0.0f) },
	};
	GO_VertCount = ARRAYSIZE(vertices);
	D3D11_BUFFER_DESC bd;
	ZeroMemory(&bd, sizeof(bd));
	bd.Usage = D3D11_USAGE_DEFAULT;
	bd.ByteWidth = sizeof(SimpleVertexAlt) * GO_VertCount;
	bd.BindFlags = D3D11_BIND_VERTEX_BUFFER;
	bd.CPUAccessFlags = 0;
	D3D11_SUBRESOURCE_DATA InitData;
	ZeroMemory(&InitData, sizeof(InitData));
	InitData.pSysMem = vertices;
	HR(Shared::m_pDevice->CreateBuffer(&bd, &InitData, &GO_VertexBuffer));

	//Create index buffer 
	WORD indices[] =
	{
		3,1,0,
		2,1,3,

		6,4,5,
		7,4,6,

		11,9,8,
		10,9,11,

		14,12,13,
		15,12,14,

		19,17,16,
		18,17,19,

		22,20,21,
		23,20,22
	};
	GO_IndexCount = ARRAYSIZE(indices);
	bd.Usage = D3D11_USAGE_DEFAULT;
	bd.ByteWidth = sizeof(WORD) * GO_IndexCount;
	bd.BindFlags = D3D11_BIND_INDEX_BUFFER;
	bd.CPUAccessFlags = 0;
	InitData.pSysMem = indices;
	HR(Shared::m_pDevice->CreateBuffer(&bd, &InitData, &GO_IndexBuffer));

	//Constant buffer 
	bd.Usage = D3D11_USAGE_DEFAULT;
	bd.ByteWidth = sizeof(ConstantBufferAlt);
	bd.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
	bd.CPUAccessFlags = 0;
	HR(Shared::m_pDevice->CreateBuffer(&bd, nullptr, &GO_ConstantBuffer));

	// Create the sample state
	D3D11_SAMPLER_DESC sampDesc;
	ZeroMemory(&sampDesc, sizeof(sampDesc));
	sampDesc.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
	sampDesc.AddressU = D3D11_TEXTURE_ADDRESS_WRAP;
	sampDesc.AddressV = D3D11_TEXTURE_ADDRESS_WRAP;
	sampDesc.AddressW = D3D11_TEXTURE_ADDRESS_WRAP;
	sampDesc.ComparisonFunc = D3D11_COMPARISON_NEVER;
	sampDesc.MinLOD = 0;
	sampDesc.MaxLOD = D3D11_FLOAT32_MAX;
	HR(Shared::m_pDevice->CreateSamplerState(&sampDesc, &g_pSamplerLinear));

	//Vertex shader
	ID3DBlob* pVSBlob = nullptr;
	HR(Utilities::CompileShaderFromFile(L"data/ObjectShaderUnlit.fx", "VS_UNLIT", "vs_4_0", &pVSBlob));
	HR(Shared::m_pDevice->CreateVertexShader(pVSBlob->GetBufferPointer(), pVSBlob->GetBufferSize(), nullptr, &GO_VertexShader));

	//Input layout
	D3D11_INPUT_ELEMENT_DESC layout[] =
	{
		{ "POSITION", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 0, D3D11_INPUT_PER_VERTEX_DATA, 0 },
		{ "TEXCOORD", 0, DXGI_FORMAT_R32G32_FLOAT, 0, 12, D3D11_INPUT_PER_VERTEX_DATA, 0 }
	};
	UINT numElements = ARRAYSIZE(layout);
	HR(Shared::m_pDevice->CreateInputLayout(layout, numElements, pVSBlob->GetBufferPointer(), pVSBlob->GetBufferSize(), &GO_VertLayout));
	pVSBlob->Release();

	//Pixel shader
	ID3DBlob* pPSBlob = nullptr;
	HR(Utilities::CompileShaderFromFile(L"data/ObjectShaderUnlit.fx", "PS_UNLIT", "ps_4_0", &pPSBlob));
	HR(Shared::m_pDevice->CreatePixelShader(pPSBlob->GetBufferPointer(), pPSBlob->GetBufferSize(), nullptr, &GO_PixelShader));
	pPSBlob->Release();
#endif
}

/* Release resources */
void DebugCube::Release()
{
	GameObject::Release();
#ifdef _DEBUG
	Memory::SafeRelease(GO_VertexBuffer);
	Memory::SafeRelease(GO_IndexBuffer);
	Memory::SafeRelease(GO_VertexShader);
	Memory::SafeRelease(GO_PixelShader);
	Memory::SafeRelease(GO_ConstantBuffer);
#endif
}

/* Render resources */
void DebugCube::Render(float dt)
{
	GameObject::Render(dt);

#ifdef _DEBUG
	if (showBounds) {
		if (!setTex) {
			Debug::Log("ERROR! Tried to render debug cube, but SetTexture hasn't been called! Check script!");
			return;
		}

		//Input layout
		Shared::m_pImmediateContext->IASetInputLayout(GO_VertLayout);

		//Set sampler
		Shared::m_pImmediateContext->PSSetSamplers(0, 1, &g_pSamplerLinear);

		//Set texture
		Shared::m_pImmediateContext->PSSetShaderResources(0, 1, &materialTexture);

		//Shaders
		Shared::m_pImmediateContext->VSSetShader(GO_VertexShader, nullptr, 0);
		Shared::m_pImmediateContext->PSSetShader(GO_PixelShader, nullptr, 0);

		//Constant buffer
		ConstantBufferAlt cb;
		cb.mWorld = XMMatrixTranspose(mWorld);
		cb.mView = XMMatrixTranspose(Shared::mView);
		cb.mProjection = XMMatrixTranspose(Shared::mProjection);
		Shared::m_pImmediateContext->UpdateSubresource(GO_ConstantBuffer, 0, nullptr, &cb, 0, 0);
		Shared::m_pImmediateContext->VSSetConstantBuffers(0, 1, &GO_ConstantBuffer);
		Shared::m_pImmediateContext->PSSetConstantBuffers(0, 1, &GO_ConstantBuffer);

		//Index/vertex buffers
		Shared::m_pImmediateContext->IASetIndexBuffer(GO_IndexBuffer, DXGI_FORMAT_R16_UINT, 0);
		UINT stride = sizeof(SimpleVertexAlt);
		UINT offset = 0;
		Shared::m_pImmediateContext->IASetVertexBuffers(0, 1, &GO_VertexBuffer, &stride, &offset);

		//Draw
		Shared::m_pImmediateContext->DrawIndexed(GO_IndexCount, 0, 0);
	}
#endif
}

/* Set the texture to use in editor */
void DebugCube::SetTexture(std::string filename)
{
#ifdef _DEBUG
	setTex = false;
	Debug::Log("Setting DebugCube texture to " + filename);

	if (thisTexture != nullptr) thisTexture->RemoveUsage();
	thisTexture = Shared::textureManager->LoadTexture(filename);
	if (thisTexture != nullptr) {
		materialTexture = thisTexture->GetResourceView();
	}
	else {
		materialTexture = nullptr;
	}

	setTex = true;
#endif
}
