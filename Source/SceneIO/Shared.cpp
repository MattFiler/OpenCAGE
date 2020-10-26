#include "Shared.h"

ID3D11Device* Shared::m_pDevice = nullptr;
ID3D11DeviceContext* Shared::m_pImmediateContext = nullptr;

HWND Shared::m_hwnd;

UINT Shared::m_renderWidth = 0;
UINT Shared::m_renderHeight = 0;

DirectX::XMMATRIX Shared::mView;
DirectX::XMMATRIX Shared::mProjection;

DirectX::XMFLOAT4 Shared::ambientLightColour;

//This should match the DataTypes enum in DataTypes.h
const char* Shared::dataTypes[7] = { "RGB", "STRING", "FLOAT", "INTEGER", "UNSIGNED_INTEGER", "BOOLEAN", "FLOAT_ARRAY" };

DynamicMaterial* Shared::environmentMaterial = nullptr;

Camera* Shared::activeCamera = nullptr;

float Shared::mouseCameraSensitivity = 0.03f;

float Shared::cameraFOV = 1.5f;
float Shared::cameraNear = 0.01f;
float Shared::cameraFar = 2000.0f;

DynamicMaterialManager* Shared::materialManager = nullptr;
TextureManager* Shared::textureManager = nullptr;

NodeDB* Shared::nodeDB = nullptr;

ImGuizmo::OPERATION Shared::mCurrentGizmoOperation = ImGuizmo::ROTATE;
ImGuizmo::MODE Shared::mCurrentGizmoMode = ImGuizmo::WORLD;