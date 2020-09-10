#pragma once
#include "CommonMesh.h"

/* Functionality shared between the main application and DLLs */

/* Position and rotation data for the scene's camera */
class SceneCamera {
public:
	SceneCamera() = default;
	SceneCamera(Vector3 _pos, Vector3 _rot, float _fov, bool _usingRadians = true) {
		position = _pos;
		rotation = _rot;
		fov = _fov;
		rotationIsInRadians = _usingRadians;
	}

	Vector3 GetPosition() { return position; }                  //Returns the position of the camera
	Vector3 GetRotation() { return rotation; }                  //Returns the rotation of the camera
	bool IsRotationInRadians() { return rotationIsInRadians; }  //If the camera's rotation is specified in radians rather than degrees, this is true
	float GetFOV() { return fov; }                              //The FOV of the camera in the scene in top-down radians

private:
	Vector3 position = Vector3(0,0,0);
	Vector3 rotation = Vector3(0,0,0);
	bool rotationIsInRadians = true;
	float fov = 1.5f;
};

/* Mesh data along with position and rotation information */
struct LoadedModelPositioner {
	LoadedModel* model = nullptr;        //A LoadedModel object containing the mesh's vertices and indicies PLEASE SPECIFY FILENAME FOR OPTIMISATIONS
	Vector3 position = Vector3(0, 0, 0); //The position of the LoadedModel within the scene in world space
	Vector3 rotation = Vector3(0, 0, 0); //The rotation of the LoadedModel in the scene
	bool rotationIsInRadians = true;     //If the above rotation is in radians, this value should be true
};

/* A definition of a scene, containing camera data and model info */
class LoadedScene {
public:
	std::vector<LoadedModelPositioner> modelDefinitions = std::vector<LoadedModelPositioner>(); //The models within the scene paired with rotation & position
	SceneCamera camera = SceneCamera();                                                         //The camera parameters for the scene
	DynamicMaterial* environmentMat = nullptr;                                                  //The scene's environment material with properties
	Vector2 targetResolution = Vector2(0, 0);                                                   //The target resolution for the scene's render
	Vector2 editorResolution = Vector2(0, 0);                                                   //The resolution of the render in editor (specifying this from DLL will not change the editor's resolution)
};