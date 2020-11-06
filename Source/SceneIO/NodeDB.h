#pragma once

#include "BinaryReader.h"
#include "CathodeID.h"

class NodeDB {
public:
	NodeDB();
	~NodeDB() {
		node_types.clear();
		node_friendly_names.clear();
	}

	std::string GetName(CathodeID id) {
		if (id.Get() == nullptr) return ""; //TODO: return id as hex string
		for (int i = 0; i < node_types.size(); i++) {
			if (node_types[i].first.Equals(id)) return node_types[i].second;
		}
		return ""; //TODO: return id as hex string
	}

	std::string GetFriendlyName(CathodeID id) {
		if (id.Get() == nullptr) return ""; //TODO: return id as hex string
		for (int i = 0; i < node_friendly_names.size(); i++) {
			if (node_friendly_names[i].first.Equals(id)) return node_friendly_names[i].second;
		}
		return ""; //TODO: return id as hex string
	}

private:
	static std::vector<std::pair<CathodeID, std::string>> node_types;
	static std::vector<std::pair<CathodeID, std::string>> node_friendly_names;
};