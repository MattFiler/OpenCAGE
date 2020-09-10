#pragma once

#include <windows.h>
#include <d3d11.h>
#pragma comment(lib, "d3d11.lib")
#include <DirectXMath.h>
#include <DirectXCollision.h>

#include <string>
#include <vector>

#include "imgui/imgui.h"
#include "imgui/imgui_impl_win32.h"
#include "imgui/imgui_impl_dx11.h"
#include "imguizmo/ImGuizmo.h"
#include "imguizmo.quat/imGuIZMOquat.h"
#include "imgui_stl.h"
#include "imfilebrowser/imfilebrowser.h"

enum CameraControlType {
	KEYBOARD,
	MOUSE
};

class DynamicMaterialManager;
class DynamicMaterial;
class PluginManager;
class Camera;
class TextureManager;

/* Shared values which are persistant throughout the application's lifetime */
struct Shared 
{
public:
	/* DirectX/Windows Core */
	static ID3D11Device* m_pDevice;
	static ID3D11DeviceContext* m_pImmediateContext;

	static HWND m_hwnd;

	/* Rendering Parameters */
	static UINT m_renderWidth;
	static UINT m_renderHeight;

	static DirectX::XMMATRIX mView;
	static DirectX::XMMATRIX mProjection;

	static DirectX::XMFLOAT4 ambientLightColour;

	/* Misc Shared Variables */
	const static char* dataTypes[7];

	/* The active scene's global environment material */
	static DynamicMaterial* environmentMaterial;

	/* Active camera ref */
	static Camera* activeCamera;

	/* Camera Properties */
	static float mouseCameraSensitivity;

	static float cameraFOV;
	static float cameraNear;
	static float cameraFar;

	/* Engine Functionality */
	static DynamicMaterialManager* materialManager;
	static TextureManager* textureManager;

	/* ImGuizmo */
	static ImGuizmo::OPERATION mCurrentGizmoOperation;
	static ImGuizmo::MODE mCurrentGizmoMode;
};