#pragma once

#include <windows.h>
#include "Shared.h"
#include <d3dcompiler.h>
#pragma comment(lib, "D3DCompiler.lib")

#include <DirectXMath.h>
#include <DirectXColors.h>

#include "Memory.h"
#include "EXErr.h"
#include "FreeImage.h"
#include "InputHandler.h"

#include "CommonScene.h"
#include "TextureManager.h"

#include <string>
#include <vector>
#include <fstream>
#include <thread>
#include <mutex>
#include <time.h>
#include <random>
#include <iostream>
#include <filesystem>

typedef HRESULT(CALLBACK* LPFNDLLFUNC1)(DWORD, UINT*);

using namespace DirectX;

/* Handle DX HRESULT errors nicely in debug */
#ifdef _DEBUG
	#ifndef HR
		#define HR(x) { HRESULT hr = x; if (FAILED(hr)) { DXTraceW(__FILEW__, __LINE__, hr, L#x, TRUE); } }
	#endif
#else
	#ifndef HR
		#define HR(x) x;
	#endif
#endif

/* Debug logger */
class Debug
{
public:
	static void Log(std::string msg) {
		OutputDebugString(msg.c_str());
		OutputDebugString("\n");
	}
	static void Log(int msg) {
		OutputDebugString(std::to_string(msg).c_str());
		OutputDebugString("\n");
	}
	static void Log(float msg) {
		OutputDebugString(std::to_string(msg).c_str());
		OutputDebugString("\n");
	}
	static void Log(double msg) {
		OutputDebugString(std::to_string(msg).c_str());
		OutputDebugString("\n");
	}
	static void Log(unsigned long msg) {
		OutputDebugString(std::to_string(msg).c_str());
		OutputDebugString("\n");
	}
	static void Log(size_t msg) {
		OutputDebugString(std::to_string(msg).c_str());
		OutputDebugString("\n");
	}
	static void Log(DirectX::XMFLOAT2 msg) {
		OutputDebugString(("(X: " + std::to_string(msg.x) + ", Y: " + std::to_string(msg.y) + ")").c_str());
		OutputDebugString("\n");
	}
	static void Log(DirectX::XMFLOAT3 msg) {
		OutputDebugString(("(X: " + std::to_string(msg.x) + ", Y: " + std::to_string(msg.y) + ", Z: " + std::to_string(msg.z) + ")").c_str());
		OutputDebugString("\n");
	}
	static void Log(Vector2 msg) {
		OutputDebugString(("(X: " + std::to_string(msg.x) + ", Y: " + std::to_string(msg.y) + ")").c_str());
		OutputDebugString("\n");
	}
	static void Log(Vector3 msg) {
		OutputDebugString(("(X: " + std::to_string(msg.x) + ", Y: " + std::to_string(msg.y) + ", Z: " + std::to_string(msg.z) + ")").c_str());
		OutputDebugString("\n");
	}
};

struct SimpleVertexAlt
{
	XMFLOAT3 Pos;
	XMFLOAT2 Tex;
};

struct Ray {
	XMFLOAT3 origin;
	XMFLOAT3 direction;
};

struct Intersection {
	Intersection() = default;
	Intersection(int _i, float _d) {
		entityIndex = _i;
		distance = _d;
	}
	int entityIndex = -1;
	float distance = Shared::cameraFar;
};

struct ConstantBuffer
{
	XMMATRIX mWorld;
	XMMATRIX mView;
	XMMATRIX mProjection;
};

struct ConstantBufferAlt
{
	XMMATRIX mWorld;
	XMMATRIX mView;
	XMMATRIX mProjection;
};

class ConstantBufferRGB {
public:
	XMFLOAT4 colourVal = XMFLOAT4(1.0f, 1.0f, 1.0f, 1.0f);
};

class PosAndRot {
public:
	PosAndRot operator+ (PosAndRot const& obj)
	{
		PosAndRot newTrans = PosAndRot();
		newTrans.position = position + obj.position;
		newTrans.rotation = rotation + obj.rotation;
		return newTrans;
	}
	Vector3 position = Vector3(0, 0, 0);
	Vector3 rotation = Vector3(0, 0, 0);
};

class Utilities
{
public:
	/* Try and compile a shader from file (function created by Microsoft originally) */
	static HRESULT CompileShaderFromFile(LPCWSTR szFileName, LPCSTR szEntryPoint, LPCSTR szShaderModel, ID3DBlob** ppBlobOut)
	{
		//Request d3d debugging if in debug
		DWORD dwShaderFlags = D3DCOMPILE_ENABLE_STRICTNESS;
#ifdef _DEBUG
		dwShaderFlags |= D3DCOMPILE_DEBUG;
		dwShaderFlags |= D3DCOMPILE_SKIP_OPTIMIZATION;
#endif

		//Try and compile shader, handle errors
		ID3DBlob* pErrorBlob = nullptr;
		HRESULT hr = S_OK;
		hr = D3DCompileFromFile(szFileName, nullptr, nullptr, szEntryPoint, szShaderModel,
			dwShaderFlags, 0, ppBlobOut, &pErrorBlob);
		if (FAILED(hr))
		{
			if (pErrorBlob)
			{
				OutputDebugStringA(reinterpret_cast<const char*>(pErrorBlob->GetBufferPointer()));
				pErrorBlob->Release();
			}
			return hr;
		}
		if (pErrorBlob) pErrorBlob->Release();

		return S_OK;
	}

	/* Get the file extension from a filename/path */
	static std::string GetFileExtension(std::string filename)
	{
		const char* file_name = filename.c_str();

		int ext = '.';
		const char* extension = NULL;
		extension = strrchr(file_name, ext);

		if (extension == NULL) return "";
		return extension;
	}

	/* Convert a DirectX Matrix to Float4X4 */
	static XMFLOAT4X4 MatrixToFloat4x4(XMMATRIX mat)
	{
		XMFLOAT4X4 temp;
		XMStoreFloat4x4(&temp, mat);
		return temp;
	}

	/* Add two XMFLOAT3 objects */
	static XMFLOAT3 AddFloat3(XMFLOAT3 a, XMFLOAT3 b)
	{
		return XMFLOAT3(a.x + b.x, a.y + b.y, a.z + b.z);
	}

	/* Multiply two XMFLOAT3 objects */
	static XMFLOAT3 MultiplyFloat3(XMFLOAT3 a, XMFLOAT3 b)
	{
		return XMFLOAT3(a.x * b.x, a.y * b.y, a.z * b.z);
	}

	/* Multiply an XMFLOAT3 by a float */
	static XMFLOAT3 MultiplyFloat3(XMFLOAT3 a, float b)
	{
		return XMFLOAT3(a.x * b, a.y * b, a.z * b);
	}

	/* Transform a LoadedModel object by a world matrix */
	static void TransformLoadedModel(LoadedModel* loadedModel, XMMATRIX worldMatrix) {
		for (int i = 0; i < loadedModel->modelParts.size(); i++) {
			for (int x = 0; x < loadedModel->modelParts[i].compVertices.size(); x++) {
				Vector3* vert = &loadedModel->modelParts[i].compVertices[x].Pos;
				XMFLOAT3 tempPos = XMFLOAT3(vert->x, vert->y, vert->z);
				XMStoreFloat3(&tempPos, XMVector3Transform(XMLoadFloat3(&tempPos), worldMatrix));
				vert->x = tempPos.x; vert->y = tempPos.y; vert->z = tempPos.z;
			}
		}
	}
	
	/* Conversions between vector types */
	static Vector3 Vec3FromDXVec3(DirectX::XMFLOAT3 _vec) {
		return Vector3(_vec.x, _vec.y, _vec.z);
	}
	static DirectX::XMFLOAT3 DXVec3FromVec3(Vector3 _vec) {
		return DirectX::XMFLOAT3(_vec.x, _vec.y, _vec.z);
	}
};