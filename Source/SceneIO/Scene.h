#pragma once

#include "Utilities.h"

#include "Model.h"
#include "Camera.h"
#include "Light.h"

#include "DynamicMaterialManager.h"
#include "ModelManager.h"

/* The parent for a defined "scene" within the SceneManager instance */
class Scene
{
public:
	virtual void Init() = 0;
	virtual void Release() = 0;

	virtual bool Update(double dt) = 0;
	virtual void Render(double dt) = 0;
};

