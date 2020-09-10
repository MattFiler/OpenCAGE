#pragma once
#include <string>
#include <vector>

/* Functionality shared between the main application and DLLs */

/* A vector of X and Y values */
struct Vector2 {
	Vector2() {}
	Vector2(float _x, float _y) {
		x = _x;
		y = _y;
	}
	Vector2 operator + (Vector2 const& obj) {
		return Vector2(x + obj.x, y + obj.y);
	}
	Vector2 operator - (Vector2 const& obj) {
		return Vector2(x - obj.x, y - obj.y);
	}
	Vector2 operator * (Vector2 const& obj) {
		return Vector2(x * obj.x, y * obj.y);
	}
	Vector2 operator / (Vector2 const& obj) {
		return Vector2(x / obj.x, y / obj.y);
	}

	float x = 0.0f;
	float y = 0.0f;
};

/* A vector of X, Y, and Z values */
struct Vector3 {
	Vector3() {}
	Vector3(float _x, float _y, float _z) {
		x = _x;
		y = _y;
		z = _z;
	}
	Vector3 operator + (Vector3 const& obj) {
		return Vector3(x + obj.x, y + obj.y, z + obj.z);
	}
	Vector3 operator - (Vector3 const& obj) {
		return Vector3(x - obj.x, y - obj.y, z - obj.z);
	}
	Vector3 operator * (Vector3 const& obj) {
		return Vector3(x * obj.x, y * obj.y, z * obj.z);
	}
	Vector3 operator / (Vector3 const& obj) {
		return Vector3(x / obj.x, y / obj.y, z / obj.z);
	}

	float x = 0.0f;
	float y = 0.0f;
	float z = 0.0f;
};

/* Plugin definition */
enum class PluginType {
	DUMMY,

	MODEL_IMPORTER,            //Defines LoadModel functionality
	MODEL_EXPORTER,            //Defines SaveModel functionality
					           
	SCENE_IMPORTER,            //Defines LoadScene functionality
	SCENE_EXPORTER,            //Defines SaveScene functionality
							   
	MODEL_AND_SCENE_IMPORTER,  //Defines LoadModel and LoadScene functionality
	MODEL_AND_SCENE_EXPORTER,  //Defines SaveModel and SaveScene functionality
							   
	UI_BUTTON,                 //Defines ButtonPress functionality

	IMPORT_EVENT,              //Defines ImportEvent functionality
};
struct PluginDefinition {
	//Filled in by the editor tool
	std::string pluginPath = ""; 

	//Filled in by the plugin itself
	std::string pluginName = ""; 
	std::vector<std::string> supportedExtensions = std::vector<std::string>();
	PluginType pluginType = PluginType::DUMMY;

	PluginDefinition() {};
	PluginDefinition(std::string _name, PluginType _type, std::vector<std::string> _extensions = std::vector<std::string>()) : pluginName(_name), supportedExtensions(_extensions), pluginType(_type) {};
};