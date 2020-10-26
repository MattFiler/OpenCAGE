#pragma once

#include "MaterialParameter.h"
#ifndef SCENEIO_PLUGIN
#include "Utilities.h"
#endif

#ifdef SCENEIO_PLUGIN
class ID3D11VertexShader;
class ID3D11PixelShader;
class ID3D11InputLayout;
#endif

enum class MaterialSurfaceTypes {
	SURFACE,
	VOLUME,
	ENVIRONMENT,
};

/* A material which can be defined by JSON */
class DynamicMaterial {
public:
	DynamicMaterial(json _config, int _index) {
		config = _config;
		matIndex = _index;
		Setup(false);
	}
	DynamicMaterial(const DynamicMaterial& cpy) {
		config = cpy.config;
		Setup(true);
		parameters.clear(); //sanity
		for (int i = 0; i < cpy.parameters.size(); i++) {
			parameters.push_back(new MaterialParameter(*cpy.parameters[i]));
		}
		matIndex = cpy.matIndex;
#ifndef SCENEIO_PLUGIN
		if (cpy.m_vertexLayout && cpy.m_vertexShader && cpy.m_pixelShader) {
			m_vertexLayout = cpy.m_vertexLayout;
			m_vertexShader = cpy.m_vertexShader;
			m_pixelShader = cpy.m_pixelShader;
		}
		else {
			LoadShader();
		}
#endif
	}

	~DynamicMaterial() {
		for (int i = 0; i < parameters.size(); i++) {
			delete parameters[i];
		}
		parameters.clear();
#ifndef SCENEIO_PLUGIN
		if (isCopy) return;
		Memory::SafeRelease(m_vertexShader);
		Memory::SafeRelease(m_pixelShader);
		Memory::SafeDelete(m_vertexLayout);
#endif
	}

	std::string GetName() {
		return name;
	}
	int GetIndex() {
		return matIndex;
	}
	MaterialSurfaceTypes GetSurfaceType() {
		return type;
	}

	MaterialParameter* GetParameter(std::string name) {
		for (int i = 0; i < parameters.size(); i++) {
			if (parameters[i]->name == name) {
				return parameters[i];
			}
		}
		return nullptr;
	}
	MaterialParameter* GetParameter(int index) {
		if (index >= 0 && index < parameters.size()) return parameters[index];
		return nullptr;
	}

	int GetParameterCount() {
		return parameters.size();
	}

#ifndef SCENEIO_PLUGIN
	void SetShader() {
		Shared::m_pImmediateContext->VSSetShader(m_vertexShader, nullptr, 0);
		Shared::m_pImmediateContext->PSSetShader(m_pixelShader, nullptr, 0);
		Shared::m_pImmediateContext->IASetInputLayout(m_vertexLayout);
	}

	void LoadShader() {
		//Compile the vertex shader
		ID3DBlob* pVSBlob = nullptr;
		std::string s = "data/materials/" + name + ".fx";
		std::wstring stemp = std::wstring(s.begin(), s.end());
		HR(Utilities::CompileShaderFromFile(stemp.c_str(), "VS", "vs_4_0", &pVSBlob));

		//Create the vertex shader
		HR(Shared::m_pDevice->CreateVertexShader(pVSBlob->GetBufferPointer(), pVSBlob->GetBufferSize(), nullptr, &m_vertexShader));

		//Define the input layout
		D3D11_INPUT_ELEMENT_DESC layout[] =
		{
			{ "POSITION", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 0, D3D11_INPUT_PER_VERTEX_DATA, 0 },
			{ "TEXCOORD", 0, DXGI_FORMAT_R32G32_FLOAT, 0, 12, D3D11_INPUT_PER_VERTEX_DATA, 0 },
			{ "NORMAL", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 20, D3D11_INPUT_PER_VERTEX_DATA, 0 },
		};
		UINT numElements = ARRAYSIZE(layout);

		//Create the input layout
		HR(Shared::m_pDevice->CreateInputLayout(layout, numElements, pVSBlob->GetBufferPointer(), pVSBlob->GetBufferSize(), &m_vertexLayout));
		pVSBlob->Release();

		//Compile the pixel shader
		ID3DBlob* pPSBlob = nullptr;
		HR(Utilities::CompileShaderFromFile(stemp.c_str(), "PS", "ps_4_0", &pPSBlob));

		//Create the pixel shader
		HR(Shared::m_pDevice->CreatePixelShader(pPSBlob->GetBufferPointer(), pPSBlob->GetBufferSize(), nullptr, &m_pixelShader));
		pPSBlob->Release();
	}
#endif

private:
	void Setup(bool _isCopy) {
		//Setup base material values
		name = config["name"].get<std::string>();

		std::string typeString = config["type"].get<std::string>();
		if (typeString == "SURFACE") type = MaterialSurfaceTypes::SURFACE;
		if (typeString == "VOLUME") type = MaterialSurfaceTypes::VOLUME;
		if (typeString == "ENVIRONMENT") type = MaterialSurfaceTypes::ENVIRONMENT;

		isCopy = _isCopy;
		if (_isCopy) return;

		for (int i = 0; i < config["parameters"].size(); i++) {
			MaterialParameter* newParam = new MaterialParameter(config["parameters"][i]);
			parameters.push_back(newParam);
		}

#ifndef SCENEIO_PLUGIN
		LoadShader();
#endif
	}

	std::string name = "";
	MaterialSurfaceTypes type = MaterialSurfaceTypes::SURFACE;
	std::vector<MaterialParameter*> parameters = std::vector<MaterialParameter*>();
	json config;

	ID3D11VertexShader* m_vertexShader = nullptr;
	ID3D11PixelShader* m_pixelShader = nullptr;
	ID3D11InputLayout* m_vertexLayout = nullptr;

	int matIndex = 0;

	bool isCopy = false;
};