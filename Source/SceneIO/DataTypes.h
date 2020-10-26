#pragma once

#include <stdexcept>
#ifndef SCENEIO_PLUGIN
#include "Utilities.h"
#endif

/* Inheritable datatypes for dynamic casting */

class ID3D11ShaderResourceView;
class ID3D11Buffer;
class Texture;

//This should match the values and order in Shared.cpp
enum class DataTypes {
	RGB,
	TEXTURE_FILEPATH,
	STRING,
	FLOAT,
	INTEGER,
	UNSIGNED_INTEGER,
	BOOLEAN,
	FLOAT_ARRAY,
	OPTIONS_LIST,
};

class RGBValue
{
public:
	RGBValue() {};
	RGBValue(float _r, float _g, float _b) : R(_r), G(_g), B(_b) {}

	float* AsFloatArray() {
		float* toReturn[3] = { &R, &G, &B };
		return *toReturn;
	}

	float R = 0.0f;
	float G = 0.0f;
	float B = 0.0f;
};

/* Base datatype with static string to enum helper function for type conversion */
class DataType {
public:
	static DataTypes TypeStringToEnum(std::string _t) {
		if (_t == "RGB") return DataTypes::RGB;
		if (_t == "TEXTURE_FILEPATH") return DataTypes::TEXTURE_FILEPATH;
		if (_t == "STRING") return DataTypes::STRING;
		if (_t == "FLOAT") return DataTypes::FLOAT;
		if (_t == "INTEGER") return DataTypes::INTEGER;
		if (_t == "UNSIGNED_INTEGER") return DataTypes::UNSIGNED_INTEGER;
		if (_t == "BOOLEAN") return DataTypes::BOOLEAN;
		if (_t == "FLOAT_ARRAY") return DataTypes::FLOAT_ARRAY;
		if (_t == "OPTIONS_LIST") return DataTypes::OPTIONS_LIST;
		throw new std::invalid_argument("Could not match type string to enum value!");
	}
	DataTypes type;

	/* NOT FOR USE IN DLLS */
	virtual void SetupBindable() {}; //This can be called for any type, but will only effect canBeBound types
	virtual ID3D11Buffer* GetDataBindable() { return nullptr; }; //This will return nullptr if doesn't exist 
	virtual ID3D11ShaderResourceView* GetTextureBindable() { return nullptr; }; //This will return nullptr if doesn't exist 
};

/* These datatypes can be bound to our shader, so they hold their own constant buffer resources */
class DataTypeRGB : public DataType {
public:
#ifndef SCENEIO_PLUGIN
	~DataTypeRGB() {
		Memory::SafeRelease(g_pConstantBuffer);
	}
#endif

	RGBValue value = RGBValue();

#ifndef SCENEIO_PLUGIN
	void SetupBindable() override {
		D3D11_BUFFER_DESC bd;
		ZeroMemory(&bd, sizeof(bd));
		bd.Usage = D3D11_USAGE_DEFAULT;
		bd.ByteWidth = sizeof(ConstantBufferRGB);
		bd.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
		bd.CPUAccessFlags = 0;
		Shared::m_pDevice->CreateBuffer(&bd, nullptr, &g_pConstantBuffer);
	}
	ID3D11Buffer* GetDataBindable() override {
		ConstantBufferRGB rgbConstant = ConstantBufferRGB();
		rgbConstant.colourVal.x = value.R;
		rgbConstant.colourVal.y = value.G;
		rgbConstant.colourVal.z = value.B;
		Shared::m_pImmediateContext->UpdateSubresource(g_pConstantBuffer, 0, nullptr, &rgbConstant, 0, 0);
		return g_pConstantBuffer;
	}
#endif

private:
	ID3D11Buffer* g_pConstantBuffer = nullptr;
};

class DataTypeTextureFilepath : public DataType {
public:
#ifndef SCENEIO_PLUGIN
	~DataTypeTextureFilepath() {
		Memory::SafeRelease(g_pConstantBuffer);
		if (internalTex != nullptr) internalTex->RemoveUsage();
		//Should probably clear defaultColour here too.
	}
#endif

	std::string value = "";
	int* defaultColour;

#ifndef SCENEIO_PLUGIN
	ID3D11ShaderResourceView* GetTextureBindable() override {
		if (value == "" && !isUsingBaseColour) {
			if (internalTex != nullptr) internalTex->RemoveUsage();
			internalTex = Shared::textureManager->LoadTexture(defaultColour);
			isUsingBaseColour = true;
		}
		else
		{
			if (value != "" && (internalTex == nullptr || internalTex->GetFilepath() != value)) {
				internalTex = Shared::textureManager->LoadTexture(value);
				isUsingBaseColour = false; 
			}
		}
		if (internalTex == nullptr) return nullptr;
		return internalTex->GetResourceView();
	}
#endif

private:
	ID3D11Buffer* g_pConstantBuffer = nullptr;
	Texture* internalTex = nullptr;
	bool isUsingBaseColour = false;
};

/* Any following datatypes cannot be bound to our shader, and are used purely for the API */
class DataTypeString : public DataType {
public:
	std::string value = "";
};

class DataTypeFloat : public DataType {
public:
	float value = 0.0f;
};

class DataTypeInt : public DataType {
public:
	int value = 0;
};

class DataTypeUInt : public DataType {
public:
	uint32_t value = 0;
};

class DataTypeBool : public DataType {
public:
	bool value = false;
};

class DataTypeFloatArray : public DataType {
public:
	int length = 0;
	float* value[];
};

class DataTypeOptionsList : public DataType {
public:
	int value = 0;
	std::vector<std::string> options = std::vector<std::string>();
};