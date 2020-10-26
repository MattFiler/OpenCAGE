#pragma once

#include "DataTypes.h"

#include "nlohmann/json.hpp"
using json = nlohmann::json;

/* A parameter for a JSON define material */
class MaterialParameter {
public:
	MaterialParameter(json config) {
		//Setup base info
		name = config["name"].get<std::string>();
		DataTypes type = DataType::TypeStringToEnum(config["type"].get<std::string>());
		ChangeValueType(type);
		isBound = config["is_bound"].get<bool>();

		//The options parameter takes in its options from the definition
		if (type == DataTypes::OPTIONS_LIST) {
			DataTypeOptionsList* valueOptions = static_cast<DataTypeOptionsList*>(value);
			for (int i = 0; i < config["values"].size(); i++) {
				valueOptions->options.push_back(config["values"][i].get<std::string>());
			}
		}

		//The filepath parameter takes a default colour for when we have no texture given
		if (type == DataTypes::TEXTURE_FILEPATH) {
			static_cast<DataTypeTextureFilepath*>(value)->defaultColour = new int[3] { config["default_colour"][0].get<int>(), config["default_colour"][1].get<int>(), config["default_colour"][2].get<int>() };
		}
	}
	MaterialParameter(const MaterialParameter& cpy) {
		name = cpy.name;
		isBound = cpy.isBound;
		ChangeValueType(cpy.value->type);
		switch (value->type) {
			case DataTypes::RGB: {
				static_cast<DataTypeRGB*>(value)->value = static_cast<DataTypeRGB*>(cpy.value)->value;
				break;
			}
			case DataTypes::TEXTURE_FILEPATH: {
				static_cast<DataTypeTextureFilepath*>(value)->value = static_cast<DataTypeTextureFilepath*>(cpy.value)->value;
				static_cast<DataTypeTextureFilepath*>(value)->defaultColour = static_cast<DataTypeTextureFilepath*>(cpy.value)->defaultColour;
				break;
			}
			case DataTypes::STRING: {
				static_cast<DataTypeString*>(value)->value = static_cast<DataTypeString*>(cpy.value)->value;
				break;
			}
			case DataTypes::FLOAT: {
				static_cast<DataTypeFloat*>(value)->value = static_cast<DataTypeFloat*>(cpy.value)->value;
				break;
			}
			case DataTypes::INTEGER: {
				static_cast<DataTypeInt*>(value)->value = static_cast<DataTypeInt*>(cpy.value)->value;
				break;
			}
			case DataTypes::UNSIGNED_INTEGER: {
				static_cast<DataTypeUInt*>(value)->value = static_cast<DataTypeUInt*>(cpy.value)->value;
				break;
			}
			case DataTypes::BOOLEAN: {
				static_cast<DataTypeBool*>(value)->value = static_cast<DataTypeBool*>(cpy.value)->value;
				break;
			}
			case DataTypes::FLOAT_ARRAY: {
				static_cast<DataTypeFloatArray*>(value)->length = static_cast<DataTypeFloatArray*>(cpy.value)->length;
				for (int i = 0; i < static_cast<DataTypeFloatArray*>(value)->length; i++) {
					//TODO: test this
					static_cast<DataTypeFloatArray*>(value)->value[i] = static_cast<DataTypeFloatArray*>(cpy.value)->value[i];
				}
				break;
			}
			case DataTypes::OPTIONS_LIST: {
				static_cast<DataTypeOptionsList*>(value)->value = static_cast<DataTypeOptionsList*>(cpy.value)->value;
				static_cast<DataTypeOptionsList*>(value)->options = static_cast<DataTypeOptionsList*>(cpy.value)->options;
				break;
			}
		}
	}

	~MaterialParameter() {
		Release();
	}

	/* Change the datatype of this parameter (WILL RESET THE VALUE) */
	void ChangeValueType(DataTypes newType) {
		Release();
		switch (newType) {
			case DataTypes::RGB: {
				value = new DataTypeRGB();
				break;
			}
			case DataTypes::TEXTURE_FILEPATH: {
				value = new DataTypeTextureFilepath();
				break;
			}
			case DataTypes::STRING: {
				value = new DataTypeString();
				break;
			}
			case DataTypes::FLOAT: {
				value = new DataTypeFloat();
				break;
			}
			case DataTypes::INTEGER: {
				value = new DataTypeInt();
				break;
			}
			case DataTypes::UNSIGNED_INTEGER: {
				value = new DataTypeUInt();
				break;
			}
			case DataTypes::BOOLEAN: {
				value = new DataTypeBool();
				break;
			}
			case DataTypes::FLOAT_ARRAY: {
				value = new DataTypeFloatArray();
				break;
			}
			case DataTypes::OPTIONS_LIST: {
				value = new DataTypeOptionsList();
				break;
			}
		}
		value->type = newType;
#ifndef SCENEIO_PLUGIN
		value->SetupBindable(); //Not all datatypes are bindable, but this is safe to call for any for ease of use.
#endif
	}

	std::string name = "";
	DataType* value = nullptr;
	bool isBound = false;

private:
	void Release() {
		if (value == nullptr) return;
		switch (value->type) {
#ifndef SCENEIO_PLUGIN
			case DataTypes::RGB: {
				DataTypeRGB* valueCast = static_cast<DataTypeRGB*>(value);
				Memory::SafeDelete(valueCast);
				break;
			}
			case DataTypes::TEXTURE_FILEPATH: {
				DataTypeTextureFilepath* valueCast = static_cast<DataTypeTextureFilepath*>(value);
				Memory::SafeDelete(valueCast);
				break;
			}
#endif
			default: {
#ifndef SCENEIO_PLUGIN
				Memory::SafeDelete(value);
#else
				delete value;
				value = nullptr;
#endif
				break;
			}
		}
	}
};