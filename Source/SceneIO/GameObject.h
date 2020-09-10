#pragma once

#include "Utilities.h"
#include "GameObjectManager.h"

/* The parent GameObject can be inherited from to define an in-world entity */
class GameObject
{
public:
	GameObject() = default;
	GameObject(const GameObject& cpy) {
		mWorld = cpy.mWorld;
		position = cpy.position;
		rotation = cpy.rotation;
		scale = cpy.scale;
		isActive = cpy.isActive; 
		isInvisible = cpy.isInvisible;
	}
	~GameObject() {
		GameObjectManager::RemoveObject(this);
		Release();
	}

	virtual void Create();
	virtual void Release();
	virtual void Update(float dt);
	virtual void Render(float dt);

	virtual void SetRotation(XMFLOAT3 _rot, bool _isRadians = false)
	{
		if (!_isRadians) _rot = DirectX::XMFLOAT3(DirectX::XMConvertToRadians(_rot.x), DirectX::XMConvertToRadians(_rot.y), DirectX::XMConvertToRadians(_rot.z));
		rotation = _rot;
	}
	virtual XMFLOAT3 GetRotation(bool asRadians = true)
	{
		if (asRadians) return rotation;
		return DirectX::XMFLOAT3(DirectX::XMConvertToDegrees(rotation.x), DirectX::XMConvertToDegrees(rotation.y), DirectX::XMConvertToDegrees(rotation.z));
	}

	virtual void SetPosition(XMFLOAT3 _pos)
	{
		position = _pos;
	}
	virtual XMFLOAT3 GetPosition()
	{
		return position;
	}
	
	virtual void SetScale(float _scale)
	{
		SetScale(XMFLOAT3(_scale, _scale, _scale));
	}
	virtual void SetScale(XMFLOAT3 _scale)
	{
		scale = _scale;
	}
	virtual XMFLOAT3 GetScale()
	{
		return scale;
	}

	void SetActive(bool _active) 
	{
		isActive = _active;
	}
	bool GetActive() 
	{
		return isActive;
	}

	void SetInvisible(bool _invis)
	{
		isInvisible = _invis;
	}
	bool GetInvisible()
	{
		return isInvisible;
	}

	XMMATRIX GetWorldMatrix() {
		return mWorld;
	}
	XMFLOAT4X4 GetWorldMatrix4X4() {
		XMFLOAT4X4 temp;
		DirectX::XMStoreFloat4x4(&temp, mWorld);
		return temp;
	}

protected:
	XMMATRIX mWorld;

	XMFLOAT3 position = XMFLOAT3(0.0f, 0.0f, 0.0f);
	XMFLOAT3 rotation = XMFLOAT3(0.0f, 0.0f, 0.0f);
	XMFLOAT3 scale = XMFLOAT3(1.0f, 1.0f, 1.0f);
	bool isActive = true; //If false, won't render or update
	bool isInvisible = false; //If true, will update but not render
};

