#include "Picker.h"
#include "imgui/imgui.h"

/* Generate a "picker" ray from the viewport through the scene */
Ray Picker::GenerateRay()
{
	ImGuiIO& io = ImGui::GetIO();

	float px = (((2.0f * io.MousePos.x) / Shared::m_renderWidth) - 1.0f) / Utilities::MatrixToFloat4x4(Shared::mProjection).m[0][0];
	float py = -(((2.0f * io.MousePos.y) / Shared::m_renderHeight) - 1.0f) / Utilities::MatrixToFloat4x4(Shared::mProjection).m[1][1];

	XMMATRIX inverseView = DirectX::XMMatrixInverse(nullptr, Shared::mView);
	XMVECTOR origTransformed = DirectX::XMVector3TransformCoord(XMLoadFloat3(&XMFLOAT3(0.0f, 0.0f, 0.0f)), inverseView);
	XMVECTOR dirTransformed = DirectX::XMVector3TransformNormal(XMLoadFloat3(&XMFLOAT3(px, py, 1.0f)), inverseView);
	dirTransformed = DirectX::XMVector3Normalize(dirTransformed);

	Ray toReturn = Ray();
	XMStoreFloat3(&toReturn.origin, origTransformed);
	XMStoreFloat3(&toReturn.direction, dirTransformed);
	return toReturn;
}
