#pragma once

#include "BinaryReader.h"
#include "CathodeID.h"

class NodeDB {
public:
	NodeDB();
	~NodeDB() {
		param_names.clear();
		node_types.clear();
		node_friendly_names.clear();
	}

	std::string GetParameterName(CathodeID id) {
		if (id.Get() == nullptr) return ""; //TODO: return id as hex string
		for (int i = 0; i < param_names.size(); i++) {
			if (param_names[i].first.Equals(id)) return param_names[i].second;
		}
		return ""; //TODO: return id as hex string
	}

	std::string GetTypeName(CathodeID id) {
		if (id.Get() == nullptr) return ""; //TODO: return id as hex string
		for (int i = 0; i < node_types.size(); i++) {
			if (node_types[i].first.first.Equals(id)) return node_types[i].first.second;
		}
		return ""; //TODO: return id as hex string
	}

	std::vector<std::string> GetAllTypeNames() {
		return node_types_name_only;
	}
	std::vector<std::pair<std::pair<CathodeID, std::string>, bool>> GetAllTypeNamesAndIDs() {
		return node_types;
	}

	//We have extra runtime properties on node types for showing/hiding them in the 3d editor
	bool GetNodeTypeHidden(CathodeID type_id) {
		for (int i = 0; i < node_types.size(); i++) {
			if (node_types[i].first.first.Equals(type_id)) return node_types[i].second;
		}
		return false;
	}
	void SetNodeTypeHidden(CathodeID type_id, bool is_hidden) {
		for (int i = 0; i < node_types.size(); i++) {
			if (node_types[i].first.first.Equals(type_id)) node_types[i].second = is_hidden;
		}
	}

	std::string GetFriendlyName(CathodeID id) {
		if (id.Get() == nullptr) return ""; //TODO: return id as hex string
		for (int i = 0; i < node_friendly_names.size(); i++) {
			if (node_friendly_names[i].first.Equals(id)) return node_friendly_names[i].second;
		}
		return ""; //TODO: return id as hex string
	}

private:
	static std::vector<std::pair<CathodeID, std::string>> param_names;
	static std::vector<std::pair<std::pair<CathodeID, std::string>, bool>> node_types;
	static std::vector<std::string> node_types_name_only; //this index list should match node_types above
	static std::vector<std::pair<CathodeID, std::string>> node_friendly_names;
};