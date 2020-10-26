#include "GameObjectManager.h"
#include "GameObject.h"
#include "Light.h"
#include "Model.h"

std::vector<GameObject*> GameObjectManager::allGameObjects = std::vector<GameObject*>();

/* Remove a game object from the game object pool */
void GameObjectManager::RemoveObject(GameObject * _aLight)
{
	for (int i = 0; i < allGameObjects.size(); i++) {
		if (allGameObjects[i] == _aLight) {
			_aLight->Release();
			allGameObjects.erase(allGameObjects.begin() + i);
		}
	}
}

/* Get all lights in the current game object pool */
std::vector<Light*> GameObjectManager::GetLights()
{
	std::vector<Light*> allLights = std::vector<Light*>();
	for (int i = 0; i < allGameObjects.size(); i++) {
		if (dynamic_cast<Light*>(allGameObjects[i])) {
			allLights.push_back(dynamic_cast<Light*>(allGameObjects[i]));
		}
	}
	return allLights;
}

/* Get all models in the current game object pool */
std::vector<Model*> GameObjectManager::GetModels()
{
	std::vector<Model*> allModels = std::vector<Model*>();
	for (int i = 0; i < allGameObjects.size(); i++) {
		if (dynamic_cast<Model*>(allGameObjects[i])) {
			allModels.push_back(dynamic_cast<Model*>(allGameObjects[i]));
		}
	}
	return allModels;
}

/* Run "Create()" on all gameobjects in the pool */
void GameObjectManager::Create()
{
	for (int i = 0; i < allGameObjects.size(); i++) {
		allGameObjects[i]->Create();
	}
}

/* Run "Release()" on all gameobjects in the pool */
void GameObjectManager::Release()
{
	for (int i = 0; i < allGameObjects.size(); i++) {
		allGameObjects[i]->Release();
	}
	allGameObjects.clear();
}

/* Run "Update(dt)" on all gameobjects in the pool */
void GameObjectManager::Update(float dt)
{
	for (int i = 0; i < allGameObjects.size(); i++) {
		allGameObjects[i]->Update(dt);
	}
}

/* Run "Render(dt)" on all gameobjects in the pool */
void GameObjectManager::Render(float dt)
{
	for (int i = 0; i < allGameObjects.size(); i++) {
		allGameObjects[i]->Render(dt);
	}
}
