#include "GameObject.h"

/* Create our low level GameObject resources */
void GameObject::Create()
{
	//Initialize the world matrix 
	mWorld = XMMatrixIdentity();
}

/* Safely release our low level GameObject memory */
void GameObject::Release() { }

/* Perform low level GameObject update functions */
void GameObject::Update(float dt)
{
	if (!isActive) return;
	mWorld = XMMatrixScaling(scale.x, scale.y, scale.z) * XMMatrixRotationRollPitchYaw(rotation.x, rotation.y, rotation.z) * XMMatrixTranslation(position.x, position.y, position.z);
}

/* Perform low level GameObject render functions */
void GameObject::Render(float dt)
{
	if (!isActive) return;
	if (isInvisible) return;
	//--
}