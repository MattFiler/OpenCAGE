#include "Camera.h"
#include <fstream>
#include <vector>

/* Update the Camera */
void Camera::Update(float dt)
{
	GameObject::Update(dt);

	if (!isActive) return;

	camTarget = DirectX::XMLoadFloat3(&position) + XMVector3Normalize(XMVector3TransformCoord(DefaultForward, XMMatrixRotationRollPitchYaw(rotation.x, rotation.z, rotation.y)));
	Shared::mView = XMMatrixLookAtLH(DirectX::XMLoadFloat3(&position), camTarget, camUp);

	if (isLocked) return;

	DirectX::XMFLOAT3 forward = DirectX::XMFLOAT3(Shared::mView.r[0].m128_f32[2], Shared::mView.r[1].m128_f32[2], Shared::mView.r[2].m128_f32[2]);
	DirectX::XMFLOAT3 right = DirectX::XMFLOAT3(Shared::mView.r[0].m128_f32[0], Shared::mView.r[1].m128_f32[0], Shared::mView.r[2].m128_f32[0]);

	//Camera core mouse movement
	if (InputHandler::MouseDown(WindowsMouse::RIGHT_CLICK)) {
		POINT newMousePos;
		GetCursorPos(&newMousePos);
		if (!mouseWasDownLastFrame) {
			prevMousePos = newMousePos;
			ShowCursor(false);
			mouseWasReset = false;
		}

		float mov_x = (prevMousePos.x - newMousePos.x) * Shared::mouseCameraSensitivity;
		float mov_y = (prevMousePos.y - newMousePos.y) * Shared::mouseCameraSensitivity;

		if (InputHandler::KeyDown(WindowsKey::CTRL)) {
			SetPosition(XMFLOAT3(GetPosition().x + (mov_x * right.x), GetPosition().y - (mov_y), GetPosition().z + (mov_x * right.z)));
		}
		else {
			SetPosition(XMFLOAT3(GetPosition().x + (mov_y * forward.x), GetPosition().y, GetPosition().z + (mov_y * forward.z)));
			SetRotation(XMFLOAT3(GetRotation().x, GetRotation().y, GetRotation().z - (mov_x * 0.25f)), true);
			if (GetRotation(false).z >= 360.0f) SetRotation(XMFLOAT3(GetRotation(false).x, GetRotation(false).y, GetRotation(false).z - 360.0f));
		}

		SetCursorPos(prevMousePos.x, prevMousePos.y);
		mouseWasDownLastFrame = true;
	}
	else
	{
		mouseWasDownLastFrame = false;
		if (!mouseWasReset) {
			SetCursorPos(prevMousePos.x, prevMousePos.y);
			ShowCursor(true);
			mouseWasReset = true;
		}
	}

	//Camera accompanying keyboard movement (TODO: keybinds)
	float speedMod = 1.0f;
	if (InputHandler::KeyDown(WindowsKey::SHIFT)) {
		speedMod = 3.0f;
	}
	if (InputHandler::KeyDown(WindowsKey::Z)) {
		//Look Up
		SetRotation(XMFLOAT3(GetRotation().x + (dt * speedMod), GetRotation().y, GetRotation().z), true);
	}
	if (InputHandler::KeyDown(WindowsKey::A)) {
		//Look Down
		SetRotation(XMFLOAT3(GetRotation().x - (dt * speedMod), GetRotation().y, GetRotation().z), true);
	}
	if (InputHandler::KeyDown(WindowsKey::Q)) {
		//Look Left
		SetRotation(XMFLOAT3(GetRotation().x, GetRotation().y, GetRotation().z - (dt * speedMod)), true);
	}
	if (InputHandler::KeyDown(WindowsKey::E)) {
		//Look Right
		SetRotation(XMFLOAT3(GetRotation().x, GetRotation().y, GetRotation().z + (dt * speedMod)), true);
	}
	if (InputHandler::KeyDown(WindowsKey::D)) {
		//Move Up
		SetPosition(XMFLOAT3(GetPosition().x, GetPosition().y + (dt * speedMod), GetPosition().z));
	}
	if (InputHandler::KeyDown(WindowsKey::C)) {
		//Move Down
		SetPosition(XMFLOAT3(GetPosition().x, GetPosition().y - (dt * speedMod), GetPosition().z));
	}
}