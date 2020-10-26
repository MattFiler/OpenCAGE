#pragma once

#include <vector>

class GameObject;
class Light;
class Model;

/* Keeps a track of all active GameObjects: provides a simple interface for updating/rendering/releasing everything */
class GameObjectManager
{
public:
	GameObjectManager() = default;
	~GameObjectManager() {
		Release();
	}

	static void AddObject(GameObject* _aLight) {
		allGameObjects.push_back(_aLight);
	}
	static void RemoveObject(GameObject* _aLight);

	static std::vector<Light*> GetLights();
	static std::vector<Model*> GetModels();

	static void Create();
	static void Release();
	static void Update(float dt);
	static void Render(float dt);

private:
	static std::vector<GameObject*> allGameObjects;
};

