#pragma once
#include "CommonMisc.h"

class DynamicMaterial;

/* Functionality shared between the main application and DLLs */

/* A definition of a mesh vertex */
struct SimpleVertex
{
	Vector3 Pos = Vector3(0,0,0);
	Vector2 Tex = Vector2(0,0);
	Vector3 Normal = Vector3(0,0,0);
};

/* A definition of a submesh within a mesh object */
struct LoadedModelPart
{
	// This struct doesn't have a destructor as it's used a bit casually throughout the application, and destroying the material
	// each time can easily cause corruptions. For this reason, when getting rid of an instance of this struct for the final
	// time, you should be aware that you need to MANUALLY delete the material object.

	std::vector<SimpleVertex> compVertices = std::vector<SimpleVertex>(); //Core vertex data
	std::vector<float*> compVerticesAdditional = std::vector<float*>();   //Extra additional vertex data, if required
	std::vector<WORD> compIndices = std::vector<WORD>();                  //Indicies mapping to the vertices and extra vertex data
	DynamicMaterial* material = nullptr;                                  //This model part (submesh)'s material
};

/* A definition of a mesh object */
struct LoadedModel
{
	std::vector<LoadedModelPart> modelParts = std::vector<LoadedModelPart>(); //All model parts (submeshes) that make up this model
	std::string filepath = "";												  //The pathpath to the model - PLEASE specify this as it is used for optimisation
};